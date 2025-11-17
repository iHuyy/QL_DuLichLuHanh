<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';

// Lấy param (POST JSON hoặc GET maTour)
$data = json_decode(file_get_contents("php://input"), true);
$maTour = null;
if (!empty($data['maTour'])) $maTour = trim($data['maTour']);
if (!$maTour && isset($_GET['maTour'])) $maTour = trim($_GET['maTour']);

if (!$maTour) {
    http_response_code(400);
    echo json_encode(["success" => false, "message" => "Missing maTour"]);
    exit;
}

// ensure we have connection
if (empty($conn) || $conn === null) {
    $conn = connect_read();
}
if (!$conn) {
    http_response_code(500);
    echo json_encode(["success" => false, "message" => "DB connection failed"]);
    exit;
}

$sql = "SELECT MaTour, TieuDe, MoTa, NoiKhoiHanh, NoiDen, ThanhPho, ThoiGian, GiaNguoiLon, GiaTreEm, SoLuong, ChiNhanh, DuLieuAnh, LoaiAnh FROM Tour WHERE MaTour = :id";
$stid = @oci_parse($conn, $sql);
if (!$stid) {
    $e = oci_error($conn);
    http_response_code(500);
    echo json_encode(["success" => false, "message" => $e['message'] ?? 'Parse error']);
    close_conn($conn);
    exit;
}

oci_bind_by_name($stid, ":id", $maTour);
$ok = @oci_execute($stid);
if (!$ok) {
    $e = oci_error($stid) ?: oci_error($conn);
    http_response_code(500);
    echo json_encode(["success" => false, "message" => $e['message'] ?? 'Execute error']);
    oci_free_statement($stid);
    close_conn($conn);
    exit;
}

$row = oci_fetch_assoc($stid);
if (!$row) {
    echo json_encode(["success" => false, "message" => "Tour not found"]);
    oci_free_statement($stid);
    close_conn($conn);
    exit;
}

// Normalize keys to uppercase for client
$row = array_change_key_case($row, CASE_UPPER);

// Convert BLOB to base64 data URL
if (isset($row['DULIEUANH']) && $row['DULIEUANH'] !== null && $row['DULIEUANH'] !== '') {
    try {
        $blob = $row['DULIEUANH'];
        $data = null;
        if (is_object($blob) && method_exists($blob, 'load')) {
            $data = $blob->load();
        } else if (is_resource($blob)) {
            $data = stream_get_contents($blob);
        } else {
            $data = $blob;
        }
        if ($data !== null && $data !== '') {
            $b64 = base64_encode($data);
            $mime = isset($row['LOAIANH']) && $row['LOAIANH'] !== '' ? trim($row['LOAIANH']) : 'image/jpeg';
            $row['DULIEUANH'] = 'data:' . $mime . ';base64,' . $b64;
        }
    } catch (Exception $e) {
        // on error, leave as-is
    }
}

echo json_encode(["success" => true, "tour" => $row]);

oci_free_statement($stid);
close_conn($conn);
?>