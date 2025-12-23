-- Tab Tour
-- Trigger audit cho Tour
create or replace trigger tadmin.trg_aud_tour after
   insert or update or delete on tadmin.tour
   for each row
declare
   v_action    nvarchar2(20);
   v_record_id nvarchar2(100);
   v_old       clob := '{}';
   v_new       clob := '{}';
   jo_old      json_object_t := json_object_t();
   jo_new      json_object_t := json_object_t();
begin
   v_action :=
      case
         when inserting then
            'INSERT'
         when updating then
            'UPDATE'
         else
            'DELETE'
      end;
   v_record_id :=
      case
         when deleting then
            to_char(:old.matour)
         else
            to_char(:new.matour)
      end;

   if inserting then
      select to_clob(
         json_object(
            'MATOUR' value :new.matour,
                     'TIEUDE' value :new.tieude,
                     'GIANGUOILON' value :new.gianguoilon,
                     'GIATREEM' value :new.giatreem,
                     'TRANGTHAI' value :new.trangthai,
                     'SOLUONG' value :new.soluong,
                     'MACHINHANH' value :new.machinhanh
         )
      )
        into v_new
        from dual;
   elsif deleting then
      select to_clob(
         json_object(
            'MATOUR' value :old.matour,
                     'TIEUDE' value :old.tieude,
                     'GIANGUOILON' value :old.gianguoilon,
                     'GIATREEM' value :old.giatreem,
                     'TRANGTHAI' value :old.trangthai,
                     'SOLUONG' value :old.soluong,
                     'MACHINHANH' value :old.machinhanh
         )
      )
        into v_old
        from dual;
   else
      if ( :old.tieude <> :new.tieude
      or (
         :old.tieude is null
         and :new.tieude is not null
      )
      or (
         :old.tieude is not null
         and :new.tieude is null
      ) ) then
         jo_old.put(
            'TIEUDE',
            :old.tieude
         );
         jo_new.put(
            'TIEUDE',
            :new.tieude
         );
      end if;
      if ( :old.gianguoilon <> :new.gianguoilon
      or (
         :old.gianguoilon is null
         and :new.gianguoilon is not null
      )
      or (
         :old.gianguoilon is not null
         and :new.gianguoilon is null
      ) ) then
         jo_old.put(
            'GIANGUOILON',
            :old.gianguoilon
         );
         jo_new.put(
            'GIANGUOILON',
            :new.gianguoilon
         );
      end if;
      if ( :old.giatreem <> :new.giatreem
      or (
         :old.giatreem is null
         and :new.giatreem is not null
      )
      or (
         :old.giatreem is not null
         and :new.giatreem is null
      ) ) then
         jo_old.put(
            'GIATREEM',
            :old.giatreem
         );
         jo_new.put(
            'GIATREEM',
            :new.giatreem
         );
      end if;
      if ( :old.trangthai <> :new.trangthai
      or (
         :old.trangthai is null
         and :new.trangthai is not null
      )
      or (
         :old.trangthai is not null
         and :new.trangthai is null
      ) ) then
         jo_old.put(
            'TRANGTHAI',
            :old.trangthai
         );
         jo_new.put(
            'TRANGTHAI',
            :new.trangthai
         );
      end if;
      if ( :old.soluong <> :new.soluong
      or (
         :old.soluong is null
         and :new.soluong is not null
      )
      or (
         :old.soluong is not null
         and :new.soluong is null
      ) ) then
         jo_old.put(
            'SOLUONG',
            :old.soluong
         );
         jo_new.put(
            'SOLUONG',
            :new.soluong
         );
      end if;
      if ( :old.machinhanh <> :new.machinhanh
      or (
         :old.machinhanh is null
         and :new.machinhanh is not null
      )
      or (
         :old.machinhanh is not null
         and :new.machinhanh is null
      ) ) then
         jo_old.put(
            'MACHINHANH',
            :old.machinhanh
         );
         jo_new.put(
            'MACHINHANH',
            :new.machinhanh
         );
      end if;

      if jo_old.get_size = 0 then
         return;
      end if;
      v_old := jo_old.to_clob;
      v_new := jo_new.to_clob;
   end if;

   insert into tadmin.audit_log_detail (
      tablename,
      action,
      recordid,
      oldvalues,
      newvalues
   ) values ( 'TOUR',
              v_action,
              v_record_id,
              v_old,
              v_new );
end;
/

-- Trigger audit cho DatTour
create or replace trigger tadmin.trg_aud_dattour after
   insert or update or delete on tadmin.dattour
   for each row
declare
   v_action    nvarchar2(20);
   v_record_id nvarchar2(100);
   v_old       clob := '{}';
   v_new       clob := '{}';
begin
   v_action :=
      case
         when inserting then
            'INSERT'
         when updating then
            'UPDATE'
         else
            'DELETE'
      end;
   v_record_id :=
      case
         when deleting then
            to_char(:old.madattour)
         else
            to_char(:new.madattour)
      end;

   if inserting then
      select to_clob(
         json_object(
            'MADATTOUR' value :new.madattour,
                     'MAKHACHHANG' value :new.makhachhang,
                     'MATOUR' value :new.matour,
                     'SONGUOILON' value :new.songuoilon,
                     'SOTREEM' value :new.sotreem,
                     'TONGTIEN' value :new.tongtien,
                     'TRANGTHAITHANHTOAN' value :new.trangthaithanhtoan,
                     'TRANGTHAIDAT' value :new.trangthaidat
         )
      )
        into v_new
        from dual;
   elsif deleting then
      select to_clob(
         json_object(
            'MADATTOUR' value :old.madattour,
                     'MAKHACHHANG' value :old.makhachhang,
                     'MATOUR' value :old.matour,
                     'SONGUOILON' value :old.songuoilon,
                     'SOTREEM' value :old.sotreem,
                     'TONGTIEN' value :old.tongtien,
                     'TRANGTHAITHANHTOAN' value :old.trangthaithanhtoan,
                     'TRANGTHAIDAT' value :old.trangthaidat
         )
      )
        into v_old
        from dual;
   else
      select to_clob(
         json_object(
            'MADATTOUR' value :old.madattour,
                     'MAKHACHHANG' value :old.makhachhang,
                     'MATOUR' value :old.matour,
                     'SONGUOILON' value :old.songuoilon,
                     'SOTREEM' value :old.sotreem,
                     'TONGTIEN' value :old.tongtien,
                     'TRANGTHAITHANHTOAN' value :old.trangthaithanhtoan,
                     'TRANGTHAIDAT' value :old.trangthaidat
         )
      )
        into v_old
        from dual;
      select to_clob(
         json_object(
            'MADATTOUR' value :new.madattour,
                     'MAKHACHHANG' value :new.makhachhang,
                     'MATOUR' value :new.matour,
                     'SONGUOILON' value :new.songuoilon,
                     'SOTREEM' value :new.sotreem,
                     'TONGTIEN' value :new.tongtien,
                     'TRANGTHAITHANHTOAN' value :new.trangthaithanhtoan,
                     'TRANGTHAIDAT' value :new.trangthaidat
         )
      )
        into v_new
        from dual;
   end if;

   insert into tadmin.audit_log_detail (
      tablename,
      action,
      recordid,
      oldvalues,
      newvalues
   ) values ( 'DATTOUR',
              v_action,
              v_record_id,
              v_old,
              v_new );
end;
/

-- Trigger audit cho HoaDon
create or replace trigger tadmin.trg_aud_hoadon after
   insert or update or delete on tadmin.hoadon
   for each row
declare
   v_action    nvarchar2(20);
   v_record_id nvarchar2(100);
   v_old       clob := '{}';
   v_new       clob := '{}';
begin
   v_action :=
      case
         when inserting then
            'INSERT'
         when updating then
            'UPDATE'
         else
            'DELETE'
      end;
   v_record_id :=
      case
         when deleting then
            to_char(:old.mahoadon)
         else
            to_char(:new.mahoadon)
      end;

   if inserting then
      select to_clob(
         json_object(
            'MAHOADON' value :new.mahoadon,
                     'MADATTOUR' value :new.madattour,
                     'SOTIEN' value :new.sotien,
                     'TRANGTHAI' value :new.trangthai
         )
      )
        into v_new
        from dual;
   elsif deleting then
      select to_clob(
         json_object(
            'MAHOADON' value :old.mahoadon,
                     'MADATTOUR' value :old.madattour,
                     'SOTIEN' value :old.sotien,
                     'TRANGTHAI' value :old.trangthai
         )
      )
        into v_old
        from dual;
   else
      select to_clob(
         json_object(
            'MAHOADON' value :old.mahoadon,
                     'MADATTOUR' value :old.madattour,
                     'SOTIEN' value :old.sotien,
                     'TRANGTHAI' value :old.trangthai
         )
      )
        into v_old
        from dual;
      select to_clob(
         json_object(
            'MAHOADON' value :new.mahoadon,
                     'MADATTOUR' value :new.madattour,
                     'SOTIEN' value :new.sotien,
                     'TRANGTHAI' value :new.trangthai
         )
      )
        into v_new
        from dual;
   end if;

   insert into tadmin.audit_log_detail (
      tablename,
      action,
      recordid,
      oldvalues,
      newvalues
   ) values ( 'HOADON',
              v_action,
              v_record_id,
              v_old,
              v_new );
end;
/

-- Trigger audit cho ChiTietTour 
declare
   v_count integer;
begin
   select count(*)
     into v_count
     from dba_tables
    where owner = 'TADMIN'
      and table_name = 'CHITIETTOUR';

   if v_count > 0 then
      execute immediate q'[
CREATE OR REPLACE TRIGGER TADMIN.TRG_AUD_CHITIETTOUR
AFTER INSERT OR UPDATE OR DELETE ON TADMIN.CHITIETTOUR
FOR EACH ROW
DECLARE
    v_action NVARCHAR2(20);
    v_record_id NVARCHAR2(100);
    v_old CLOB := '{}';
    v_new CLOB := '{}';
BEGIN
    v_action := CASE WHEN INSERTING THEN 'INSERT' WHEN UPDATING THEN 'UPDATE' ELSE 'DELETE' END;
    v_record_id := CASE WHEN DELETING THEN TO_CHAR(:OLD.MATOUR) || '-' || TO_CHAR(:OLD.MADICHVU)
                        ELSE TO_CHAR(:NEW.MATOUR) || '-' || TO_CHAR(:NEW.MADICHVU) END;

    IF INSERTING THEN
        SELECT TO_CLOB(JSON_OBJECT(
            'MATOUR' VALUE :NEW.MATOUR, 'MADICHVU' VALUE :NEW.MADICHVU,
            'SOLUONG' VALUE :NEW.SOLUONG
        )) INTO v_new FROM dual;
    ELSIF DELETING THEN
        SELECT TO_CLOB(JSON_OBJECT(
            'MATOUR' VALUE :OLD.MATOUR, 'MADICHVU' VALUE :OLD.MADICHVU,
            'SOLUONG' VALUE :OLD.SOLUONG
        )) INTO v_old FROM dual;
    ELSE
        SELECT TO_CLOB(JSON_OBJECT(
            'MATOUR' VALUE :OLD.MATOUR, 'MADICHVU' VALUE :OLD.MADICHVU,
            'SOLUONG' VALUE :OLD.SOLUONG
        )) INTO v_old FROM dual;
        SELECT TO_CLOB(JSON_OBJECT(
            'MATOUR' VALUE :NEW.MATOUR, 'MADICHVU' VALUE :NEW.MADICHVU,
            'SOLUONG' VALUE :NEW.SOLUONG
        )) INTO v_new FROM dual;
    END IF;

    INSERT INTO TADMIN.AUDIT_LOG_DETAIL (TableName, Action, RecordId, OldValues, NewValues)
    VALUES ('CHITIETTOUR', v_action, v_record_id, v_old, v_new);
END;
]';
   end if;
end;
/

PROMPT ===== Section 2: Standard audit cho tab Nhan vien =====
audit insert,update,delete on tadmin.nhanvien by access;

PROMPT ===== Section 3: Audit tong CSDL (chi dung FGA + DDL unified) =====
audit table by tadmin by access;
-- FGA cho cac bang chinh de lay SQL text (phuc vu tab CSDL)
begin
   dbms_fga.drop_policy(
      object_schema => 'TADMIN',
      object_name   => 'HOADON',
      policy_name   => 'FGA_HOADON_SQL'
   );
exception
   when others then
      if sqlcode != -28102 then
         raise;
      end if;
end;
/
begin
   dbms_fga.add_policy(
      object_schema   => 'TADMIN',
      object_name     => 'HOADON',
      policy_name     => 'FGA_HOADON_SQL',
      statement_types => 'INSERT,UPDATE,DELETE',
      audit_trail     => dbms_fga.db_extended
   );
end;
/
begin
   dbms_fga.drop_policy(
      object_schema => 'TADMIN',
      object_name   => 'TOUR',
      policy_name   => 'FGA_TOUR_SQL'
   );
exception
   when others then
      if sqlcode != -28102 then
         raise;
      end if;
end;
/
begin
   dbms_fga.add_policy(
      object_schema   => 'TADMIN',
      object_name     => 'TOUR',
      policy_name     => 'FGA_TOUR_SQL',
      statement_types => 'INSERT,UPDATE,DELETE',
      audit_trail     => dbms_fga.db_extended
   );
end;
/
begin
   dbms_fga.drop_policy(
      object_schema => 'TADMIN',
      object_name   => 'DATTOUR',
      policy_name   => 'FGA_DATTOUR_SQL'
   );
exception
   when others then
      if sqlcode != -28102 then
         raise;
      end if;
end;
/
begin
   dbms_fga.add_policy(
      object_schema   => 'TADMIN',
      object_name     => 'DATTOUR',
      policy_name     => 'FGA_DATTOUR_SQL',
      statement_types => 'INSERT,UPDATE,DELETE',
      audit_trail     => dbms_fga.db_extended
   );
end;
/
begin
   dbms_fga.drop_policy(
      object_schema => 'TADMIN',
      object_name   => 'CHITIETTOUR',
      policy_name   => 'FGA_CHITIETTOUR_SQL'
   );
exception
   when others then
      if sqlcode != -28102 then
         raise;
      end if;
end;
/
begin
   dbms_fga.add_policy(
      object_schema   => 'TADMIN',
      object_name     => 'CHITIETTOUR',
      policy_name     => 'FGA_CHITIETTOUR_SQL',
      statement_types => 'INSERT,UPDATE,DELETE',
      audit_trail     => dbms_fga.db_extended
   );
end;
/

PROMPT ===== Hoan thanh setup audit theo 3 tab =====