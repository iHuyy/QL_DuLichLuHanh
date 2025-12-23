<?php
// KLTN/force_change_password.php
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/connect.php';

$data = json_decode(file_get_contents("php://input"), true);
$username = strtoupper(trim($data['username'] ?? ''));
$oldPass = trim($data['oldPassword'] ?? '');
$newPass = trim($data['newPassword'] ?? '');

if (empty($username) || empty($oldPass) || empty($newPass)) {
    echo json_encode(['success' => false, 'message' => 'Vui lòng nhập đầy đủ thông tin.']);
    exit;
}

// 1. Kiểm tra mật khẩu mới khác mật khẩu cũ
if ($oldPass === $newPass) {
    echo json_encode(['success' => false, 'message' => 'Mật khẩu mới không được trùng với mật khẩu cũ.']);
    exit;
}

// 2. Validate mật khẩu mới
if (strlen($newPass) < 8 || !preg_match('/[A-Z]/', $newPass) || !preg_match('/[0-9]/', $newPass) || !preg_match('/[!@#$%^&*(),.?":{}|<>]/', $newPass)) {
    echo json_encode(['success' => false, 'message' => 'Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, số và ký tự đặc biệt.']);
    exit;
}

// 3. Xác minh người dùng bằng cách thử kết nối với mật khẩu cũ
// Lưu ý: Nếu mật khẩu hết hạn, oci_connect vẫn trả về lỗi nhưng mã lỗi là ORA-28001
// Chúng ta coi ORA-28001 là xác thực thành công danh tính (đúng pass nhưng hết hạn).
$testConn = @oci_connect($username, $oldPass, ORACLE_CONN_STR, ORACLE_CHARSET);
$isVerified = false;

if ($testConn) {
    $isVerified = true;
    oci_close($testConn);
} else {
    $err = oci_error();
    // Chấp nhận ORA-28001 (Password expired) là xác thực đúng
    if (isset($err['message']) && stripos($err['message'], 'ORA-28001') !== false) {
        $isVerified = true;
    }
}

if (!$isVerified) {
    echo json_encode(['success' => false, 'message' => 'Mật khẩu cũ không chính xác.']);
    exit;
}

// 4. Thực hiện đổi mật khẩu (Sử dụng quyền Admin của $conn trong connect.php)
check_db_connection();

try {
    $safeUser = str_replace('"', '""', $username);
    $safeNewPass = str_replace('"', '""', $newPass);

    $sql = "ALTER USER \"$safeUser\" IDENTIFIED BY \"$safeNewPass\"";
    $stmt = @oci_parse($conn, $sql);

    if (!@oci_execute($stmt, OCI_COMMIT_ON_SUCCESS)) {
        $e = oci_error($stmt);
        throw new Exception("Lỗi hệ thống: " . $e['message']);
    }

    echo json_encode(['success' => true, 'message' => 'Đổi mật khẩu thành công! Vui lòng đăng nhập lại.']);

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (isset($conn) && $conn) oci_close($conn);
}
?>