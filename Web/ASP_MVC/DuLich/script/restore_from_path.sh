#!/bin/bash
# Restore wrapper (invoked over SSH). Adds env + logging for easier debugging.

set -uo pipefail

if [ -z "${1:-}" ]; then
  echo "ERROR: you must provide backup directory path."
  exit 1
fi

BACKUP_DIR=$1

if [ ! -d "$BACKUP_DIR" ]; then
  echo "ERROR: backup directory does not exist: $BACKUP_DIR"
  exit 1
fi

# Fail fast if the directory has no RMAN backup pieces
if ! find "$BACKUP_DIR" -maxdepth 1 -type f -name '*.bkp' -print -quit | grep -q .; then
  echo "ERROR: no *.bkp files found in $BACKUP_DIR (backup directory is empty or invalid)."
  exit 1
fi

BACKUP_ROOT="${BACKUP_ROOT:-/u01/backup}"
ORACLE_HOME="${ORACLE_HOME:-/u01/app/oracle/product/19.0.0/dbhome_1}"
ORACLE_SID="${ORACLE_SID:-ORCLCDB}"
PATH="$ORACLE_HOME/bin:$PATH"
export ORACLE_HOME ORACLE_SID PATH

# Prefer restoring controlfile that is closest to the backup timestamp (avoid resetlogs mismatch)
# 1) Try to pick FRA autobackup at or before backup time; fallback to latest autobackup.
BACKUP_TS_STR=$(basename "$BACKUP_DIR")
# Expect YYYYMMDD_HHMMSS; if parse fails, use current time
if BACKUP_EPOCH=$(date -d "${BACKUP_TS_STR}" +%s 2>/dev/null); then
  :
else
  BACKUP_EPOCH=$(date +%s)
fi
FRA_AUTOBACKUP=$(find /u01/app/oracle/fast_recovery_area/ORCLCDB/autobackup -type f -name 'o1_mf_s_*.bkp' -printf '%T@ %p\n' 2>/dev/null | sort -nr | while read -r ts path; do
  if (( ${ts%.*} <= BACKUP_EPOCH )); then
    echo "$path"
    break
  fi
done)
# If none found before backup time, use newest autobackup
if [ -z "$FRA_AUTOBACKUP" ]; then
  FRA_AUTOBACKUP=$(find /u01/app/oracle/fast_recovery_area/ORCLCDB/autobackup -type f -name 'o1_mf_s_*.bkp' -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n1 | awk '{print $2}')
fi
CF_RESTORE="RESTORE CONTROLFILE FROM AUTOBACKUP;"
if [ -n "$FRA_AUTOBACKUP" ]; then
  CF_RESTORE="RESTORE CONTROLFILE FROM '${FRA_AUTOBACKUP}';"
fi

# Optional: allow incomplete recovery by setting one of these env vars before running:
#   UNTIL_SEQ   (sequence number)
#   UNTIL_SCN   (SCN number)
#   UNTIL_TIME  (timestamp, e.g. 2025-11-24 03:20:00)
UNTIL_BLOCK=""
if [ -n "${UNTIL_SEQ:-}" ]; then
  UNTIL_BLOCK="SET UNTIL SEQUENCE ${UNTIL_SEQ} THREAD 1;"
elif [ -n "${UNTIL_SCN:-}" ]; then
  UNTIL_BLOCK="SET UNTIL SCN ${UNTIL_SCN};"
elif [ -n "${UNTIL_TIME:-}" ]; then
  # Normalize ISO 8601 style (YYYY-MM-DDTHH:MM) to space-separated
  UNTIL_TIME="${UNTIL_TIME/T/ }"
  # Use TO_DATE wrapped in double-quotes as RMAN expects a quoted expression
  UNTIL_BLOCK="SET UNTIL TIME \"TO_DATE('${UNTIL_TIME}','YYYY-MM-DD HH24:MI:SS')\";"
fi

LOG_DIR="${BACKUP_ROOT}/logs"
mkdir -p "${LOG_DIR}"
LOG_FILE="${LOG_DIR}/rman_restore_$(date +%Y%m%d_%H%M%S).log"

echo "Starting restore from directory: ${BACKUP_DIR}"
echo "RMAN log: ${LOG_FILE}"

if ! command -v rman >/dev/null 2>&1; then
  echo "ERROR: rman not found in PATH. Current PATH=$PATH"
  exit 1
fi

rman target / log="${LOG_FILE}" <<EOF
SHUTDOWN IMMEDIATE;
STARTUP FORCE NOMOUNT;
RUN {
  ${UNTIL_BLOCK}
  ${CF_RESTORE}
}
ALTER DATABASE MOUNT;
# Catalog the target directory and the root backup folder to pick up ORCLCDB_* pieces
CATALOG START WITH '${BACKUP_DIR}' NOPROMPT;
CATALOG START WITH '/u01/backup/ORCLCDB_' NOPROMPT;
RUN {
  ${UNTIL_BLOCK}
  RESTORE DATABASE;
  RECOVER DATABASE;
}
ALTER DATABASE OPEN RESETLOGS;
exit;
EOF
RC=$?

if [ $RC -ne 0 ]; then
  echo "Restore failed during RMAN execution (exit=${RC}). Tail of log:"
  tail -n 80 "${LOG_FILE}" || true
  exit $RC
fi

# Post-RMAN steps: Wait for DB to be truly open, then open PDBs.
echo "RMAN process completed. Waiting for database to open..."
# Loop for up to 120 seconds
for i in $(seq 1 120); do
  # Get status from v$instance, remove whitespace/newlines for reliable comparison
  STATUS=$(sqlplus -s / as sysdba <<'EOF'
set heading off feedback off verify off echo off pages 0 trimspool on
select status from v$instance;
exit;
EOF
)
  STATUS=$(echo "$STATUS" | tr -d '[:space:]\r\n')
  if [ "$STATUS" = "OPEN" ]; then
    echo "Container database is OPEN. Opening all Pluggable Databases (PDBs)..."
    echo "ALTER PLUGGABLE DATABASE ALL OPEN;" | sqlplus -s / as sysdba
    echo "ALTER PLUGGABLE DATABASE ALL SAVE STATE;" | sqlplus -s / as sysdba
    echo "All PDBs processed. Restore completed successfully."
    exit 0 # Success
  fi
  echo "Database status is ${STATUS}. Waiting... (${i}s)"
  sleep 1
done

echo "Error: Timed out waiting for database to open after RMAN completed."
tail -n 80 "${LOG_FILE}" || true
exit 1 # Failure
