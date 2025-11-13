<?php
ini_set('display_errors', 1);
error_reporting(E_ALL);

// Lấy cấu hình kết nối chung và kết nối admin ($conn)
require_once __DIR__ . '/connect.php';

// --- B1: Lấy dữ liệu đầu vào từ Flutter ---
$data = json_decode(file_get_contents("php://input"), true);
$username = strtoupper(trim($data["username"] ?? ''));
$password = trim($data["password"] ?? '');
$device_info = trim($data["device_info"] ?? 'Unknown Device'); // Lấy thông tin thiết bị

if (empty($username) || empty($password)) {
    echo json_encode(["success" => false, "message" => "Vui lòng nhập đầy đủ thông tin"]);
    exit;
}

// --- B2: Xác thực thông tin đăng nhập của người dùng ---
// Tạm thời tạo một kết nối bằng chính thông tin user cung cấp để check pass
$conn_str = defined('ORACLE_CONN_STR') ? ORACLE_CONN_STR : "(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=100.91.47.90)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCLPDB1)))";
$charset  = defined('ORACLE_CHARSET') ? ORACLE_CHARSET : "AL32UTF8";

$role = (strtoupper($username) === 'SYS') ? 'SYSDBA' : 'DEFAULT';
if ($role === 'SYSDBA') {
    $user_conn = @oci_connect($username, $password, $conn_str, $charset, OCI_SYSDBA);
} else {
    $user_conn = @oci_connect($username, $password, $conn_str, $charset);
}

// Nếu kết nối thất bại, tức là sai user/pass hoặc tài khoản bị khóa
if (!$user_conn) {
    $err = oci_error();
    $msg = $err['message'] ?? '';
    if (stripos($msg, 'ORA-28000') !== false) {
        echo json_encode(["success" => false, "message" => "Tài khoản đã bị khóa do đăng nhập sai nhiều lần."]);
    } elseif (stripos($msg, 'ORA-01017') !== false) {
        echo json_encode(["success" => false, "message" => "Sai tên đăng nhập hoặc mật khẩu."]);
    } else {
        echo json_encode(["success" => false, "message" => "Lỗi xác thực: " . $msg]);
    }
    exit;
}
// Đóng kết nối tạm thời lại ngay
oci_close($user_conn);


// --- B3: Xử lý logic session trong DB (dùng kết nối admin $conn từ connect.php) ---

// Kiểm tra xem kết nối admin có sẵn sàng không
if (!$conn) {
    echo json_encode(["success" => false, "message" => "Lỗi hệ thống: Không thể kết nối tới DB với quyền admin."]);
    exit;
}

try {
    // Determine user_type and numeric USER_ID to store in USER_SESSIONS
    $user_type = ($role === 'SYSDBA') ? 'ADMIN' : 'CUSTOMER';

    // For customers, resolve MaKhachHang (numeric) from KhachHang.ORACLE_USERNAME
    $session_user_id = 0; // default numeric id to store in USER_SESSIONS
    if ($user_type === 'CUSTOMER') {
        $sql_get = "SELECT MaKhachHang FROM KhachHang WHERE UPPER(ORACLE_USERNAME) = :username";
        $stmt_get = oci_parse($conn, $sql_get);
        $upperUser = strtoupper($username);
        oci_bind_by_name($stmt_get, ':username', $upperUser);
        if (!oci_execute($stmt_get)) {
            oci_free_statement($stmt_get);
            throw new Exception("Lỗi khi truy vấn MaKhachHang.");
        }
        $rowGet = oci_fetch_assoc($stmt_get);
        oci_free_statement($stmt_get);
        $session_user_id = intval($rowGet['MAKHACHHANG'] ?? 0);
        if ($session_user_id <= 0) {
            throw new Exception("Tài khoản chưa được liên kết tới MaKhachHang.");
        }
    } else {
        // For admin/system users, use 0 as placeholder numeric id
        $session_user_id = 0;
    }

    // 1. Vô hiệu hóa tất cả các session MOBILE cũ của user này (chỉ MOBILE và IS_ACTIVE = 'Y')
    $sql_invalidate = "UPDATE USER_SESSIONS SET IS_ACTIVE = 'N' WHERE USER_ID = :user_id AND USER_TYPE = :user_type AND DEVICE_TYPE = 'MOBILE' AND IS_ACTIVE = 'Y'";
    $stmt_invalidate = oci_parse($conn, $sql_invalidate);
    oci_bind_by_name($stmt_invalidate, ':user_id', $session_user_id);
    oci_bind_by_name($stmt_invalidate, ':user_type', $user_type);

    if (!oci_execute($stmt_invalidate, OCI_NO_AUTO_COMMIT)) {
        throw new Exception("Lỗi khi vô hiệu hóa phiên cũ.");
    }

    // 2. Tạo một token mới, an toàn
    $token = bin2hex(random_bytes(32));

    // 3. Lưu session mới vào database
    // Giả sử USER_TYPE là 'CUSTOMER' cho người dùng thông thường
    $user_type = ($role === 'SYSDBA') ? 'ADMIN' : 'CUSTOMER';

    $sql_insert = "
        INSERT INTO USER_SESSIONS 
            (SESSION_ID, USER_ID, USER_TYPE, DEVICE_TYPE, DEVICE_INFO, LOGIN_TIME, IS_ACTIVE) 
        VALUES 
            (:session_id, :user_id, :user_type, 'MOBILE', :device_info, SYSTIMESTAMP, 'Y')
    ";
    $stmt_insert = oci_parse($conn, $sql_insert);
    oci_bind_by_name($stmt_insert, ':session_id', $token);
    oci_bind_by_name($stmt_insert, ':user_id', $session_user_id);
    oci_bind_by_name($stmt_insert, ':user_type', $user_type);
    oci_bind_by_name($stmt_insert, ':device_info', $device_info);

    if (!oci_execute($stmt_insert, OCI_NO_AUTO_COMMIT)) {
        throw new Exception("Lỗi khi tạo phiên đăng nhập mới.");
    }

    // 4. Commit transaction
    oci_commit($conn);

    // 4b. Cleanup: delete old MOBILE sessions for this user except the newly created one
    // This prevents table growth since each login creates a new session row.
    try {
        $sql_cleanup = "DELETE FROM USER_SESSIONS WHERE USER_ID = :user_id AND DEVICE_TYPE = 'MOBILE' AND SESSION_ID <> :session_id";
        $stmt_cleanup = oci_parse($conn, $sql_cleanup);
        oci_bind_by_name($stmt_cleanup, ':user_id', $session_user_id);
        oci_bind_by_name($stmt_cleanup, ':session_id', $token);
        // execute and commit the cleanup
        if (!@oci_execute($stmt_cleanup, OCI_NO_AUTO_COMMIT)) {
            // Non-fatal: log and continue, but rollback to be safe
            oci_free_statement($stmt_cleanup);
            throw new Exception("Lỗi khi xóa các phiên cũ.");
        }
        oci_commit($conn);
        if (isset($stmt_cleanup)) oci_free_statement($stmt_cleanup);
    } catch (Exception $cleanupEx) {
        // If cleanup fails, attempt to rollback but keep the new session.
        try { oci_rollback($conn); } catch (Exception $_) {}
        // Log to server error log for investigation but don't block login success
        error_log('Session cleanup error: ' . $cleanupEx->getMessage());
    }

    // 5. Trả token về cho client
    echo json_encode([
        "success"  => true,
        "message"  => "Đăng nhập thành công!",
        "session_id"    => $token,
        // Return numeric user id (MaKhachHang) for customers, 0 for admin
        "userID"   => $session_user_id,
        "username" => $username,
        "role"     => $role
    ]);

} catch (Exception $e) {
    oci_rollback($conn); // Rollback nếu có lỗi
    echo json_encode(["success" => false, "message" => "Lỗi xử lý phiên: " . $e->getMessage()]);
} finally {
    // Giải phóng statement và đóng kết nối
    if (isset($stmt_invalidate)) oci_free_statement($stmt_invalidate);
    if (isset($stmt_insert)) oci_free_statement($stmt_insert);
    // CRITICAL: Close Oracle connection to free up session slot and avoid SESSIONS_PER_USER limit
    if ($conn !== null && is_resource($conn)) {
        oci_close($conn);
        $conn = null;
    }
}

?>
