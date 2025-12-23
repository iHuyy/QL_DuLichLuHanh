<?php
// KLTN/change_password.php
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

    // 1. Kiểm tra rỗng
    if (empty($oldPass) || empty($newPass)) {
        echo json_encode(['success' => false, 'message' => 'Vui lòng nhập đầy đủ mật khẩu.']);
        exit;
    }

    // 2. Kiểm tra trùng mật khẩu cũ
    if ($oldPass === $newPass) {
        echo json_encode(['success' => false, 'message' => 'Mật khẩu mới không được trùng với mật khẩu hiện tại.']);
        exit;
    }

    // 3. Kiểm tra độ mạnh mật khẩu (Server-side validation)
    // Ít nhất 8 ký tự, 1 hoa, 1 thường, 1 số, 1 ký tự đặc biệt
    if (!preg_match('/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$/', $newPass)) {
        echo json_encode(['success' => false, 'message' => 'Mật khẩu mới phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt.']);
        exit;
    }

    check_db_connection();

    // 4. Lấy Oracle Username
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

    // 5. Xác minh mật khẩu cũ (Thử login)
    $testConn = @connect_custom($oracleUser, $oldPass);
    
    if (!$testConn) {
        $e = oci_error();
        // Nếu lỗi là ORA-28001 (Password expired), nghĩa là mật khẩu cũ ĐÚNG nhưng hết hạn -> Vẫn cho đổi
        if ($e && stripos($e['message'], 'ORA-28001') === false) {
             echo json_encode(['success' => false, 'message' => 'Mật khẩu hiện tại không chính xác.']);
             exit;
        }
    } else {
        oci_close($testConn); 
    }

    // 6. Đổi mật khẩu
    $safeNewPass = str_replace('"', '""', $newPass);
    $safeUser = str_replace('"', '""', $oracleUser);

    $sqlAlter = "ALTER USER \"$safeUser\" IDENTIFIED BY \"$safeNewPass\"";
    $stmtAlter = @oci_parse($conn, $sqlAlter);
    
    if (!@oci_execute($stmtAlter, OCI_COMMIT_ON_SUCCESS)) {
        $e = oci_error($stmtAlter);
        // ORA-28003: password verification for the specified password failed (Do Oracle Profile quy định)
        if (stripos($e['message'], 'ORA-28003') !== false) {
             throw new Exception("Mật khẩu mới không đáp ứng chính sách bảo mật của hệ thống Oracle (ví dụ: quá giống mật khẩu cũ).");
        }
        throw new Exception("Lỗi đổi mật khẩu: " . ($e['message'] ?? 'Unknown'));
    }

    // Mở khóa tài khoản nếu đang bị khóa/hết hạn
    $sqlUnlock = "ALTER USER \"$safeUser\" ACCOUNT UNLOCK";
    $stmtUnlock = @oci_parse($conn, $sqlUnlock);
    @oci_execute($stmtUnlock, OCI_COMMIT_ON_SUCCESS);

    echo json_encode(['success' => true, 'message' => 'Đổi mật khẩu thành công!']);
    oci_free_statement($stmtAlter);

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (isset($conn) && $conn) oci_close($conn);
}
?>