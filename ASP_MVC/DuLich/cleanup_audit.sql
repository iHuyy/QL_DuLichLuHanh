-- =================================================================================
-- FILE: cleanup_audit.sql
-- DESCRIPTION: Script to remove all previous audit configurations.
-- SCHEMA: TADMIN
-- =================================================================================
-- Ghi chú: Vui lòng chạy script này với user có đủ quyền hạn (ví dụ: SYS as SYSDBA)
-- để dọn dẹp các cấu hình audit cũ.

-- =================================================================================
-- PHẦN 1: VÔ HIỆU HÓA VÀ XÓA CÁC POLICY AUDIT
-- =================================================================================

-- Vô hiệu hóa Standard Auditing
NOAUDIT ALL ON TADMIN.HoaDon;
NOAUDIT ALL ON TADMIN.DatTour;
NOAUDIT ALL ON TADMIN.NhanVien;
NOAUDIT ALL ON TADMIN.Tour;

-- Xóa các FGA Policy (nếu tồn tại)
BEGIN
    DBMS_FGA.DROP_POLICY(object_schema => 'TADMIN', object_name => 'HoaDon', policy_name => 'FGA_HOADON_POLICY');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -28102 THEN NULL; -- Policy không tồn tại, bỏ qua
        ELSE RAISE;
        END IF;
END;
/

BEGIN
    DBMS_FGA.DROP_POLICY(object_schema => 'TADMIN', object_name => 'DatTour', policy_name => 'FGA_DATTUOR_POLICY');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -28102 THEN NULL; -- Policy không tồn tại, bỏ qua
        ELSE RAISE;
        END IF;
END;
/

BEGIN
    DBMS_FGA.DROP_POLICY(object_schema => 'TADMIN', object_name => 'NhanVien', policy_name => 'FGA_NHANVIEN_POLICY');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -28102 THEN NULL; -- Policy không tồn tại, bỏ qua
        ELSE RAISE;
        END IF;
END;
/

BEGIN
    DBMS_FGA.DROP_POLICY(object_schema => 'TADMIN', object_name => 'Tour', policy_name => 'FGA_TOUR_POLICY');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -28102 THEN NULL; -- Policy không tồn tại, bỏ qua
        ELSE RAISE;
        END IF;
END;
/

-- =================================================================================
-- PHẦN 2: XÓA CÁC ĐỐI TƯỢNG DATABASE CỦA TRIGGER-BASED AUDITING
-- =================================================================================

-- Xóa các trigger cũ
BEGIN
   FOR t IN (SELECT trigger_name FROM all_triggers WHERE owner = 'TADMIN' AND trigger_name IN ('TRG_AUDIT_HOADON', 'TRG_AUDIT_NHANVIEN', 'TRG_AUDIT_TOUR', 'TRG_AUDIT_DATTOUR')) LOOP
      EXECUTE IMMEDIATE 'DROP TRIGGER TADMIN.' || t.trigger_name;
   END LOOP;
END;
/

-- Xóa procedure cũ
DROP PROCEDURE TADMIN.GET_AUDIT_LOGS;

-- Xóa bảng log cũ
DROP TABLE TADMIN.AUDIT_LOG_DETAIL;

-- Dọn dẹp audit trail (tùy chọn, cẩn thận khi chạy trên production)
-- BEGIN
--   DBMS_AUDIT_MGMT.CLEAN_AUDIT_TRAIL(
--    AUDIT_TRAIL_TYPE => DBMS_AUDIT_MGMT.AUDIT_TRAIL_UNIFIED,
--    USE_LAST_ARCH_TIMESTAMP => FALSE
--   );
-- END;
-- /

PROMPT Cleanup script finished.

