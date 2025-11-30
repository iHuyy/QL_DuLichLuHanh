<?php
// KLTN/update_profile.php
// 1. Tắt hiển thị lỗi để tránh làm hỏng JSON
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

// Bắt lỗi Fatal phút chót
register_shutdown_function(function() {
    $error = error_get_last();
    if ($error !== null && $error['type'] === E_ERROR) {
        echo json_encode(['success' => false, 'message' => 'Fatal Error: ' . $error['message']]);
    }
});

try {
    // 2. Xác thực
    $session = require_auth();
    $userId = $session['user_id'];

    // 3. Nhận dữ liệu
    $data = json_decode(file_get_contents("php://input"), true);
    
    $hoTen = trim($data['hoTen'] ?? '');
    $email = trim($data['email'] ?? '');
    $sdt = trim($data['soDienThoai'] ?? '');
    $diaChi = trim($data['diaChi'] ?? '');

    if (empty($hoTen) || empty($email)) {
        echo json_encode(['success' => false, 'message' => 'Họ tên và Email không được để trống.']);
        exit;
    }

    check_db_connection();

    // 4. Cập nhật (SỬA LỖI ORA-01745: Đổi tên bind variable)
    $sql = "UPDATE KhachHang 
            SET HoTen = :p_hoten, Email = :p_email, SoDienThoai = :p_sdt, DiaChi = :p_diachi 
            WHERE MaKhachHang = :p_uid";
    
    $stmt = oci_parse($conn, $sql);
    
    // Bind với tên biến mới an toàn hơn
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
    // Trả về lỗi dạng JSON sạch sẽ
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (isset($conn) && $conn) oci_close($conn);
}
?>