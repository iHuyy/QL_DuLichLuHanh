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
