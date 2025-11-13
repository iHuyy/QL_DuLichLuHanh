<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/auth_middleware.php';

try {
    // require_auth will validate the bearer token and return session info
    $session = require_auth();
    $sessionId = $session['session_id'];

    global $conn;
    if (empty($conn) || $conn === null) {
        $conn = connect_read();
    }

    $sql = "UPDATE USER_SESSIONS SET IS_ACTIVE = 'N' WHERE SESSION_ID = :session_id AND IS_ACTIVE = 'Y'";
    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) throw new Exception('Failed to prepare update');
    oci_bind_by_name($stmt, ':session_id', $sessionId);
    if (!@oci_execute($stmt, OCI_NO_AUTO_COMMIT)) {
        oci_free_statement($stmt);
        throw new Exception('Failed to execute update');
    }
    oci_commit($conn);
    oci_free_statement($stmt);

    echo json_encode(['success' => true, 'message' => 'Logged out']);
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
