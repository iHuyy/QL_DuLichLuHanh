<?php
// KLTN/verify_invoice.php
// Tắt hiển thị lỗi hệ thống để không làm hỏng JSON
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/connect.php';

// Bắt lỗi Fatal Error phút chót
register_shutdown_function(function() {
    $error = error_get_last();
    if ($error !== null && $error['type'] === E_ERROR) {
        echo json_encode(['success' => false, 'isValid' => false, 'message' => 'Server Fatal Error: ' . $error['message']]);
    }
});

try {
    $maHoaDon = 0;
    if (isset($_REQUEST['maHoaDon'])) {
        $maHoaDon = intval($_REQUEST['maHoaDon']);
    } else {
        $input = json_decode(file_get_contents('php://input'), true) ?? [];
        $maHoaDon = intval($input['maHoaDon'] ?? 0);
    }

    if ($maHoaDon <= 0) {
        echo json_encode(['success' => false, 'message' => 'Vui lòng cung cấp Mã hóa đơn.']);
        exit;
    }

    check_db_connection();

    // 1. Lấy thông tin
    $sql = "SELECT MaHoaDon, TrangThai, ChuKySo, Payload, TO_CHAR(NgayXuat, 'YYYY-MM-DD HH24:MI:SS') as NGAYXUAT 
            FROM HoaDon WHERE MaHoaDon = :m";
    $stmt = oci_parse($conn, $sql);
    oci_bind_by_name($stmt, ':m', $maHoaDon);
    
    if (!@oci_execute($stmt)) throw new Exception("Lỗi truy vấn DB");
    
    $row = oci_fetch_assoc($stmt);
    if (!$row) {
        echo json_encode(['success' => false, 'isValid' => false, 'message' => "Không tìm thấy hóa đơn #$maHoaDon"]);
        exit;
    }

    // 2. Xử lý Payload
    $payloadRaw = "";
    if (isset($row['PAYLOAD'])) {
        $pl = $row['PAYLOAD'];
        if (is_object($pl)) {
             if (method_exists($pl, 'load')) {
                 $payloadRaw = $pl->load();
                 $pl->free();
             } else {
                 $payloadRaw = (string)$pl;
             }
        } else {
            $payloadRaw = $pl;
        }
    }

    if (empty($payloadRaw) || empty($row['CHUKYSO'])) {
        echo json_encode(['success' => true, 'isValid' => false, 'message' => 'Hóa đơn chưa được ký số.']);
        exit;
    }

    // 3. Load Key
    $publicKey = null;
    $paths = [
        'G:/Study/KLTN/AppQLDVDLLH/app_dllh_may_that/Keys/public_key.pem',
        __DIR__ . '/Keys/public_key.pem'
    ];
    foreach ($paths as $p) {
        if (file_exists($p)) { $publicKey = file_get_contents($p); break; }
    }

    if (!$publicKey) throw new Exception("Server missing Public Key.");

    // 4. Verify
    $signature = base64_decode($row['CHUKYSO']);
    $ok = openssl_verify($payloadRaw, $signature, $publicKey, OPENSSL_ALGO_SHA256);

    if ($ok === 1) {
        echo json_encode([
            'success' => true,
            'isValid' => true,
            'message' => "Hóa đơn HỢP LỆ.\nChữ ký số khớp hoàn toàn.",
            'data' => [
                'maHoaDon' => $row['MAHOADON'],
                'ngayXuat' => $row['NGAYXUAT'],
                'trangThai' => $row['TRANGTHAI']
            ]
        ]);
    } elseif ($ok === 0) {
        echo json_encode(['success' => true, 'isValid' => false, 'message' => "CẢNH BÁO: Dữ liệu KHÔNG KHỚP với chữ ký."]);
    } else {
        throw new Exception("Lỗi OpenSSL: " . openssl_error_string());
    }

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => 'Lỗi: ' . $e->getMessage()]);
} finally {
    if (isset($conn) && $conn) oci_close($conn);
}
?>