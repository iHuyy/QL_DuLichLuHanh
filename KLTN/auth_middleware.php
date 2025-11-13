<?php
// auth_middleware.php
// Provides functions to validate Bearer token stored in USER_SESSIONS
require_once __DIR__ . '/connect.php';

function get_bearer_token_from_header() {
    $headers = null;
    if (function_exists('getallheaders')) {
        $headers = getallheaders();
    } else {
        // Fallback for environments without getallheaders
        $headers = [];
        foreach ($_SERVER as $name => $value) {
            if (substr($name, 0, 5) == 'HTTP_') {
                $headers[str_replace(' ', '-', ucwords(str_replace('_', ' ', strtolower(substr($name, 5)))))] = $value;
            }
        }
    }

    $auth = null;
    if (isset($headers['Authorization'])) {
        $auth = $headers['Authorization'];
    } elseif (isset($headers['authorization'])) {
        $auth = $headers['authorization'];
    } elseif (isset($_SERVER['HTTP_AUTHORIZATION'])) {
        $auth = $_SERVER['HTTP_AUTHORIZATION'];
    }

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

/**
 * require_auth()
 * Validates Authorization: Bearer {token} against USER_SESSIONS table.
 * Returns associative array with session info on success.
 * On failure, sends 401 JSON and exits.
 */
function require_auth() {
    global $conn;
    // ensure connect.php provided a global connection or create one
    if (empty($conn) || $conn === null) {
        $conn = connect_read();
    }
    if (!$conn) {
        unauthorized_json_and_exit('DB connection failed');
    }

    $token = get_bearer_token_from_header();
    if (!$token) {
        // Also accept token via POST/GET for convenience if client cannot set header
        if (!empty($_POST['token'])) $token = $_POST['token'];
        if (!$token && !empty($_GET['token'])) $token = $_GET['token'];
    }
    if (!$token) {
        unauthorized_json_and_exit('Missing token');
    }

    $sql = "SELECT SESSION_ID, USER_ID, USER_TYPE, DEVICE_TYPE, DEVICE_INFO, LOGIN_TIME, IS_ACTIVE FROM USER_SESSIONS WHERE SESSION_ID = :session_id";
    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) {
        unauthorized_json_and_exit('Failed to prepare auth query');
    }
    oci_bind_by_name($stmt, ':session_id', $token);
    if (!@oci_execute($stmt)) {
        oci_free_statement($stmt);
        unauthorized_json_and_exit('Auth query failed');
    }
    $row = oci_fetch_assoc($stmt);
    oci_free_statement($stmt);

    if (!$row) {
        unauthorized_json_and_exit('Invalid token');
    }

    // Check IS_ACTIVE
    $isActive = strtoupper(trim($row['IS_ACTIVE'] ?? 'N')) === 'Y';
    if (!$isActive) {
        unauthorized_json_and_exit('Session inactive');
    }

    // Success: return session info
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
