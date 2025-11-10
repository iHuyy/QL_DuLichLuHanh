-- QUAN TRỌNG: Vui lòng đảm bảo thư mục 'D:\OracleBackups' tồn tại trên máy chủ cơ sở dữ liệu Oracle
-- và tiến trình Oracle có quyền đọc/ghi vào nó.
-- Bạn có thể thay đổi đường dẫn này nếu cần.

-- Bước 1: Tạo một đối tượng thư mục trong Oracle để chứa các bản sao lưu
-- Lệnh 1: Sửa lại đường dẫn cho đúng với máy chủ Linux
CREATE OR REPLACE DIRECTORY backup_dir AS '/home/oracle/backups';

-- Bước 2: Cấp các quyền cần thiết cho người dùng TADMIN
-- Lưu ý: Các lệnh cấp quyền này cần được thực thi bởi người dùng có quyền SYSDBA hoặc tương đương.
GRANT READ, WRITE ON DIRECTORY backup_dir TO TADMIN;
GRANT DATAPUMP_EXP_FULL_DATABASE TO TADMIN;
GRANT DATAPUMP_IMP_FULL_DATABASE TO TADMIN;
GRANT CREATE ANY TABLE TO TADMIN;
/

-- Bước 3: Thủ tục (Stored Procedure) cho Sao lưu Thủ công và Tự động
CREATE OR REPLACE PROCEDURE TADMIN.SP_TAO_BAN_SAO_LUU AS
  h1   NUMBER;
  l_job_name VARCHAR2(100);
  l_dump_file VARCHAR2(100);
  l_log_file VARCHAR2(100);
  l_timestamp VARCHAR2(20);
BEGIN
  -- Tạo tên duy nhất cho job và các file bằng cách sử dụng timestamp
  l_timestamp := TO_CHAR(SYSTIMESTAMP, 'YYYYMMDD_HH24MISS');
  l_job_name := 'EXPORT_TADMIN_' || l_timestamp;
  l_dump_file := 'TADMIN_BACKUP_' || l_timestamp || '.dmp';
  l_log_file := 'TADMIN_BACKUP_EXP_' || l_timestamp || '.log';

  -- Mở một job Data Pump export mới
  h1 := DBMS_DATAPUMP.OPEN(
    operation   => 'EXPORT',
    job_mode    => 'SCHEMA',
    job_name    => l_job_name,
    version     => 'LATEST'
  );

  -- Chỉ định file dump và file log
  DBMS_DATAPUMP.ADD_FILE(
    handle    => h1,
    filename  => l_dump_file,
    directory => 'BACKUP_DIR',
    filetype  => DBMS_DATAPUMP.KU$_FILE_TYPE_DUMP_FILE
  );

  DBMS_DATAPUMP.ADD_FILE(
    handle    => h1,
    filename  => l_log_file,
    directory => 'BACKUP_DIR',
    filetype  => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE
  );

  -- Chỉ định rằng chúng ta đang export schema 'TADMIN'
  DBMS_DATAPUMP.METADATA_FILTER(
    handle => h1,
    name   => 'SCHEMA_EXPR',
    value  => 'IN (''TADMIN'')'
  );

  -- Bắt đầu job
  DBMS_DATAPUMP.START_JOB(handle => h1);

  -- Tách khỏi job, cho phép nó chạy ngầm
  DBMS_DATAPUMP.DETACH(handle => h1);
END SP_TAO_BAN_SAO_LUU;
/

-- Bước 4: Thủ tục (Stored Procedure) để Phục hồi từ một bản sao lưu
CREATE OR REPLACE PROCEDURE TADMIN.SP_PHUC_HOI_TU_BAN_SAO_LUU (
   p_dump_file IN VARCHAR2
) AS
  h1   NUMBER;
  l_job_name VARCHAR2(100);
  l_log_file VARCHAR2(100);
  l_timestamp VARCHAR2(20);
BEGIN
  -- Tạo tên duy nhất cho job và file log
  l_timestamp := TO_CHAR(SYSTIMESTAMP, 'YYYYMMDD_HH24MISS');
  l_job_name := 'IMPORT_TADMIN_' || l_timestamp;
  l_log_file := 'TADMIN_BACKUP_IMP_' || l_timestamp || '.log';

  -- Mở một job Data Pump import mới
  h1 := DBMS_DATAPUMP.OPEN(
    operation   => 'IMPORT',
    job_mode    => 'SCHEMA',
    job_name    => l_job_name,
    version     => 'LATEST'
  );

  -- Chỉ định file dump và file log
  DBMS_DATAPUMP.ADD_FILE(
    handle    => h1,
    filename  => p_dump_file,
    directory => 'BACKUP_DIR',
    filetype  => DBMS_DATAPUMP.KU$_FILE_TYPE_DUMP_FILE
  );

  DBMS_DATAPUMP.ADD_FILE(
    handle    => h1,
    filename  => l_log_file,
    directory => 'BACKUP_DIR',
    filetype  => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE
  );

  -- Chỉ định rằng chúng ta đang import vào schema 'TADMIN'
  DBMS_DATAPUMP.METADATA_FILTER(
    handle => h1,
    name   => 'SCHEMA_EXPR',
    value  => 'IN (''TADMIN'')'
  );
  
  -- Ánh xạ lại schema nếu file dump được tạo từ một người dùng khác (tùy chọn nhưng là một thói quen tốt)
  DBMS_DATAPUMP.METADATA_REMAP(
    handle    => h1,
    name      => 'REMAP_SCHEMA',
    old_value => 'TADMIN',
    value     => 'TADMIN'
  );

  -- Nếu các bảng đã tồn tại, hãy xóa chúng và tạo lại từ file dump.
  DBMS_DATAPUMP.SET_PARAMETER(
    handle => h1,
    name   => 'TABLE_EXISTS_ACTION',
    value  => 'REPLACE'
  );

  -- Bắt đầu job
  DBMS_DATAPUMP.START_JOB(handle => h1);

  -- Tách khỏi job
  DBMS_DATAPUMP.DETACH(handle => h1);
END SP_PHUC_HOI_TU_BAN_SAO_LUU;
/

-- Bước 5: Job Sao lưu Tự động sử dụng DBMS_SCHEDULER
-- Job này sẽ chạy thủ tục sao lưu mỗi ngày vào lúc 2 giờ sáng.
BEGIN
  DBMS_SCHEDULER.CREATE_JOB (
    job_name        => 'TADMIN_NIGHTLY_BACKUP_JOB',
    job_type        => 'STORED_PROCEDURE',
    job_action      => 'TADMIN.SP_TAO_BAN_SAO_LUU',
    start_date      => SYSTIMESTAMP,
    repeat_interval => 'FREQ=DAILY; BYHOUR=2; BYMINUTE=0; BYSECOND=0',
    enabled         => TRUE,
    comments        => 'Thực hiện sao lưu schema TADMIN hàng đêm.'
  );
END;
/
