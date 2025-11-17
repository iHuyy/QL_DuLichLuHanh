#!/bin/bash
export ORACLE_SID=ORCLCDB          # SID CDB của bạn
export ORACLE_HOME=/u01/app/oracle/product/19.0.0/dbhome_1
PATH=$ORACLE_HOME/bin:$PATH

mode="--full"
[ "$1" = "--incremental" ] && mode="--incremental"

rman target / <<EOF
RUN {
  CROSSCHECK BACKUP;
  DELETE NOPROMPT EXPIRED BACKUP;
  BACKUP AS COMPRESSED BACKUPSET DATABASE PLUS ARCHIVELOG;
  DELETE NOPROMPT ARCHIVELOG ALL BACKED UP 2 TIMES TO DISK;
}
EOF

# Lấy file mới nhất và in ra BACKUP_PATH để app đọc
latest=$(ls -1t /u01/backup | head -1)
echo "BACKUP_PATH=/u01/backup/$latest"
