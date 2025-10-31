-- Bước 1: Tạo Context và Package để quản lý MaChiNhanh

-- Tạo một context để lưu trữ mã chi nhánh của người dùng
CREATE OR REPLACE CONTEXT tour_management_ctx USING TADMIN.pkg_tour_management;
/

-- Tạo package để set giá trị cho context
CREATE OR REPLACE PACKAGE TADMIN.pkg_tour_management AS
  -- Thủ tục này sẽ được gọi từ ứng dụng ASP.NET để gán mã chi nhánh
  PROCEDURE set_branch_id(branch_id IN NUMBER);
END pkg_tour_management;
/

CREATE OR REPLACE PACKAGE BODY TADMIN.pkg_tour_management AS
  PROCEDURE set_branch_id(branch_id IN NUMBER) IS
  BEGIN
    -- Gán giá trị branch_id vào context 'tour_management_ctx'
    DBMS_SESSION.SET_CONTEXT('tour_management_ctx', 'branch_id', branch_id);
  END set_branch_id;
END pkg_tour_management;
/
