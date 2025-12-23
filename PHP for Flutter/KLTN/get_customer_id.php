<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';

// Nhận username từ request
$data = json_decode(file_get_contents("php://input"), true);
$username = trim($data['username'] ?? '');

// Uppercase username để tương thích với Oracle (thường lưu trữ uppercase)
$username = strtoupper($username);

if (!$username) {
    echo json_encode(["success" => false, "message" => "Missing username"]);
    exit;
}

// ensure connection
if (empty($conn) || $conn === null) {
    $conn = connect_read();
}
if (!$conn) {
    http_response_code(500);
    echo json_encode(["success" => false, "message" => "DB connection failed"]);
    exit;
}

// Query để lấy MaKhachHang từ ORACLE_USERNAME
$sql = "SELECT MaKhachHang FROM KhachHang WHERE ORACLE_USERNAME = :username";
$stid = @oci_parse($conn, $sql);
if (!$stid) {
    $e = oci_error($conn);
    echo json_encode(["success" => false, "message" => "Parse error: " . ($e['message'] ?? '')]);
    close_conn($conn);
    exit;
}

oci_bind_by_name($stid, ":username", $username);
$ok = @oci_execute($stid);
if (!$ok) {
    $e = oci_error($stid) ?: oci_error($conn);
    echo json_encode(["success" => false, "message" => "Execute error: " . ($e['message'] ?? '')]);
    oci_free_statement($stid);
    close_conn($conn);
    exit;
}

$row = oci_fetch_assoc($stid);
oci_free_statement($stid);
close_conn($conn);

if (!$row) {
    // Không tìm thấy KhachHang với username này - trả lỗi rõ ràng
    echo json_encode([
        "success" => false,
        "message" => "Customer not found for username: $username. Ensure the customer has been registered in the KhachHang table with ORACLE_USERNAME = $username."
    ]);
    exit;
}

// Đảm bảo maKhachHang là số hợp lệ
$maKhachHang = intval($row['MAKHACHHANG'] ?? 0);
if ($maKhachHang <= 0) {
    echo json_encode([
        "success" => false,
        "message" => "Invalid MaKhachHang value: $maKhachHang. Database record may be corrupted."
    ]);
    exit;
}

echo json_encode([
    "success" => true,
    "maKhachHang" => $maKhachHang
]);
?>