<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/../../auth_middleware.php';

// Debug endpoint: list active sessions for a given ORACLE_USERNAME (case-insensitive).
// Accepts 'username' via GET or JSON body. Requires a valid Authorization header.
try {
    $session = require_auth();

    $data = json_decode(file_get_contents('php://input'), true);
    $username = trim($data['username'] ?? $_GET['username'] ?? '');
    $username = strtoupper($username);

    if (!$username) {
        echo json_encode(['success' => false, 'message' => 'Missing username']);
        exit;
    }

    global $conn;
    if (empty($conn) || $conn === null) $conn = connect_read();

    // Find MaKhachHang for the username
    $sql = "SELECT MaKhachHang FROM KhachHang WHERE UPPER(ORACLE_USERNAME) = :username";
    $stid = @oci_parse($conn, $sql);
    if (!$stid) { echo json_encode(['success'=>false,'message'=>'parse failed']); exit; }
    oci_bind_by_name($stid, ':username', $username);
    if (!@oci_execute($stid)) { oci_free_statement($stid); echo json_encode(['success'=>false,'message'=>'execute failed']); exit; }
    $r = oci_fetch_assoc($stid);
    oci_free_statement($stid);
    if (!$r) { echo json_encode(['success'=>false,'message'=>'no customer']); exit; }
    $mk = intval($r['MAKHACHHANG']);

    // Return sessions for this MaKhachHang
    $sql2 = "SELECT SESSION_ID, USER_ID, USER_TYPE, DEVICE_TYPE, DEVICE_INFO, LOGIN_TIME, IS_ACTIVE FROM USER_SESSIONS WHERE USER_ID = :mk ORDER BY LOGIN_TIME DESC";
    $st2 = @oci_parse($conn, $sql2);
    if (!$st2) { echo json_encode(['success'=>false,'message'=>'parse2 failed']); exit; }
    oci_bind_by_name($st2, ':mk', $mk);
    if (!@oci_execute($st2)) { oci_free_statement($st2); echo json_encode(['success'=>false,'message'=>'execute2 failed']); exit; }
    $rows = [];
    while ($row = oci_fetch_assoc($st2)) {
        $rows[] = $row;
    }
    oci_free_statement($st2);

    echo json_encode(['success'=>true,'maKhachHang'=>$mk,'username'=>$username,'sessions'=>$rows]);
} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success'=>false,'message'=>$e->getMessage()]);
}

?>
