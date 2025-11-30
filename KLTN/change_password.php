<?php
// KLTN/change_password.php
// 1. Tắt hiển thị lỗi hệ thống (Nguyên nhân gây FormatException trên Flutter)
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
    $session = require_auth();
    $userId = $session['user_id'];

    $data = json_decode(file_get_contents("php://input"), true);
    $oldPass = $data['oldPassword'] ?? '';
    $newPass = $data['newPassword'] ?? '';

    if (empty($oldPass) || empty($newPass)) {
        echo json_encode(['success' => false, 'message' => 'Vui lòng nhập đầy đủ mật khẩu.']);
        exit;
    }

    if (strlen($newPass) < 6) {
        echo json_encode(['success' => false, 'message' => 'Mật khẩu mới phải có ít nhất 6 ký tự.']);
        exit;
    }

    check_db_connection();

    // 2. Lấy Oracle Username (Dùng tên bind an toàn)
    $sqlUser = "SELECT ORACLE_USERNAME FROM KhachHang WHERE MaKhachHang = :p_chk_uid";
    $stmtUser = oci_parse($conn, $sqlUser);
    oci_bind_by_name($stmtUser, ':p_chk_uid', $userId);
    
    if (!@oci_execute($stmtUser)) {
        throw new Exception("Lỗi truy vấn thông tin user.");
    }
    
    $row = oci_fetch_assoc($stmtUser);
    oci_free_statement($stmtUser);

    if (!$row) throw new Exception("Không tìm thấy thông tin tài khoản.");
    $oracleUser = $row['ORACLE_USERNAME'];

    // 3. Xác minh mật khẩu cũ (Thử login)
    // Lưu ý: Hàm connect_custom nằm trong connect.php, đã xử lý việc kết nối an toàn
    // Sử dụng @ để chặn warning nếu login thất bại
    $testConn = @connect_custom($oracleUser, $oldPass);
    
    if (!$testConn) {
        echo json_encode(['success' => false, 'message' => 'Mật khẩu cũ không chính xác.']);
        exit;
    }
    oci_close($testConn); 

    // 4. Đổi mật khẩu bằng quyền Admin (Kết nối $conn hiện tại)
    $safeNewPass = str_replace('"', '""', $newPass);
    $safeUser = str_replace('"', '""', $oracleUser);

    $sqlAlter = "ALTER USER \"$safeUser\" IDENTIFIED BY \"$safeNewPass\"";
    $stmtAlter = @oci_parse($conn, $sqlAlter);
    
    if (!@oci_execute($stmtAlter, OCI_COMMIT_ON_SUCCESS)) {
        $e = oci_error($stmtAlter);
        throw new Exception("Lỗi đổi mật khẩu: " . ($e['message'] ?? 'Unknown'));
    }

    echo json_encode(['success' => true, 'message' => 'Đổi mật khẩu thành công!']);
    oci_free_statement($stmtAlter);

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (isset($conn) && $conn) oci_close($conn);
}
?>