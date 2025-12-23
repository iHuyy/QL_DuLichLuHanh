<?php
// KLTN/auth_middleware.php
require_once __DIR__ . '/connect.php';

function get_bearer_token_from_header() {
    $headers = null;
    if (function_exists('getallheaders')) {
        $headers = getallheaders();
    } else {
        $headers = [];
        foreach ($_SERVER as $name => $value) {
            if (substr($name, 0, 5) == 'HTTP_') {
                $headers[str_replace(' ', '-', ucwords(str_replace('_', ' ', strtolower(substr($name, 5)))))] = $value;
            }
        }
    }
    
    $auth = $headers['Authorization'] ?? $headers['authorization'] ?? $_SERVER['HTTP_AUTHORIZATION'] ?? null;
    if (!$auth) return null;
    if (preg_match('/Bearer\s+(.*)$/i', $auth, $matches)) {
        return trim($matches[1]);
    }
    return null;
}

function unauthorized_json_and_exit($message = 'Unauthorized') {
    http_response_code(401);
    echo json_encode(["success" => false, "message" => $message]);
    exit;
}

function require_auth() {
    global $conn;
    if (!$conn) require_once __DIR__ . '/connect.php';

    $token = get_bearer_token_from_header();
    if (!$token) $token = $_REQUEST['token'] ?? null;

    if (!$token) {
        unauthorized_json_and_exit('Thiếu Token xác thực');
    }

    // Lấy thông tin Session và LAST_ACTIVITY
    $sql = "SELECT SESSION_ID, USER_ID, USER_TYPE, DEVICE_TYPE, DEVICE_INFO, LOGIN_TIME, IS_ACTIVE, 
            to_char(LAST_ACTIVITY, 'YYYY-MM-DD HH24:MI:SS') as LAST_ACT_STR
            FROM USER_SESSIONS WHERE SESSION_ID = :session_id";
    
    $stmt = @oci_parse($conn, $sql);
    oci_bind_by_name($stmt, ':session_id', $token);
    
    if (!@oci_execute($stmt)) {
        unauthorized_json_and_exit('Lỗi thực thi truy vấn xác thực');
    }
    
    $row = oci_fetch_assoc($stmt);
    oci_free_statement($stmt);

    if (!$row) {
        unauthorized_json_and_exit('Token không hợp lệ hoặc không tìm thấy phiên');
    }

    // 1. Kiểm tra trạng thái Active
    $isActive = strtoupper(trim($row['IS_ACTIVE'] ?? 'N')) === 'Y';
    if (!$isActive) {
        unauthorized_json_and_exit('Phiên đăng nhập đã hết hạn hoặc bị hủy');
    }

    // 2. [MỚI] Kiểm tra IDLE TIME (2 Phút)
    $lastActivity = strtotime($row['LAST_ACT_STR']);
    $now = time();
    $diffMinutes = ($now - $lastActivity) / 60;

    // Cấu hình: Quá 2 phút không gọi API -> Logout
    if ($diffMinutes > 2) {
        // Cập nhật DB thành Inactive
        $sqlUpd = "UPDATE USER_SESSIONS SET IS_ACTIVE = 'N' WHERE SESSION_ID = :sid";
        $sUpd = oci_parse($conn, $sqlUpd);
        oci_bind_by_name($sUpd, ':sid', $token);
        oci_execute($sUpd, OCI_COMMIT_ON_SUCCESS);
        oci_free_statement($sUpd);

        unauthorized_json_and_exit('Phiên làm việc hết hạn do không hoạt động quá 2 phút.');
    }

    // 3. Cập nhật lại thời gian hoạt động (Touch)
    $sqlTouch = "UPDATE USER_SESSIONS SET LAST_ACTIVITY = SYSTIMESTAMP WHERE SESSION_ID = :sid";
    $sTouch = oci_parse($conn, $sqlTouch);
    oci_bind_by_name($sTouch, ':sid', $token);
    oci_execute($sTouch, OCI_COMMIT_ON_SUCCESS);
    oci_free_statement($sTouch);

    return [
        'session_id' => $row['SESSION_ID'],
        'user_id' => $row['USER_ID'],
        'user_type' => $row['USER_TYPE'],
        'device_type' => $row['DEVICE_TYPE'],
        'device_info' => $row['DEVICE_INFO'],
        'login_time' => $row['LOGIN_TIME'],
    ];
}
?>