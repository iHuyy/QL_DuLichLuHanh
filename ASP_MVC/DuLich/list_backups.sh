#!/bin/bash
# Lists available backups by listing the timestamped directories created by run_backup.sh.
# This is more robust than parsing RMAN output.

set -uo pipefail

BACKUP_ROOT="${BACKUP_DIR:-/u01/backup}"
ORACLE_HOME="${ORACLE_HOME:-/u01/app/oracle/product/19.0.0/dbhome_1}"
ORACLE_SID="${ORACLE_SID:-ORCLCDB}"
PATH="$ORACLE_HOME/bin:$PATH"
export ORACLE_HOME ORACLE_SID PATH

# Check if backup root exists
if [ ! -d "$BACKUP_ROOT" ]; then
    echo "ERROR: Backup root directory not found at ${BACKUP_ROOT}" >&2
    exit 1
fi

echo "---BACKUP_LIST_START---"
# Find all subdirectories in the backup root that look like our backup directories (e.g. YYYYMMDD_HHMMSS)
# and list them, newest first.
find "$BACKUP_ROOT" -mindepth 1 -maxdepth 1 -type d -name '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]_[0-9][0-9][0-9][0-9][0-9][0-9]' | sort -r | while read -r backup_dir; do
    # Skip directories that do not contain any backup piece (*.bkp)
    if ! find "$backup_dir" -maxdepth 1 -type f -name '*.bkp' -print -quit | grep -q .; then
        continue
    fi

    # For each directory, output it in a parseable format.
    # The directory name is the identifier.
    dir_name=$(basename "$backup_dir")
    
    # Extract date and time from directory name for a more friendly display
    # Format: YYYYMMDD_HHMMSS -> YYYY-MM-DD HH:MM:SS
    datetime_str=$(echo "$dir_name" | sed -r 's/([0-9]{4})([0-9]{2})([0-9]{2})_([0-9]{2})([0-9]{2})([0-9]{2})/\1-\2-\3 \4:\5:\6/')

    echo "---BACKUPSET_START---"
    echo "Path: $backup_dir"
    echo "Timestamp: $datetime_str"
    echo "---BACKUPSET_END---"
done
echo "---BACKUP_LIST_END---"
