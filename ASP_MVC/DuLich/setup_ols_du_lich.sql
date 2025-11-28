-- OLS setup cho DuLich (PDB: orclpdb1)
-- Chạy trong PDB với user LBACSYS (hoặc SYS có quyền LBAC_DBA).
-- Yêu cầu: đã grant INHERIT PRIVILEGES ON LBACSYS cho TADMIN (trong PDB).

-- 1) Dọn policy cũ (bỏ qua lỗi nếu chưa có)
begin
   sa_sysdba.drop_policy('DULICH_OLS');
exception
   when others then
      if sqlcode not in ( - 12444,
                          - 28116 ) then
         raise;
      end if;
end;
/

-- 2) Tạo policy và các thành phần
begin
   sa_sysdba.create_policy(
      'DULICH_OLS',
      'OLS_LABEL'
   );
   sa_components.create_level(
      'DULICH_OLS',
      1000,
      'PUB',
      'PUBLIC_DATA'
   );
   sa_components.create_level(
      'DULICH_OLS',
      2000,
      'INT',
      'INTERNAL_DATA'
   );
   sa_label_admin.create_label(
      'DULICH_OLS',
      1000,
      'PUB'
   );
   sa_label_admin.create_label(
      'DULICH_OLS',
      2000,
      'INT'
   );
end;
/

-- 3) Áp policy vào bảng TADMIN (OLS sẽ tự tạo cột ẩn OLS_LABEL)
begin
   sa_policy_admin.apply_table_policy(
      policy_name   => 'DULICH_OLS',
      schema_name   => 'TADMIN',
      table_name    => 'TOUR',
      table_options => 'READ_CONTROL, WRITE_CONTROL, CHECK_CONTROL'
   );

   sa_policy_admin.apply_table_policy(
      policy_name   => 'DULICH_OLS',
      schema_name   => 'TADMIN',
      table_name    => 'DATTOUR',
      table_options => 'READ_CONTROL, WRITE_CONTROL, CHECK_CONTROL'
   );

   sa_policy_admin.apply_table_policy(
      policy_name   => 'DULICH_OLS',
      schema_name   => 'TADMIN',
      table_name    => 'HOADON',
      table_options => 'READ_CONTROL, WRITE_CONTROL, CHECK_CONTROL'
   );
end;
/

-- 4) Cấp nhãn cho user/role (TADMIN/ROLE_ADMIN/ROLE_STAFF/ROLE_CUSTOMER)
begin
   sa_user_admin.set_user_labels(
      'DULICH_OLS',
      'TADMIN',
      'INT',
      'INT',
      'PUB',
      'INT',
      'INT'
   );
   sa_user_admin.set_user_labels(
      'DULICH_OLS',
      'ROLE_ADMIN',
      'INT',
      'INT',
      'PUB',
      'INT',
      'INT'
   );
   sa_user_admin.set_user_labels(
      'DULICH_OLS',
      'ROLE_STAFF',
      'INT',
      'INT',
      'PUB',
      'INT',
      'INT'
   );
   sa_user_admin.set_user_labels(
      'DULICH_OLS',
      'ROLE_CUSTOMER',
      'PUB',
      'PUB',
      'PUB',
      'PUB',
      'PUB'
   );
end;
/

-- 5) Gán nhãn PUB cho dữ liệu cũ (chạy LBACSYS, tắt policy tạm để tránh chặn)
begin
   sa_policy_admin.disable_table_policy(
      'DULICH_OLS',
      'TADMIN',
      'TOUR'
   );
   sa_policy_admin.disable_table_policy(
      'DULICH_OLS',
      'TADMIN',
      'DATTOUR'
   );
   sa_policy_admin.disable_table_policy(
      'DULICH_OLS',
      'TADMIN',
      'HOADON'
   );
end;
/

update tadmin.tour
   set
   ols_label = char_to_label(
      'DULICH_OLS',
      'PUB'
   )
 where ols_label is null;
update tadmin.dattour
   set
   ols_label = char_to_label(
      'DULICH_OLS',
      'PUB'
   )
 where ols_label is null;
update tadmin.hoadon
   set
   ols_label = char_to_label(
      'DULICH_OLS',
      'PUB'
   )
 where ols_label is null;

begin
   sa_policy_admin.enable_table_policy(
      'DULICH_OLS',
      'TADMIN',
      'TOUR'
   );
   sa_policy_admin.enable_table_policy(
      'DULICH_OLS',
      'TADMIN',
      'DATTOUR'
   );
   sa_policy_admin.enable_table_policy(
      'DULICH_OLS',
      'TADMIN',
      'HOADON'
   );
end;
/
commit;

-- 6) Kiểm tra nhãn mẫu (chạy với LBACSYS, đã set session label INT nếu cần)
-- SELECT SA_LABEL_ADMIN.LABEL_TO_CHAR('DULICH_OLS', OLS_LABEL) FROM TADMIN.TOUR FETCH FIRST 5 ROWS ONLY;