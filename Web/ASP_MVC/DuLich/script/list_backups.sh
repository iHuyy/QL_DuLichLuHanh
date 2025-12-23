#!/bin/bash
# Lists available backups by listing the timestamped directories created by run_backup.sh.
# This is more robust than parsing RMAN output.

set -uo pipefail

BACKUP_ROOT="${BACKUP_DIR:-/u01/backup}"
ORACLE_HOME="${ORACLE_HOME:-/u01/app/oracle/product/19.0.0/dbhome_1}"
ORACLE_SID="${ORACLE_SID:-ORCLCDB}"
PATH="$ORACLE_HOME/bin:$PATH"
export ORACLE_HOME ORACLE_SID PATH

if [ ! -d "$BACKUP_ROOT" ]; then
    echo "ERROR: Backup root directory not found at ${BACKUP_ROOT}" >&2
    exit 1
fi

echo "---BACKUP_LIST_START---"
find "$BACKUP_ROOT" -mindepth 1 -maxdepth 1 -type d -name '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]_[0-9][0-9][0-9][0-9][0-9][0-9]' | sort -r | while read -r backup_dir; do
    # Skip directories that do not contain any backup piece (*.bkp)
    if ! find "$backup_dir" -maxdepth 1 -type f -name '*.bkp' -print -quit | grep -q .; then
        continue
    fi

    dir_name=$(basename "$backup_dir")
    
    datetime_str=$(echo "$dir_name" | sed -r 's/([0-9]{4})([0-9]{2})([0-9]{2})_([0-9]{2})([0-9]{2})([0-9]{2})/\1-\2-\3 \4:\5:\6/')

    echo "---BACKUPSET_START---"
    echo "Path: $backup_dir"
    echo "Timestamp: $datetime_str"
    echo "---BACKUPSET_END---"
done
echo "---BACKUP_LIST_END---"
