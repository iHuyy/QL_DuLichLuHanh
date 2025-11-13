<?php
// Không in warnings/HTML lỗi ra client
ini_set('display_errors', 0);
error_reporting(E_ALL);

header("Content-Type: application/json");
session_start();

$data = json_decode(file_get_contents("php://input"), true);

// Lấy credentials/role ưu tiên: request -> session -> default (không đủ -> lỗi)
$reqDbUser = isset($data['dbUser']) ? trim($data['dbUser']) : null;
$reqDbPass = isset($data['dbPass']) ? trim($data['dbPass']) : null;
$reqRole   = isset($data['role']) ? strtoupper(trim($data['role'])) : null;

$sessionUser = !empty($_SESSION['sys_user']) ? $_SESSION['sys_user'] : null;
$sessionPass = !empty($_SESSION['sys_pass']) ? $_SESSION['sys_pass'] : null;
$sessionIsSys = !empty($_SESSION['is_sysdba']) && $_SESSION['is_sysdba'] === true;

$dbUser = $reqDbUser ?? $sessionUser;
$dbPass = $reqDbPass ?? $sessionPass;
$role   = $reqRole ?? ($sessionIsSys ? 'SYSDBA' : 'DEFAULT');

if (!$dbUser || !$dbPass) {
    echo json_encode(["success" => false, "message" => "Missing DB credentials"]);
    exit;
}

$conn_str = "(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=100.91.47.90)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCLPDB1)))";

// Thử kết nối (SYSDBA nếu được yêu cầu)
if ($role === 'SYSDBA') {
    $conn = @oci_connect($dbUser, $dbPass, $conn_str, "AL32UTF8", OCI_SYSDBA);
} else {
    $conn = @oci_connect($dbUser, $dbPass, $conn_str, "AL32UTF8");
}

if (!$conn) {
    $e = oci_error();
    $msg = isset($e['message']) ? $e['message'] : 'Connection failed';
    echo json_encode(["success" => false, "message" => $msg, "role" => $role]);
    exit;
}

// Nếu kết nối thành công, chỉ trả tên tài khoản và role (không trả mật khẩu)
echo json_encode([
    "success" => true,
    "username" => $dbUser,
    "role" => $role
]);

oci_close($conn);
exit;
?>