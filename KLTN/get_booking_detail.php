<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php'; // (THÊM)

try {
    $session = require_auth(); // (THÊM - Buộc xác thực)

    $madattour = isset($_GET['madattour']) ? intval($_GET['madattour']) : 0;
    if ($madattour <= 0) {
        echo json_encode(['success' => false, 'message' => 'Missing or invalid madattour']);
        exit;
    }

    global $conn;
    if (empty($conn) || $conn === null) $conn = connect_read();

    $sql = "SELECT dt.MaDatTour, dt.MaKhachHang, dt.MaTour, TO_CHAR(dt.NgayDat,'YYYY-MM-DD') AS NGAYDAT, dt.SoNguoiLon, dt.SoTreEm, dt.TongTien, dt.TrangThaiThanhToan, dt.TrangThaiDat, dt.YeuCauDacBiet, t.TieuDe, t.MoTa, t.NoiKhoiHanh, t.NoiDen, t.ThanhPho, TO_CHAR(t.ThoiGian,'YYYY-MM-DD') AS THOIGIAN, t.GiaNguoiLon, t.GiaTreEm FROM DATTOUR dt JOIN TOUR t ON dt.MaTour = t.MaTour WHERE dt.MaDatTour = :madattour";

    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) { echo json_encode(['success' => false, 'message' => 'Failed to prepare query']); exit; }
    oci_bind_by_name($stmt, ':madattour', $madattour);
    if (!@oci_execute($stmt)) { oci_free_statement($stmt); echo json_encode(['success' => false, 'message' => 'Failed to execute query']); exit; }

    $row = oci_fetch_assoc($stmt);
    oci_free_statement($stmt);

    if (!$row) {
        echo json_encode(['success' => false, 'message' => 'Booking not found']);
        exit;
    }

    echo json_encode(['success' => true, 'booking' => $row]);
    exit;
} catch (Exception $e) {
    // (THÊM) Bắt lỗi 401 hoặc lỗi 500
    // Mã 401 sẽ tự động được đặt bởi auth_middleware.php
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
}
?>