-- Bước 2: Tạo hàm chính sách (Policy Function)

-- Hàm này sẽ được Oracle tự động gọi mỗi khi có truy vấn đến bảng TOUR.
-- Nó sẽ trả về một mệnh đề WHERE để lọc dữ liệu dựa trên mã chi nhánh trong context.
CREATE OR REPLACE FUNCTION TADMIN.fn_vpd_tour_security(
  schema_name IN VARCHAR2,
  table_name IN VARCHAR2
)
RETURN VARCHAR2
AS
  v_branch_id VARCHAR2(100);
BEGIN
  -- Lấy mã chi nhánh từ context mà ứng dụng đã set
  v_branch_id := SYS_CONTEXT('tour_management_ctx', 'branch_id');

  -- Nếu người dùng là ADMIN hoặc một vai trò không phải nhân viên,
  -- context sẽ không được set, và họ sẽ thấy tất cả các tour.
  IF v_branch_id IS NULL THEN
    RETURN '1=1'; -- Trả về điều kiện luôn đúng (thấy tất cả)
  END IF;

  -- Nếu là nhân viên, chỉ thấy các tour thuộc chi nhánh của họ.
  RETURN 'MaChiNhanh = ' || v_branch_id;
END;
/
