<?php
// connect.php
// File này cung cấp hàm kết nối tới Oracle với cấu hình giống file gốc của bạn.
// Không thay đổi conn_str hay kiểu kết nối (OCI_DEFAULT) — chỉ gom cấu hình vào 1 nơi.

// ensure responses are JSON by default; individual scripts may override
header("Content-Type: application/json; charset=utf-8");

// start session if not started
if (session_status() === PHP_SESSION_NONE) {
    session_start();
}

// Default read account (fallback) and SYS/ADMIN creds via env or hardcode
define('SYS_DBA_USER', getenv('SYS_DBA_USER') ?: 'tAdmin');
define('SYS_DBA_PASS', getenv('SYS_DBA_PASS') ?: '123456');

// Connection string (change here when host/port/service changes)
$default_conn_str = "(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=100.91.47.90)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCLPDB1)))";
define('ORACLE_CONN_STR', getenv('ORACLE_CONN_STR') ?: $default_conn_str);

// Charset
define('ORACLE_CHARSET', getenv('ORACLE_CHARSET') ?: "AL32UTF8");

// Try to create a default global $conn for simple scripts (non-privileged)
$conn = @oci_connect(SYS_DBA_USER, SYS_DBA_PASS, ORACLE_CONN_STR, ORACLE_CHARSET);
if (!$conn) {
    // keep $conn as null; callers should handle error and return JSON
    $conn = null;
}

/**
 * connect_admin(): returns a connection intended for admin tasks.
 * Tries privileged connect (OCI_SYSDBA) first, then falls back to normal connect.
 * Returns oci connection resource or null on failure.
 */
function connect_admin() {
    $user = SYS_DBA_USER;
    $pass = SYS_DBA_PASS;

    // Try privileged connect if PHP/OCI8 configured and server allows it
    $c = @oci_connect($user, $pass, ORACLE_CONN_STR, ORACLE_CHARSET, OCI_SYSDBA);
    if ($c) return $c;

    // Fallback to normal connection
    $c2 = @oci_connect($user, $pass, ORACLE_CONN_STR, ORACLE_CHARSET);
    return $c2 ?: null;
}

/**
 * connect_read(): get connection for reading data.
 * If dbUser/dbPass provided, use them; otherwise use default SYS_DBA_USER.
 * role = 'SYSDBA' will attempt privileged connect.
 */
function connect_read($dbUser = null, $dbPass = null, $role = 'DEFAULT') {
    $user = $dbUser ?? SYS_DBA_USER;
    $pass = $dbPass ?? SYS_DBA_PASS;

    if (strtoupper($role) === 'SYSDBA') {
        $c = @oci_connect($user, $pass, ORACLE_CONN_STR, ORACLE_CHARSET, OCI_SYSDBA);
        if ($c) return $c;
        // try normal if privileged not available
    }
    $c2 = @oci_connect($user, $pass, ORACLE_CONN_STR, ORACLE_CHARSET);
    return $c2 ?: null;
}

/** safe close */
function close_conn($c) {
    if ($c) @oci_close($c);
}
?>
