<?php
// KLTN/get_booking_detail.php

// 1. Khởi tạo buffer
ob_start(); 
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

// Hàm helper để trả về JSON và kết thúc an toàn
function send_json_response($data) {
    // Xóa sạch bộ đệm trước đó để đảm bảo không dính ký tự lạ
    ob_clean();
    
    // Mã hóa JSON với các tùy chọn an toàn:
    // JSON_INVALID_UTF8_SUBSTITUTE: Thay thế ký tự lỗi font thay vì trả về false
    // JSON_UNESCAPED_UNICODE: Giữ nguyên tiếng Việt
    $json = json_encode($data, JSON_INVALID_UTF8_SUBSTITUTE | JSON_UNESCAPED_UNICODE);
    
    if ($json === false) {
        // Nếu vẫn lỗi encode, trả về thông báo lỗi thủ công
        echo '{"success":false, "message":"Server Error: JSON Encoding Failed - ' . json_last_error_msg() . '"}';
    } else {
        echo $json;
    }
    
    // Đẩy dữ liệu ra ngay lập tức
    ob_end_flush();
    exit;
}

// Bắt lỗi Fatal
register_shutdown_function(function() {
    $error = error_get_last();
    if ($error !== null && $error['type'] === E_ERROR) {
        send_json_response(['success' => false, 'message' => 'Fatal Error: ' . $error['message']]);
    }
});

try {
    $session = require_auth(); 
    
    $madattour = isset($_GET['madattour']) ? intval($_GET['madattour']) : 0;
    if ($madattour <= 0) {
        send_json_response(['success' => false, 'message' => 'Mã đặt tour không hợp lệ']);
    }

    check_db_connection();

    $sql = "SELECT 
                dt.MaDatTour, dt.MaKhachHang, dt.MaTour, 
                TO_CHAR(dt.NgayDat,'YYYY-MM-DD') AS NGAYDAT, 
                dt.SoNguoiLon, dt.SoTreEm, dt.TongTien, 
                dt.TrangThaiThanhToan, dt.TrangThaiDat, dt.YeuCauDacBiet, 
                
                t.TieuDe, t.MoTa, t.NoiKhoiHanh, t.NoiDen, t.ThanhPho, 
                TO_CHAR(t.ThoiGian,'YYYY-MM-DD') AS THOIGIAN, 
                t.GiaNguoiLon, t.GiaTreEm,
                
                hd.MaHoaDon,
                hd.TrangThai AS TrangThaiHoaDon,
                
                (SELECT DuLieuAnh FROM AnhTour WHERE MaTour = t.MaTour AND ROWNUM = 1) AS ANHTHUMB,
                (SELECT LoaiAnh FROM AnhTour WHERE MaTour = t.MaTour AND ROWNUM = 1) AS MIMETHUMB
            FROM DATTOUR dt 
            JOIN TOUR t ON dt.MaTour = t.MaTour 
            LEFT JOIN HOADON hd ON dt.MaDatTour = hd.MaDatTour
            WHERE dt.MaDatTour = :p_madattour";

    $stmt = oci_parse($conn, $sql);
    if (!$stmt) throw new Exception("Lỗi chuẩn bị truy vấn: " . oci_error($conn)['message']);
    
    oci_bind_by_name($stmt, ':p_madattour', $madattour);
    
    if (!@oci_execute($stmt)) throw new Exception("Lỗi thực thi truy vấn: " . oci_error($stmt)['message']);

    $row = oci_fetch_assoc($stmt);
    oci_free_statement($stmt);

    if (!$row) {
        send_json_response(['success' => false, 'message' => 'Không tìm thấy thông tin đặt tour']);
    }

    // Xử lý ảnh
    $imgUrl = '';
    if (isset($row['ANHTHUMB']) && $row['ANHTHUMB'] !== null) {
        $blob = $row['ANHTHUMB'];
        $data = null;
        
        if (is_object($blob)) {
            if (method_exists($blob, 'load')) {
                $data = $blob->load();
                $blob->free();
            } else {
                $data = (string)$blob;
            }
        } else if (is_resource($blob)) {
            $data = stream_get_contents($blob);
        } else {
            $data = $blob;
        }

        if ($data) {
            $mime = !empty($row['MIMETHUMB']) ? $row['MIMETHUMB'] : 'image/jpeg';
            $imgUrl = "data:$mime;base64," . base64_encode($data);
        }
    }
    
    // Xóa trường BLOB nặng khỏi mảng trước khi trả về (để JSON nhẹ hơn và tránh lỗi encode)
    unset($row['ANHTHUMB']);
    
    $row['HINHANH'] = $imgUrl;
    $row['DULIEUANH'] = $imgUrl;

    send_json_response(['success' => true, 'booking' => $row]);

} catch (Exception $e) {
    send_json_response(['success' => false, 'message' => 'Server Error: ' . $e->getMessage()]);
} finally {
    if (!empty($conn)) @oci_close($conn);
}
?>