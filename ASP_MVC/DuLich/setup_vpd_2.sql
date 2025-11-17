-- VPD for tour booking and signed invoices by branch
-- Rules:
--   - Admin sees everything.
--   - Staff only sees bookings/invoices of tours within their branch.

-- 1. Context + package to set role/branch into session context
CREATE OR REPLACE CONTEXT tour_management_ctx USING TADMIN.pkg_tour_management;
/

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

-- 4. Example to set context from application:
--    BEGIN
--      TADMIN.pkg_tour_management.set_user_context('ROLE_STAFF', :branchId);
--    END;
--    BEGIN
--      TADMIN.pkg_tour_management.set_user_context('ROLE_ADMIN', NULL);
--    END;
