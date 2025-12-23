<?php
// KLTN/cancel_booking.php

// 1. Cấu hình để không in lỗi HTML ra màn hình
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');

// 2. Bắt lỗi Fatal Error (Lỗi sập nguồn) để trả về JSON
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
    // 3. Xác thực người dùng
    $session = require_auth();
    $userId = $session['user_id'];

    // 4. Nhận dữ liệu
    $data = json_decode(file_get_contents("php://input"), true);
    $bookingId = intval($data['bookingId'] ?? 0);
    $lyDo = trim($data['lyDo'] ?? 'Khách hàng hủy qua App');

    if ($bookingId <= 0) {
        echo json_encode(['success' => false, 'message' => 'Mã đặt tour không hợp lệ']);
        exit;
    }

    check_db_connection();

    // 5. Kiểm tra thông tin đơn hàng
    $sqlCheck = "SELECT MaKhachHang, TrangThaiDat, TrangThaiThanhToan FROM DatTour WHERE MaDatTour = :id";
    $stmtCheck = @oci_parse($conn, $sqlCheck);
    
    if (!$stmtCheck) {
        $e = oci_error($conn);
        throw new Exception("Lỗi prepare SQL check: " . $e['message']);
    }

    oci_bind_by_name($stmtCheck, ':id', $bookingId);
    
    if (!@oci_execute($stmtCheck)) {
        $e = oci_error($stmtCheck);
        throw new Exception("Lỗi execute SQL check: " . $e['message']);
    }

    $row = oci_fetch_assoc($stmtCheck);
    oci_free_statement($stmtCheck);

    if (!$row) {
        echo json_encode(['success' => false, 'message' => 'Đơn đặt tour không tồn tại']);
        exit;
    }

    // Kiểm tra quyền sở hữu
    if ((int)$row['MAKHACHHANG'] !== (int)$userId) {
        echo json_encode(['success' => false, 'message' => 'Bạn không có quyền hủy đơn này']);
        exit;
    }

    // Xử lý chuỗi an toàn
    $statusRaw = $row['TRANGTHAIDAT'] ?? '';
    $payStatusRaw = $row['TRANGTHAITHANHTOAN'] ?? '';

    $status = function_exists('mb_strtolower') ? mb_strtolower($statusRaw, 'UTF-8') : strtolower($statusRaw);
    $payStatus = function_exists('mb_strtolower') ? mb_strtolower($payStatusRaw, 'UTF-8') : strtolower($payStatusRaw);

    // Logic kiểm tra điều kiện hủy
    if (strpos($status, 'hủy') !== false || strpos($status, 'hoàn thành') !== false) {
        echo json_encode(['success' => false, 'message' => 'Đơn này không thể hủy (Đã hủy hoặc đã hoàn thành)']);
        exit;
    }

    if (strpos($payStatus, 'đã thanh toán') !== false) {
        echo json_encode(['success' => false, 'message' => 'Tour đã thanh toán không thể hủy trực tuyến. Vui lòng liên hệ bộ phận CSKH.']);
        exit;
    }

    // 6. Thực hiện UPDATE
    // [SỬA LỖI] Chỉ cập nhật TrangThaiDat, BỎ cập nhật TrangThaiThanhToan để tránh lỗi ORA-02290
    $sqlUpdate = "UPDATE DatTour 
                  SET TrangThaiDat = 'Đã hủy', 
                      YeuCauDacBiet = YeuCauDacBiet || ' | Lý do hủy: ' || :lydo
                  WHERE MaDatTour = :id";
    
    $stmtUpdate = @oci_parse($conn, $sqlUpdate);
    if (!$stmtUpdate) {
        $e = oci_error($conn);
        throw new Exception("Lỗi prepare update: " . $e['message']);
    }

    oci_bind_by_name($stmtUpdate, ':lydo', $lyDo);
    oci_bind_by_name($stmtUpdate, ':id', $bookingId);

    if (!@oci_execute($stmtUpdate, OCI_COMMIT_ON_SUCCESS)) {
        $e = oci_error($stmtUpdate);
        throw new Exception("Lỗi thực thi hủy: " . $e['message']);
    }
    oci_free_statement($stmtUpdate);

    // Cập nhật trạng thái hóa đơn (Nếu có hóa đơn thì cập nhật luôn cho đồng bộ)
    // Lưu ý: Nếu HoaDon cũng có constraint chặt thì có thể cần bỏ đoạn này, 
    // nhưng thường HoaDon.TrangThai linh động hơn hoặc có giá trị 'Đã hủy'.
    $sqlHd = "UPDATE HoaDon SET TrangThai = 'Đã hủy' WHERE MaDatTour = :id";
    $stmtHd = @oci_parse($conn, $sqlHd);
    if ($stmtHd) {
        oci_bind_by_name($stmtHd, ':id', $bookingId);
        @oci_execute($stmtHd, OCI_COMMIT_ON_SUCCESS);
        oci_free_statement($stmtHd);
    }

    echo json_encode(['success' => true, 'message' => 'Đã hủy đặt tour thành công']);

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (!empty($conn)) @oci_close($conn);
}
?>