<?php
// KLTN/cancel_booking.php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

try {
    // 1. Xác thực người dùng
    $session = require_auth();
    $userId = $session['user_id'];

    // 2. Nhận dữ liệu
    $data = json_decode(file_get_contents("php://input"), true);
    $bookingId = intval($data['bookingId'] ?? 0);
    $lyDo = trim($data['lyDo'] ?? 'Khách hàng hủy qua App');

    if ($bookingId <= 0) {
        echo json_encode(['success' => false, 'message' => 'Mã đặt tour không hợp lệ']);
        exit;
    }

    check_db_connection();

    // 3. Kiểm tra quyền sở hữu và trạng thái hiện tại
    // SỬA ĐỔI: Lấy thêm TRANGTHAITHANHTOAN để kiểm tra
    $sqlCheck = "SELECT MaKhachHang, TrangThaiDat, TrangThaiThanhToan FROM DatTour WHERE MaDatTour = :id";
    $stmtCheck = oci_parse($conn, $sqlCheck);
    oci_bind_by_name($stmtCheck, ':id', $bookingId);
    oci_execute($stmtCheck);
    $row = oci_fetch_assoc($stmtCheck);
    oci_free_statement($stmtCheck);

    if (!$row) {
        echo json_encode(['success' => false, 'message' => 'Đơn đặt tour không tồn tại']);
        exit;
    }

    if ($row['MAKHACHHANG'] != $userId) {
        echo json_encode(['success' => false, 'message' => 'Bạn không có quyền hủy đơn này']);
        exit;
    }

    $status = mb_strtolower($row['TRANGTHAIDAT'], 'UTF-8');
    $payStatus = mb_strtolower($row['TRANGTHAITHANHTOAN'] ?? '', 'UTF-8');

    // Check 1: Đã hủy hoặc hoàn thành thì không hủy nữa
    if (strpos($status, 'hủy') !== false || strpos($status, 'hoàn thành') !== false) {
        echo json_encode(['success' => false, 'message' => 'Đơn này không thể hủy (Đã hủy hoặc đã hoàn thành)']);
        exit;
    }

    // Check 2: Đã thanh toán thì không được hủy (Logic mới)
    if (strpos($payStatus, 'đã thanh toán') !== false) {
        echo json_encode(['success' => false, 'message' => 'Tour đã thanh toán không thể hủy trực tuyến. Vui lòng liên hệ bộ phận CSKH.']);
        exit;
    }

    // 4. Thực hiện hủy
    // Cập nhật trạng thái DatTour và HoaDon (nếu có)
    $sqlUpdate = "UPDATE DatTour 
                  SET TrangThaiDat = 'Đã hủy', 
                      TrangThaiThanhToan = 'Đã hủy',
                      YeuCauDacBiet = YeuCauDacBiet || ' | Lý do hủy: ' || :lydo
                  WHERE MaDatTour = :id";
    
    $stmtUpdate = oci_parse($conn, $sqlUpdate);
    oci_bind_by_name($stmtUpdate, ':lydo', $lyDo);
    oci_bind_by_name($stmtUpdate, ':id', $bookingId);

    if (oci_execute($stmtUpdate, OCI_COMMIT_ON_SUCCESS)) {
        // Cập nhật luôn trạng thái hóa đơn nếu có
        $sqlHd = "UPDATE HoaDon SET TrangThai = 'Đã hủy' WHERE MaDatTour = :id";
        $stmtHd = oci_parse($conn, $sqlHd);
        oci_bind_by_name($stmtHd, ':id', $bookingId);
        oci_execute($stmtHd, OCI_COMMIT_ON_SUCCESS);
        oci_free_statement($stmtHd);

        echo json_encode(['success' => true, 'message' => 'Đã hủy đặt tour thành công']);
    } else {
        $e = oci_error($stmtUpdate);
        echo json_encode(['success' => false, 'message' => 'Lỗi DB: ' . $e['message']]);
    }
    oci_free_statement($stmtUpdate);

} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if ($conn) oci_close($conn);
}
?>