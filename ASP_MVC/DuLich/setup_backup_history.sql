-- Tạo bảng lưu lịch sử sao lưu/phục hồi
CREATE TABLE TADMIN.BACKUP_HISTORY (
    ID NUMBER(10) PRIMARY KEY,
    ACTION_TYPE VARCHAR2(100),
    REQUESTED_AT DATE,
    COMPLETED_AT DATE,
    STATUS VARCHAR2(50),
    TARGET VARCHAR2(255),
    NOTES VARCHAR2(500),
    REQUESTED_BY VARCHAR2(100)
);

-- Sequence + trigger tự tăng ID (nếu muốn dùng)
CREATE SEQUENCE TADMIN.BACKUP_HISTORY_SEQ START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE OR REPLACE TRIGGER TADMIN.BACKUP_HISTORY_BI
BEFORE INSERT ON TADMIN.BACKUP_HISTORY
FOR EACH ROW
WHEN (NEW.ID IS NULL)
BEGIN
    SELECT TADMIN.BACKUP_HISTORY_SEQ.NEXTVAL INTO :NEW.ID FROM dual;
END;
/

-- setup trong máy linux
sqlplus / as sysdba
shutdown immediate;
startup mount;
alter database archivelog;
alter database open;
sqlplus / as sysdba
alter system set db_recovery_file_dest_size=80G scope=both;
alter system set db_recovery_file_dest='/u01/app/oracle/fast_recovery_area' scope=both;
exit;
rman target /
CONFIGURE RETENTION POLICY TO REDUNDANCY 2;
CONFIGURE CONTROLFILE AUTOBACKUP ON;
CONFIGURE CHANNEL DEVICE TYPE DISK FORMAT '/u01/backup/%d_%T_%U.bkp';
CONFIGURE ARCHIVELOG DELETION POLICY TO BACKED UP 2 TIMES TO DISK;

exit;
-- thử backup
rman target /
BACKUP AS COMPRESSED BACKUPSET DATABASE PLUS ARCHIVELOG;

-- tạo 2 file 
sudo mkdir -p /u01/backup
nano /u01/backup/run_backup.sh
nano /u01/backup/restore_from_path.sh

sudo chown oracle:oinstall /u01/backup/run_backup.sh /u01/backup/restore_from_path.sh
sudo chmod +x /u01/backup/run_backup.sh /u01/backup/restore_from_path.sh
sudo chmod 775 /u01/backup

-- test
/u01/backup/run_backup.sh
/u01/backup/restore_from_path.sh /u01/backup/<file.bkp>
