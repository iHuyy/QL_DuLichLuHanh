-- Bước 1: Tạo Context và Package để quản lý MaChiNhanh

-- Tạo một context để lưu trữ mã chi nhánh của người dùng
CREATE OR REPLACE CONTEXT tour_management_ctx USING TADMIN.pkg_tour_management;
/

-- Tạo package để set giá trị cho context
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

-- Bước 2: Tạo hàm chính sách (Policy Function)

-- Hàm này sẽ được Oracle tự động gọi mỗi khi có truy vấn đến bảng TOUR.
-- Nó sẽ trả về một mệnh đề WHERE để lọc dữ liệu dựa trên mã chi nhánh trong context.
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

-- Bước 3: Gán chính sách VPD vào bảng TOUR (cách tiếp cận mới)

-- Thử nghiệm bằng cách thêm tham số update_check và để statement_types mặc định
BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => 'TADMIN',
    object_name     => 'TOUR',
    policy_name     => 'tour_branch_policy',
    function_schema => 'TADMIN',
    policy_function => 'fn_vpd_tour_security',
    update_check    => TRUE
  );
END;
/
-- Bước 4: Tạo lại Trigger để tự động gán chi nhánh khi tạo tour

CREATE OR REPLACE TRIGGER TADMIN.trg_tour_branch_autofill
BEFORE INSERT ON TADMIN.TOUR
FOR EACH ROW
DECLARE
  v_branch_id NUMBER;
BEGIN
  -- Lấy mã chi nhánh từ context mà ứng dụng đã set
  v_branch_id := TO_NUMBER(SYS_CONTEXT('tour_management_ctx', 'branch_id'));

  -- Nếu mã chi nhánh tồn tại trong context, gán nó cho tour mới
  IF v_branch_id IS NOT NULL THEN
    :NEW.MaChiNhanh := v_branch_id;
  END IF;
EXCEPTION
  -- Bỏ qua lỗi nếu context không được set hoặc không phải là số
  WHEN OTHERS THEN
    NULL;
END;
/

-- 2. Policy for DATTOUR (bookings)
CREATE OR REPLACE FUNCTION TADMIN.fn_vpd_dattour(
  schema_name IN VARCHAR2,
  table_name  IN VARCHAR2
) RETURN VARCHAR2 AS
  v_role      VARCHAR2(50);
  v_branch_id VARCHAR2(50);
BEGIN
  v_role      := SYS_CONTEXT('tour_management_ctx', 'role');
  v_branch_id := SYS_CONTEXT('tour_management_ctx', 'branch_id');

  IF v_role = 'ROLE_ADMIN' OR v_role = 'ROLE_CUSTOMER' THEN
    RETURN '1=1';
  ELSIF v_role = 'ROLE_STAFF' AND v_branch_id IS NOT NULL THEN
    RETURN 'EXISTS (SELECT 1 '
        || 'FROM TADMIN.TOUR t '
        || 'WHERE t.MaTour = DATTOUR.MaTour '
        || 'AND t.MaChiNhanh = ' || v_branch_id || ')';
  ELSE
    RETURN '1=0';
  END IF;
END;
/

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => 'TADMIN',
    object_name     => 'DATTOUR',
    policy_name     => 'dattour_branch_policy',
    function_schema => 'TADMIN',
    policy_function => 'fn_vpd_dattour',
    statement_types => 'SELECT,INSERT,UPDATE,DELETE',
    update_check    => TRUE
  );
END;
/

-- 3. Policy for HOADON (signed invoices)
CREATE OR REPLACE FUNCTION TADMIN.fn_vpd_hoadon(
  schema_name IN VARCHAR2,
  table_name  IN VARCHAR2
) RETURN VARCHAR2 AS
  v_role      VARCHAR2(50);
  v_branch_id VARCHAR2(50);
BEGIN
  v_role      := SYS_CONTEXT('tour_management_ctx', 'role');
  v_branch_id := SYS_CONTEXT('tour_management_ctx', 'branch_id');

  IF v_role = 'ROLE_ADMIN' OR v_role = 'ROLE_CUSTOMER' THEN
    RETURN '1=1';
  ELSIF v_role = 'ROLE_STAFF' AND v_branch_id IS NOT NULL THEN
    RETURN 'EXISTS (SELECT 1 '
        || 'FROM TADMIN.DATTOUR dt '
        || 'JOIN TADMIN.TOUR t ON t.MaTour = dt.MaTour '
        || 'WHERE dt.MaDatTour = HOADON.MaDatTour '
        || 'AND t.MaChiNhanh = ' || v_branch_id || ')';
  ELSE
    RETURN '1=0';
  END IF;
END;
/

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => 'TADMIN',
    object_name     => 'HOADON',
    policy_name     => 'hoadon_branch_policy',
    function_schema => 'TADMIN',
    policy_function => 'fn_vpd_hoadon',
    statement_types => 'SELECT,INSERT,UPDATE,DELETE',
    update_check    => TRUE
  );
END;
/
