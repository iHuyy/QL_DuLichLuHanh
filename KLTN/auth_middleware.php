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
    global $conn; // Dùng kết nối toàn cục từ connect.php
    
    // Kiểm tra kết nối
    if (!$conn) {
        // Nếu chưa có kết nối, thử include lại (đề phòng)
        require_once __DIR__ . '/connect.php';
    }
    if (!$conn) {
        unauthorized_json_and_exit('Lỗi kết nối Database (Auth Middleware)');
    }

    $token = get_bearer_token_from_header();
    // Hỗ trợ lấy token từ POST/GET để test dễ hơn
    if (!$token) $token = $_REQUEST['token'] ?? null;

    if (!$token) {
        unauthorized_json_and_exit('Thiếu Token xác thực');
    }

    $sql = "SELECT SESSION_ID, USER_ID, USER_TYPE, DEVICE_TYPE, DEVICE_INFO, LOGIN_TIME, IS_ACTIVE 
            FROM USER_SESSIONS WHERE SESSION_ID = :session_id";
    
    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) unauthorized_json_and_exit('Lỗi chuẩn bị truy vấn xác thực');
    
    oci_bind_by_name($stmt, ':session_id', $token);
    
    if (!@oci_execute($stmt)) {
        oci_free_statement($stmt);
        unauthorized_json_and_exit('Lỗi thực thi truy vấn xác thực');
    }
    
    $row = oci_fetch_assoc($stmt);
    oci_free_statement($stmt);

    if (!$row) {
        unauthorized_json_and_exit('Token không hợp lệ hoặc không tìm thấy phiên');
    }

    $isActive = strtoupper(trim($row['IS_ACTIVE'] ?? 'N')) === 'Y';
    if (!$isActive) {
        unauthorized_json_and_exit('Phiên đăng nhập đã hết hạn hoặc bị hủy');
    }

    // Trả về thông tin session
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