<?php
// KLTN/update_profile.php
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

register_shutdown_function(function() {
    $error = error_get_last();
    if ($error !== null && $error['type'] === E_ERROR) {
        echo json_encode(['success' => false, 'message' => 'Fatal Error: ' . $error['message']]);
    }
});

try {
    // 1. Xác thực
    $session = require_auth();
    $userId = $session['user_id'];

    // 2. Nhận dữ liệu
    $data = json_decode(file_get_contents("php://input"), true);
    
    $hoTen = trim($data['hoTen'] ?? '');
    $email = trim($data['email'] ?? '');
    $sdt = trim($data['soDienThoai'] ?? '');
    $diaChi = trim($data['diaChi'] ?? '');

    if (empty($hoTen) || empty($email)) {
        echo json_encode(['success' => false, 'message' => 'Họ tên và Email không được để trống.']);
        exit;
    }

    // *** MỚI: Validate Phone ***
    if (!preg_match('/^0\d{9}$/', $sdt)) {
        echo json_encode(['success' => false, 'message' => 'Số điện thoại không hợp lệ. Phải gồm 10 chữ số và bắt đầu bằng số 0.']);
        exit;
    }

    check_db_connection();

    // 3. Cập nhật
    $sql = "UPDATE KhachHang 
            SET HoTen = :p_hoten, Email = :p_email, SoDienThoai = :p_sdt, DiaChi = :p_diachi 
            WHERE MaKhachHang = :p_uid";
    
    $stmt = oci_parse($conn, $sql);
    
    oci_bind_by_name($stmt, ':p_hoten', $hoTen);
    oci_bind_by_name($stmt, ':p_email', $email);
    oci_bind_by_name($stmt, ':p_sdt', $sdt);
    oci_bind_by_name($stmt, ':p_diachi', $diaChi);
    oci_bind_by_name($stmt, ':p_uid', $userId);

    if (!@oci_execute($stmt, OCI_COMMIT_ON_SUCCESS)) {
        $e = oci_error($stmt);
        throw new Exception("Lỗi cập nhật: " . ($e['message'] ?? 'Unknown'));
    }

    echo json_encode([
        'success' => true, 
        'message' => 'Cập nhật thông tin thành công!',
        'data' => [
            'fullName' => $hoTen,
            'email' => $email,
            'phone' => $sdt,
            'address' => $diaChi
        ]
    ]);

    oci_free_statement($stmt);

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (isset($conn) && $conn) oci_close($conn);
}
?>