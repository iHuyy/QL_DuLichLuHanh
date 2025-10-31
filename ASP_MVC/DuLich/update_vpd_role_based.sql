-- Cập nhật package và hàm chính sách để xử lý theo vai trò

-- Cập nhật package để có thể set cả vai trò và chi nhánh
CREATE OR REPLACE PACKAGE TADMIN.pkg_tour_management AS
  PROCEDURE set_user_context(role_name IN VARCHAR2, branch_id IN NUMBER);
END pkg_tour_management;
/

CREATE OR REPLACE PACKAGE BODY TADMIN.pkg_tour_management AS
  PROCEDURE set_user_context(role_name IN VARCHAR2, branch_id IN NUMBER) IS
  BEGIN
    DBMS_SESSION.SET_CONTEXT('tour_management_ctx', 'role', role_name);
    DBMS_SESSION.SET_CONTEXT('tour_management_ctx', 'branch_id', branch_id);
  END set_user_context;
END pkg_tour_management;
/

-- Cập nhật hàm chính sách để kiểm tra vai trò
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

  -- Admin và Customer có thể thấy tất cả tour
  IF v_role = 'ROLE_ADMIN' OR v_role = 'ROLE_CUSTOMER' THEN
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
