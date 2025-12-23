create or replace trigger trg_dat_tour_after_insert_hd after
   insert on dattour
   for each row
declare
    -- Khai báo biến
   v_tong_tien_tour number(
      12,
      2
   );
begin
    -- 1. Lấy TONGTIEN từ bản ghi DatTour vừa chèn
   v_tong_tien_tour := :new.tongtien;

    -- 2. Tự động INSERT vào bảng HoaDon
   insert into hoadon (
      madattour,
      sotien,
      ngayxuat,
      trangthai
        -- Các cột khác (ChuKySo) có thể được cập nhật sau
   ) values ( :new.madattour,        -- Lấy MaDatTour vừa được tạo
              v_tong_tien_tour,      -- Lấy TongTien từ DatTour
              sysdate,               -- Ngày xuất là ngày hiện tại
              'Chưa thanh toán'      -- Thiết lập trạng thái ban đầu
               );

exception
   when others then
        -- Ghi log hoặc xử lý lỗi nếu cần
        -- Trong môi trường sản xuất, bạn nên ghi lại lỗi này vào bảng NhatKyHeThong.
      raise_application_error(
         -20001,
         'Lỗi khi tự động tạo hóa đơn: ' || sqlerrm
      );
end;
/


-- 1) Cập nhật trạng thái Tour theo ngày khởi hành
create or replace trigger tadmin.trg_tour_set_status before
   insert or update of thoigian on tadmin.tour
   for each row
declare
   v_today date := trunc(sysdate);
begin
   if :new.thoigian is not null then
      if trunc(:new.thoigian) > v_today then
         :new.trangthai := 'Hoạt động';
      elsif trunc(:new.thoigian) + 2 <= v_today then
         :new.trangthai := 'Hoàn thành';
      else
         :new.trangthai := 'Đang diễn ra';
      end if;
   end if;
end;
/
alter trigger tadmin.trg_tour_set_status enable;


-- 2) Khi Tour chuyển sang Hoàn thành thì đồng bộ DatTour -> Hoàn thành (trừ các đơn đã hủy)
create or replace trigger tadmin.trg_tour_sync_dattour after
   update of trangthai on tadmin.tour
   for each row
   when ( new.trangthai = 'Hoàn thành'
      and nvl(
      old.trangthai,
      '#'
   ) <> 'Hoàn thành' )
begin
   update tadmin.dattour d
      set
      d.trangthaidat = 'Hoàn thành'
    where d.matour = :new.matour
      and d.trangthaidat not in ( 'Hoàn thành',
                                  'Đã hủy' );
end;
/
alter trigger tadmin.trg_tour_sync_dattour enable;