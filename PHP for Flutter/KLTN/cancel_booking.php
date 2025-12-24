<?php
// KLTN/cancel_booking.php

// 1. Cấu hình
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');

register_shutdown_function(function() {
    $error = error_get_last();
    if ($error !== null && $error['type'] === E_ERROR) {
        while (ob_get_level()) ob_end_clean();
        echo json_encode(['success' => false, 'message' => 'Server Fatal Error: ' . $error['message']]);
    }
});

require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

try {
    // 2. Xác thực
    $session = require_auth();
    $userId = $session['user_id'];

    $data = json_decode(file_get_contents("php://input"), true);
    $bookingId = intval($data['bookingId'] ?? 0);
    $lyDo = trim($data['lyDo'] ?? 'Khách hàng hủy qua App');

    if ($bookingId <= 0) {
        echo json_encode(['success' => false, 'message' => 'Mã đặt tour không hợp lệ']);
        exit;
    }

    check_db_connection();

    // 3. Kiểm tra thông tin đơn hàng VÀ NGÀY KHỞI HÀNH
    // Join với bảng Tour để lấy THOIGIAN
    $sqlCheck = "SELECT d.MaKhachHang, d.TrangThaiDat, d.TrangThaiThanhToan, 
                        TO_CHAR(t.ThoiGian, 'YYYY-MM-DD') as NgayKhoiHanh
                 FROM DatTour d
                 JOIN Tour t ON d.MaTour = t.MaTour
                 WHERE d.MaDatTour = :id";
                 
    $stmtCheck = @oci_parse($conn, $sqlCheck);
    if (!$stmtCheck) throw new Exception("Lỗi SQL check: " . oci_error($conn)['message']);

    oci_bind_by_name($stmtCheck, ':id', $bookingId);
    
    if (!@oci_execute($stmtCheck)) throw new Exception("Lỗi execute check: " . oci_error($stmtCheck)['message']);

    $row = oci_fetch_assoc($stmtCheck);
    oci_free_statement($stmtCheck);

    if (!$row) {
        echo json_encode(['success' => false, 'message' => 'Đơn đặt tour không tồn tại']);
        exit;
    }

    // 4. Validate Quyền sở hữu
    if ((int)$row['MAKHACHHANG'] !== (int)$userId) {
        echo json_encode(['success' => false, 'message' => 'Bạn không có quyền hủy đơn này']);
        exit;
    }

    $statusRaw = $row['TRANGTHAIDAT'] ?? '';
    $status = mb_strtolower($statusRaw, 'UTF-8');

    // 5. Validate Trạng thái đơn (Đã hủy/Hoàn thành thì thôi)
    if (strpos($status, 'hủy') !== false || strpos($status, 'hoàn thành') !== false) {
        echo json_encode(['success' => false, 'message' => 'Đơn này đã kết thúc hoặc đã hủy.']);
        exit;
    }

    // [QUAN TRỌNG] 6. Validate Ngày: Chỉ cho hủy trước 2 ngày (Difference > 1)
    // Ví dụ: Đi ngày 25. Nay ngày 24 => Diff = 1 => KHÔNG ĐƯỢC HỦY. Nay 23 => Diff = 2 => ĐƯỢC.
    $ngayKhoiHanhStr = $row['NGAYKHOIHANH']; // YYYY-MM-DD
    if ($ngayKhoiHanhStr) {
        $tourDate = new DateTime($ngayKhoiHanhStr);
        $today = new DateTime();
        $today->setTime(0,0,0); // Reset giờ về 0h để so sánh ngày chuẩn
        $tourDate->setTime(0,0,0);
        
        $interval = $today->diff($tourDate);
        $days = (int)$interval->format('%r%a'); // %r để lấy dấu (âm/dương), %a là số ngày tuyệt đối

        // Nếu ngày đi đã qua hoặc còn <= 1 ngày
        if ($days <= 1) {
             echo json_encode([
                 'success' => false, 
                 'message' => "Đã quá hạn hủy tour. Chỉ được hủy trước ngày khởi hành ít nhất 2 ngày."
             ]);
             exit;
        }
    }

    // 7. Thực hiện UPDATE (Cho phép hủy dù đã thanh toán)
    $sqlUpdate = "UPDATE DatTour 
                  SET TrangThaiDat = 'Đã hủy', 
                      YeuCauDacBiet = YeuCauDacBiet || ' | Lý do hủy: ' || :lydo
                  WHERE MaDatTour = :id";
    
    $stmtUpdate = @oci_parse($conn, $sqlUpdate);
    oci_bind_by_name($stmtUpdate, ':lydo', $lyDo);
    oci_bind_by_name($stmtUpdate, ':id', $bookingId);

    if (!@oci_execute($stmtUpdate, OCI_COMMIT_ON_SUCCESS)) {
        throw new Exception("Lỗi thực thi hủy: " . oci_error($stmtUpdate)['message']);
    }

    // Cập nhật hóa đơn nếu có
    $sqlHd = "UPDATE HoaDon SET TrangThai = 'Đã hủy' WHERE MaDatTour = :id";
    $stmtHd = @oci_parse($conn, $sqlHd);
    if ($stmtHd) {
        oci_bind_by_name($stmtHd, ':id', $bookingId);
        @oci_execute($stmtHd, OCI_COMMIT_ON_SUCCESS);
    }

    echo json_encode(['success' => true, 'message' => 'Đã hủy đặt tour thành công']);

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (!empty($conn)) @oci_close($conn);
}
?>