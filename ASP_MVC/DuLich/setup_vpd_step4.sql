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
