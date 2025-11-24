#!/bin/bash
# Restore wrapper (invoked over SSH). Adds env + logging for easier debugging.

set -uo pipefail

if [ -z "${1:-}" ]; then
  echo "ERROR: you must provide backup directory path."
  exit 1
fi

BACKUP_DIR=$1

BACKUP_ROOT="${BACKUP_ROOT:-/u01/backup}"
ORACLE_HOME="${ORACLE_HOME:-/u01/app/oracle/product/19.0.0/dbhome_1}"
ORACLE_SID="${ORACLE_SID:-ORCLCDB}"
PATH="$ORACLE_HOME/bin:$PATH"
export ORACLE_HOME ORACLE_SID PATH

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
  # Use to_date to avoid NLS issues
  UNTIL_BLOCK="SET UNTIL TIME to_date('${UNTIL_TIME}','YYYY-MM-DD HH24:MI:SS');"
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
  RESTORE CONTROLFILE FROM AUTOBACKUP;
}
ALTER DATABASE MOUNT;
CATALOG START WITH '${BACKUP_DIR}';
RUN {
  ${UNTIL_BLOCK}
  RESTORE DATABASE;
  RECOVER DATABASE;
}
ALTER DATABASE OPEN RESETLOGS;
# Open all PDBs after restore
WHENEVER SQLERROR EXIT SQL.SQLCODE;
CONNECT / AS SYSDBA
ALTER PLUGGABLE DATABASE ALL OPEN;
ALTER PLUGGABLE DATABASE ALL SAVE STATE;
exit;
EOF
RC=$?

if [ $RC -eq 0 ]; then
  echo "Restore completed successfully."
else
  echo "Restore failed (exit=${RC}). Tail of log:"
  tail -n 80 "${LOG_FILE}" || true
  exit $RC
fi
