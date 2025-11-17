#!/bin/bash
export ORACLE_SID=ORCLCDB
export ORACLE_HOME=/u01/app/oracle/product/19.0.0/dbhome_1
PATH=$ORACLE_HOME/bin:$PATH

path="$1"
if [ -z "$path" ]; then echo "Missing backup path"; exit 1; fi

rman target / <<EOF
STARTUP NOMOUNT;
RESTORE CONTROLFILE FROM '$path';
ALTER DATABASE MOUNT;
RESTORE DATABASE;
RECOVER DATABASE;
ALTER DATABASE OPEN RESETLOGS;
EOF
