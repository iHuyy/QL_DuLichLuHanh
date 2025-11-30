<?php
// KLTN/login.php
ini_set('display_errors', 0);
error_reporting(E_ALL);

require_once __DIR__ . '/connect.php';

$data = json_decode(file_get_contents("php://input"), true);
$username = strtoupper(trim($data["username"] ?? ''));
$password = trim($data["password"] ?? '');
$device_info = trim($data["device_info"] ?? 'Unknown Device');

if (empty($username) || empty($password)) {
    echo json_encode(["success" => false, "message" => "Vui lòng nhập đầy đủ thông tin"]);
    exit;
}

// --- 1. Xác thực User ---
$role = (strtoupper($username) === 'SYS') ? 'SYSDBA' : 'DEFAULT';

if (strtoupper($role) === 'SYSDBA') {
    $user_conn = @oci_connect($username, $password, ORACLE_CONN_STR, ORACLE_CHARSET, OCI_SYSDBA);
} else {
    $user_conn = @oci_connect($username, $password, ORACLE_CONN_STR, ORACLE_CHARSET);
}

if (!$user_conn) {
    $err = oci_error();
    if (!$err) {
        echo json_encode([
            "success" => false, 
            "message" => "Kết nối thất bại (Không có mã lỗi). Vui lòng kiểm tra lại IP/Port trong file connect.php"
        ]);
        exit;
    }

    $msg = $err['message'] ?? '';
    if (stripos($msg, 'ORA-28000') !== false) {
        $responseMsg = "Tài khoản đã bị khóa.";
    } elseif (stripos($msg, 'ORA-01017') !== false) {
        $responseMsg = "Sai tên đăng nhập hoặc mật khẩu.";
    } elseif (stripos($msg, 'ORA-12170') !== false) {
        $responseMsg = "Lỗi mạng: Timeout kết nối tới Database.";
    } else {
        $responseMsg = "Lỗi xác thực: " . $msg;
    }

    echo json_encode(["success" => false, "message" => $responseMsg]);
    exit;
}
oci_close($user_conn);

// --- 2. Xử lý Session ---
if (!$conn) {
    $e = oci_error();
    echo json_encode(["success" => false, "message" => "Lỗi hệ thống: Admin connection failed"]);
    exit;
}

try {
    $user_type = ($role === 'SYSDBA') ? 'ADMIN' : 'CUSTOMER';
    $session_user_id = 0;

    if ($user_type === 'CUSTOMER') {
        $sql_get = "SELECT MaKhachHang FROM KhachHang WHERE UPPER(ORACLE_USERNAME) = :uname";
        $stmt_get = oci_parse($conn, $sql_get);
        oci_bind_by_name($stmt_get, ':uname', $username);
        
        if (!oci_execute($stmt_get)) throw new Exception("Không lấy được thông tin khách hàng.");
        
        $rowGet = oci_fetch_assoc($stmt_get);
        $session_user_id = intval($rowGet['MAKHACHHANG'] ?? 0);
        
        if ($session_user_id <= 0) throw new Exception("Tài khoản chưa liên kết dữ liệu khách hàng.");
        oci_free_statement($stmt_get);
    }

    $token = bin2hex(random_bytes(32));

    // *** SỬA LỖI ORA-01745: Đổi tên bind variable để tránh từ khóa UID, SID ***
    $sql_insert = "INSERT INTO USER_SESSIONS 
                    (SESSION_ID, USER_ID, USER_TYPE, DEVICE_TYPE, DEVICE_INFO, LOGIN_TIME, IS_ACTIVE) 
                   VALUES 
                    (:b_sess_id, :b_user_id, :b_type, 'MOBILE', :b_info, SYSTIMESTAMP, 'Y')";
    
    $stmt_insert = oci_parse($conn, $sql_insert);
    
    // Bind với tên mới an toàn hơn
    oci_bind_by_name($stmt_insert, ':b_sess_id', $token);
    oci_bind_by_name($stmt_insert, ':b_user_id', $session_user_id);
    oci_bind_by_name($stmt_insert, ':b_type', $user_type);
    oci_bind_by_name($stmt_insert, ':b_info', $device_info);

    if (!oci_execute($stmt_insert, OCI_NO_AUTO_COMMIT)) {
        $e = oci_error($stmt_insert);
        throw new Exception("Lỗi tạo phiên: " . $e['message']);
    }

    // Cleanup phiên cũ (Cũng đổi tên biến bind)
    $sql_clean = "UPDATE USER_SESSIONS SET IS_ACTIVE='N' 
                  WHERE USER_ID = :b_cl_uid AND DEVICE_TYPE='MOBILE' AND SESSION_ID <> :b_cl_sid";
    
    $stmt_clean = oci_parse($conn, $sql_clean);
    oci_bind_by_name($stmt_clean, ':b_cl_uid', $session_user_id);
    oci_bind_by_name($stmt_clean, ':b_cl_sid', $token);
    @oci_execute($stmt_clean, OCI_NO_AUTO_COMMIT);

    oci_commit($conn);

    echo json_encode([
        "success"    => true,
        "message"    => "Đăng nhập thành công",
        "session_id" => $token,
        "userID"     => $session_user_id,
        "username"   => $username,
        "role"       => $role
    ]);

} catch (Exception $e) {
    oci_rollback($conn);
    echo json_encode(["success" => false, "message" => $e->getMessage()]);
} finally {
    if ($conn) oci_close($conn);
}
?>