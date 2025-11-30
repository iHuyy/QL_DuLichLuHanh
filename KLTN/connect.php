<?php
// KLTN/connect.php

// Đảm bảo response luôn là JSON và UTF-8
header("Content-Type: application/json; charset=utf-8");
header("Access-Control-Allow-Origin: *"); // Cho phép Flutter gọi API
header("Access-Control-Allow-Methods: POST, GET, OPTIONS");

// Start session nếu chưa có
if (session_status() === PHP_SESSION_NONE) {
    session_start();
}

// =================================================================
// CẤU HÌNH DATABASE (SỬA IP/PORT Ở ĐÂY)
// =================================================================

// 1. Chuỗi kết nối (Connection String)
// Sửa HOST thành IP mới khi cần thay đổi.
define('ORACLE_CONN_STR', "(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=100.91.47.90)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCLPDB1)))");

// 2. Tài khoản Admin (Dùng để query dữ liệu chung, ghi session, v.v.)
define('SYS_DBA_USER', 'tAdmin'); 
define('SYS_DBA_PASS', '123456');

// 3. Bảng mã
define('ORACLE_CHARSET', 'AL32UTF8');

// =================================================================
// KHỞI TẠO KẾT NỐI TOÀN CỤC ($conn)
// =================================================================

// Tạo biến kết nối toàn cục $conn dùng cho các file include file này
$conn = @oci_connect(SYS_DBA_USER, SYS_DBA_PASS, ORACLE_CONN_STR, ORACLE_CHARSET);

// Hàm kiểm tra kết nối (các file khác có thể gọi để check)
function check_db_connection() {
    global $conn;
    if (!$conn) {
        $e = oci_error();
        echo json_encode([
            "success" => false, 
            "message" => "Lỗi kết nối Database trung tâm: " . ($e['message'] ?? 'Unknown error')
        ]);
        exit;
    }
}

/**
 * Hàm tạo kết nối riêng (Dùng khi cần login bằng user/pass khác, ví dụ lúc đăng nhập)
 */
function connect_custom($user, $pass, $role = 'DEFAULT') {
    if (strtoupper($role) === 'SYSDBA') {
        return @oci_connect($user, $pass, ORACLE_CONN_STR, ORACLE_CHARSET, OCI_SYSDBA);
    }
    return @oci_connect($user, $pass, ORACLE_CONN_STR, ORACLE_CHARSET);
}
?>