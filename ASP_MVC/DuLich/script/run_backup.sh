#!/bin/bash
# Simple RMAN backup wrapper used by the ASP.NET app (invoked over SSH).
# Sets Oracle env, logs output, and prints BACKUP_PATH for the C# service.

set -uo pipefail

BACKUP_ROOT="${BACKUP_DIR:-/u01/backup}"
ORACLE_HOME="${ORACLE_HOME:-/u01/app/oracle/product/19.0.0/dbhome_1}"
ORACLE_SID="${ORACLE_SID:-ORCLCDB}"
PATH="$ORACLE_HOME/bin:$PATH"
export ORACLE_HOME ORACLE_SID PATH

RUN_ID=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="${BACKUP_ROOT}/${RUN_ID}"
mkdir -p "$BACKUP_DIR" "$BACKUP_DIR/logs"

LOG_FILE="${BACKUP_DIR}/logs/rman_backup_$(date +%Y%m%d_%H%M%S).log"
START_TIME=$(date +%s)

if ! command -v rman >/dev/null 2>&1; then
  echo "ERROR: rman not found in PATH. Current PATH=$PATH"
  exit 1
fi

if [ "${1:-}" == "--full" ]; then
  echo "Requested FULL backup (online)."
  RMAN_SCRIPT=$(cat <<EOF
RUN {
  SQL 'alter system archive log current';
  BACKUP AS COMPRESSED BACKUPSET DATABASE PLUS ARCHIVELOG FORMAT '${BACKUP_DIR}/full_%T_%U.bkp';
}
EOF
)
elif [ "${1:-}" == "--incremental" ]; then
  echo "Requested INCREMENTAL backup (online)."
  RMAN_SCRIPT=$(cat <<EOF
RUN {
  BACKUP INCREMENTAL LEVEL 1 DATABASE PLUS ARCHIVELOG FORMAT '${BACKUP_DIR}/inc_%T_%U.bkp';
}
EOF
)
else
  echo "Usage: $0 --full|--incremental"
  exit 1
fi

echo "Running RMAN... log: ${LOG_FILE}"
rman target / log="${LOG_FILE}" <<EOF
${RMAN_SCRIPT}
exit;
EOF
RC=$?

if [ $RC -eq 0 ]; then
  echo "RMAN completed successfully."
  echo "Backup set is located in: ${BACKUP_DIR}"
  echo "BACKUP_PATH=${BACKUP_DIR}"
else
  echo "RMAN failed (exit=${RC}). Tail of log:"
  tail -n 50 "${LOG_FILE}" || true
  exit $RC
fi
