<?php
// KLTN/get_user.php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

try {
    $session = require_auth();
    $userId = $session['user_id'];

    check_db_connection();

    // SỬA LỖI: Đổi :uid thành :param_uid
    $sql = "SELECT MaKhachHang, HoTen, Email, SoDienThoai, DiaChi, Oracle_Username 
            FROM KhachHang WHERE MaKhachHang = :param_uid";
    
    $stmt = @oci_parse($conn, $sql);
    oci_bind_by_name($stmt, ':param_uid', $userId); // Bind tên mới
    
    if (!@oci_execute($stmt)) throw new Exception("Lỗi truy vấn user: " . oci_error($stmt)['message']);

    $user = oci_fetch_assoc($stmt);
    oci_free_statement($stmt);

    if ($user) {
        echo json_encode([
            'success' => true,
            'data' => [
                'userID' => $user['MAKHACHHANG'],
                'username' => $user['ORACLE_USERNAME'],
                'fullName' => $user['HOTEN'],
                'email' => $user['EMAIL'],
                'phone' => $user['SODIENTHOAI'],
                'address' => $user['DIACHI']
            ]
        ]);
    } else {
        echo json_encode(['success' => false, 'message' => 'User not found']);
    }

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if ($conn) oci_close($conn);
}
?>