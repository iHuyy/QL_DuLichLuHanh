<?php
// Ensure PHP does not emit warnings or notices into the JSON response
ini_set('display_errors', 0);
error_reporting(0);
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/auth_middleware.php';

try {
    // If caller provided explicit makhachhang in query, allow it (convenience/testing).
    // Otherwise require Authorization token.
    $userId = null;
    if (isset($_GET['makhachhang']) && trim($_GET['makhachhang']) !== '') {
        $userId = intval($_GET['makhachhang']);
    } else {
        // validate token and get session info
        $session = require_auth();
        $userId = intval($session['user_id']);
    }

    if ($userId <= 0) {
        echo json_encode(['success' => false, 'message' => 'Invalid user id']);
        exit;
    }

    global $conn;
    if (empty($conn) || $conn === null) $conn = connect_read();

    // Join HoaDon -> DatTour to find invoices belonging to the customer
    // Schema: HoaDon(MaHoaDon, MaDatTour, SoTien, NgayXuat, TrangThai)
    //        DatTour(MaDatTour, MaKhachHang, ...)
    $sql = "SELECT h.MaHoaDon AS MAHOADON, dt.MaKhachHang AS MAKHACHHANG, h.SoTien AS SOTIEN, h.TrangThai AS TRANGTHAI, TO_CHAR(h.NGAYXUAT,'YYYY-MM-DD HH24:MI:SS') AS NGAYLAP FROM HOADON h JOIN DATTour dt ON h.MaDatTour = dt.MaDatTour WHERE dt.MaKhachHang = :mk ORDER BY h.NGAYXUAT DESC";
    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) {
        // If parse failed (maybe table name differs), return empty list gracefully
        echo json_encode(['success' => true, 'invoices' => []]);
        exit;
    }

    oci_bind_by_name($stmt, ':mk', $userId);
    if (!@oci_execute($stmt)) {
        oci_free_statement($stmt);
        echo json_encode(['success' => false, 'message' => 'Failed to execute invoice query']);
        exit;
    }

    $rows = [];
    while ($r = oci_fetch_assoc($stmt)) {
        $rows[] = $r;
    }
    oci_free_statement($stmt);

    // Return a clean JSON response
    echo json_encode(['success' => true, 'invoices' => $rows]);
    exit;
} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
}

?>