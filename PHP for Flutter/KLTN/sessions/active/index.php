<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/../../auth_middleware.php';

try {
    $session = require_auth(); // will exit 401 if token missing/invalid/inactive

    $user_id = intval($session['user_id']);
    $user_type = $session['user_type'];

    global $conn;
    if (empty($conn) || $conn === null) {
        $conn = connect_read();
    }

    // Try to resolve the customer's ORACLE_USERNAME (if any). This allows us to
    // find sessions that belong to the same customer even when USER_ID storage
    // might differ slightly between clients.
    $oracleUsername = null;
    if ($user_id > 0) {
        $sql_user = "SELECT ORACLE_USERNAME FROM KhachHang WHERE MaKhachHang = :mk";
        $st_user = @oci_parse($conn, $sql_user);
        if ($st_user) {
            oci_bind_by_name($st_user, ':mk', $user_id);
            if (@oci_execute($st_user)) {
                $r = oci_fetch_assoc($st_user);
                if ($r && !empty($r['ORACLE_USERNAME'])) {
                    $oracleUsername = $r['ORACLE_USERNAME'];
                }
            }
            if (isset($st_user)) oci_free_statement($st_user);
        }
    }

    // Build a query that finds active sessions for this user's numeric ID OR for
    // any MaKhachHang that matches the same ORACLE_USERNAME (covers web logins).
    if ($oracleUsername) {
        $sql = "SELECT SESSION_ID, USER_ID, USER_TYPE, DEVICE_TYPE, DEVICE_INFO, LOGIN_TIME, IS_ACTIVE
                FROM USER_SESSIONS
                WHERE IS_ACTIVE = 'Y' AND (USER_ID = :user_id OR USER_ID IN (SELECT MaKhachHang FROM KhachHang WHERE ORACLE_USERNAME = :oracle))
                ORDER BY LOGIN_TIME DESC";
    } else {
        $sql = "SELECT SESSION_ID, USER_ID, USER_TYPE, DEVICE_TYPE, DEVICE_INFO, LOGIN_TIME, IS_ACTIVE
                FROM USER_SESSIONS
                WHERE USER_ID = :user_id AND IS_ACTIVE = 'Y' ORDER BY LOGIN_TIME DESC";
    }

    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) throw new Exception('Failed to prepare query');
    oci_bind_by_name($stmt, ':user_id', $user_id);
    if ($oracleUsername) oci_bind_by_name($stmt, ':oracle', $oracleUsername);
    if (!@oci_execute($stmt)) {
        oci_free_statement($stmt);
        throw new Exception('Failed to execute query');
    }

    $rows = [];
    while ($r = oci_fetch_assoc($stmt)) {
        $rows[] = [
            'SESSION_ID' => $r['SESSION_ID'],
            'USER_ID'    => intval($r['USER_ID']),
            'USER_TYPE'  => $r['USER_TYPE'],
            'DEVICE_TYPE'=> $r['DEVICE_TYPE'],
            'DEVICE_INFO'=> $r['DEVICE_INFO'],
            'LOGIN_TIME' => $r['LOGIN_TIME'],
            'IS_ACTIVE'  => $r['IS_ACTIVE']
        ];
    }
    oci_free_statement($stmt);

    echo json_encode($rows);
} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
}

?>
