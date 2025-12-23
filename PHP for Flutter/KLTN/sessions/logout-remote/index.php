<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/../../auth_middleware.php';

try {
    $session = require_auth(); // will exit 401 if token invalid/inactive

    $data = json_decode(file_get_contents('php://input'), true);
    $targetSession = trim($data['session_id_to_logout'] ?? '');
    if (!$targetSession) {
        echo json_encode(['success' => false, 'message' => 'Missing target session id']);
        exit;
    }

    $user_id = intval($session['user_id']);
    $user_type = $session['user_type'];

    global $conn;
    if (empty($conn) || $conn === null) {
        $conn = connect_read();
    }

    // Resolve ORACLE_USERNAME for the current user (if any)
    $oracleUsername = null;
    if ($user_id > 0) {
        $sql_user = "SELECT ORACLE_USERNAME FROM KhachHang WHERE MaKhachHang = :mk";
        $st_user = @oci_parse($conn, $sql_user);
        if ($st_user) {
            oci_bind_by_name($st_user, ':mk', $user_id);
            if (@oci_execute($st_user)) {
                $r = oci_fetch_assoc($st_user);
                if ($r && !empty($r['ORACLE_USERNAME'])) $oracleUsername = $r['ORACLE_USERNAME'];
            }
            if (isset($st_user)) oci_free_statement($st_user);
        }
    }

    // Ensure the target session belongs to the same customer: either same USER_ID
    // or belongs to a KhachHang with the same ORACLE_USERNAME.
    $sql_check = "SELECT USER_ID, IS_ACTIVE FROM USER_SESSIONS WHERE SESSION_ID = :session_id";
    $stmt_check = @oci_parse($conn, $sql_check);
    if (!$stmt_check) throw new Exception('Failed to prepare check query');
    oci_bind_by_name($stmt_check, ':session_id', $targetSession);
    if (!@oci_execute($stmt_check)) { oci_free_statement($stmt_check); throw new Exception('Failed to execute check query'); }
    $row = oci_fetch_assoc($stmt_check);
    oci_free_statement($stmt_check);
    if (!$row) {
        echo json_encode(['success' => false, 'message' => 'Session not found']);
        exit;
    }

    $targetUserId = intval($row['USER_ID']);
    $permitted = false;
    if ($targetUserId === $user_id) {
        $permitted = true;
    } elseif ($oracleUsername && $targetUserId > 0) {
        // check if targetUserId belongs to same ORACLE_USERNAME
        $sql_owner = "SELECT ORACLE_USERNAME FROM KhachHang WHERE MaKhachHang = :mk";
        $st_owner = @oci_parse($conn, $sql_owner);
        if ($st_owner) {
            oci_bind_by_name($st_owner, ':mk', $targetUserId);
            if (@oci_execute($st_owner)) {
                $r2 = oci_fetch_assoc($st_owner);
                if ($r2 && !empty($r2['ORACLE_USERNAME']) && $r2['ORACLE_USERNAME'] === $oracleUsername) {
                    $permitted = true;
                }
            }
            if (isset($st_owner)) oci_free_statement($st_owner);
        }
    }

    if (!$permitted) {
        echo json_encode(['success' => false, 'message' => 'Permission denied']);
        exit;
    }

    // Mark target session inactive
    $sql_update = "UPDATE USER_SESSIONS SET IS_ACTIVE = 'N' WHERE SESSION_ID = :session_id AND USER_ID = :user_id AND USER_TYPE = :user_type AND IS_ACTIVE = 'Y'";
    $stmt_update = @oci_parse($conn, $sql_update);
    if (!$stmt_update) throw new Exception('Failed to prepare update');
    oci_bind_by_name($stmt_update, ':session_id', $targetSession);
    oci_bind_by_name($stmt_update, ':user_id', $user_id);
    oci_bind_by_name($stmt_update, ':user_type', $user_type);
    if (!@oci_execute($stmt_update, OCI_NO_AUTO_COMMIT)) {
        oci_free_statement($stmt_update);
        throw new Exception('Failed to deactivate session');
    }
    oci_commit($conn);
    oci_free_statement($stmt_update);

    echo json_encode(['success' => true]);

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
