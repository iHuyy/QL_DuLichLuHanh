CREATE OR REPLACE TRIGGER trg_dat_tour_after_insert_hd
AFTER INSERT ON DatTour
FOR EACH ROW
DECLARE
    -- Khai báo biến
    v_tong_tien_tour NUMBER(12, 2);
BEGIN
    -- 1. Lấy TONGTIEN từ bản ghi DatTour vừa chèn
    v_tong_tien_tour := :NEW.TongTien;

    -- 2. Tự động INSERT vào bảng HoaDon
    INSERT INTO HoaDon (
        MaDatTour,
        SoTien,
        NgayXuat,
        TrangThai
        -- Các cột khác (ChuKySo) có thể được cập nhật sau
    )
    VALUES (
        :NEW.MaDatTour,        -- Lấy MaDatTour vừa được tạo
        v_tong_tien_tour,      -- Lấy TongTien từ DatTour
        SYSDATE,               -- Ngày xuất là ngày hiện tại
        'Chưa thanh toán'      -- Thiết lập trạng thái ban đầu
    );

EXCEPTION
    WHEN OTHERS THEN
        -- Ghi log hoặc xử lý lỗi nếu cần
        -- Trong môi trường sản xuất, bạn nên ghi lại lỗi này vào bảng NhatKyHeThong.
        RAISE_APPLICATION_ERROR(-20001, 'Lỗi khi tự động tạo hóa đơn: ' || SQLERRM);
END;
/