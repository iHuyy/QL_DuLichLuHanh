<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/../../auth_middleware.php';

try {
    $session = require_auth(); // validate current bearer
    $currentUserId = intval($session['user_id']);

    $data = json_decode(file_get_contents('php://input'), true);
    $targetSession = trim($data['session_id_to_logout'] ?? '');
    if (!$targetSession) {
        echo json_encode(['success' => false, 'message' => 'Missing target session id']);
        exit;
    }

    global $conn;
    if (empty($conn) || $conn === null) $conn = connect_read();

    // Resolve current user's ORACLE_USERNAME if possible
    $oracleUsername = null;
    if ($currentUserId > 0) {
        $sql_u = "SELECT ORACLE_USERNAME FROM KhachHang WHERE MaKhachHang = :mk";
        $st_u = @oci_parse($conn, $sql_u);
        if ($st_u) {
            oci_bind_by_name($st_u, ':mk', $currentUserId);
            if (@oci_execute($st_u)) {
                $r = oci_fetch_assoc($st_u);
                if ($r && !empty($r['ORACLE_USERNAME'])) $oracleUsername = $r['ORACLE_USERNAME'];
            }
            if (isset($st_u)) oci_free_statement($st_u);
        }
    }

    // Update: set IS_ACTIVE='N' where SESSION_ID matches and either USER_ID matches
    // or USER_ID belongs to a KhachHang with same ORACLE_USERNAME (case-insensitive)
    if ($oracleUsername) {
        $sql = "UPDATE USER_SESSIONS SET IS_ACTIVE = 'N' WHERE SESSION_ID = :session_id AND IS_ACTIVE = 'Y' AND (USER_ID = :user_id OR USER_ID IN (SELECT MaKhachHang FROM KhachHang WHERE UPPER(ORACLE_USERNAME) = UPPER(:oracle)))";
    } else {
        $sql = "UPDATE USER_SESSIONS SET IS_ACTIVE = 'N' WHERE SESSION_ID = :session_id AND IS_ACTIVE = 'Y' AND USER_ID = :user_id";
    }

    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) throw new Exception('Failed to prepare update');
    oci_bind_by_name($stmt, ':session_id', $targetSession);
    oci_bind_by_name($stmt, ':user_id', $currentUserId);
    if ($oracleUsername) oci_bind_by_name($stmt, ':oracle', $oracleUsername);

    if (!@oci_execute($stmt, OCI_NO_AUTO_COMMIT)) {
        oci_free_statement($stmt);
        throw new Exception('Failed to execute update');
    }
    oci_commit($conn);
    oci_free_statement($stmt);

    echo json_encode(['success' => true, 'message' => 'Session deactivated']);
} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    // CRITICAL: Close Oracle connection to free up session slot and avoid SESSIONS_PER_USER limit
    global $conn;
    if ($conn !== null && is_resource($conn)) {
        oci_close($conn);
        $conn = null;
    }
}

?>
