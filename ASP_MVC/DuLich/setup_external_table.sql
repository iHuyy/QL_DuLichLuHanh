-- QUAN TRỌNG: Các lệnh này phải được chạy bởi người dùng có quyền cao như SYS hoặc SYSTEM.
-- File này tạo ra một bảng ngoài (external table) để đọc danh sách các file sao lưu từ thư mục trên Linux.

-- Bước 1: Tạo một đối tượng thư mục trỏ đến nơi chứa script list_backups.sh
-- Hãy chắc chắn rằng đường dẫn '/home/oracle/scripts/' là chính xác.
CREATE OR REPLACE DIRECTORY exec_dir AS '/home/oracle/scripts/';

-- Cấp quyền thực thi trên thư mục đó cho TADMIN
GRANT EXECUTE ON DIRECTORY exec_dir TO TADMIN;
/

-- Bước 2: Tạo bảng ngoài (external table)
-- Bảng này sẽ chạy script 'list_backups.sh' mỗi khi được truy vấn.
CREATE TABLE TADMIN.BACKUP_FILES_EXTERNAL (
  FILENAME VARCHAR2(255)
)
ORGANIZATION EXTERNAL (
  TYPE ORACLE_LOADER
  DEFAULT DIRECTORY exec_dir -- Thư mục mặc định, không thực sự được dùng khi có PREPROCESSOR
  ACCESS PARAMETERS (
    RECORDS DELIMITED BY NEWLINE
    PREPROCESSOR exec_dir:'list_backups.sh'
    FIELDS TERMINATED BY ',"' OPTIONALLY ENCLOSED BY '"'
    (
      FILENAME CHAR(255)
    )
  )
  LOCATION ('list_backups.sh') -- Tên file script sẽ chạy
)
REJECT LIMIT UNLIMITED;
/

-- Bước 3: Cấp quyền cho TADMIN để truy vấn bảng này
GRANT SELECT ON TADMIN.BACKUP_FILES_EXTERNAL TO TADMIN;
/
