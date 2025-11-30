<?php
// KLTN/logout.php
require_once __DIR__ . '/auth_middleware.php'; // Đã include connect.php

try {
    $session = require_auth(); // Validate token
    $sessionId = $session['session_id'];

    check_db_connection();

    // Vô hiệu hóa session trong DB
    $sql = "UPDATE USER_SESSIONS SET IS_ACTIVE = 'N' WHERE SESSION_ID = :sid";
    $stmt = oci_parse($conn, $sql);
    oci_bind_by_name($stmt, ':sid', $sessionId);
    
    if (oci_execute($stmt)) {
        echo json_encode(['success' => true, 'message' => 'Đăng xuất thành công']);
    } else {
        echo json_encode(['success' => false, 'message' => 'Lỗi DB khi đăng xuất']);
    }

    oci_free_statement($stmt);
} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if($conn) oci_close($conn);
}
?>