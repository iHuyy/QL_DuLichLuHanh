-- Cập nhật hàm chính sách VPD để cho phép người dùng chưa đăng nhập xem tour

CREATE OR REPLACE FUNCTION TADMIN.fn_vpd_tour_security(
  schema_name IN VARCHAR2,
  table_name IN VARCHAR2
)
RETURN VARCHAR2
AS
  v_role VARCHAR2(100);
  v_branch_id VARCHAR2(100);
BEGIN
  v_role := SYS_CONTEXT('tour_management_ctx', 'role');
  v_branch_id := SYS_CONTEXT('tour_management_ctx', 'branch_id');

  -- Admin, Customer và người dùng chưa đăng nhập có thể thấy tất cả tour
  IF v_role = 'ROLE_ADMIN' OR v_role = 'ROLE_CUSTOMER' OR v_role IS NULL THEN
    RETURN '1=1'; 
  -- Nhân viên chỉ thấy tour thuộc chi nhánh của họ
  ELSIF v_role = 'ROLE_STAFF' AND v_branch_id IS NOT NULL THEN
    RETURN 'MaChiNhanh = ' || v_branch_id;
  -- Nếu không có vai trò hoặc là vai trò khác, không thấy gì cả
  ELSE
    RETURN '1=0';
  END IF;
END;
/
