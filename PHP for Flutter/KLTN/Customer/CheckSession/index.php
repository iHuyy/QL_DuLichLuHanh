<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/../../connect.php';
require_once __DIR__ . '/../../auth_middleware.php';

// Read token from Authorization header or parameters
$token = get_bearer_token_from_header();
if (!$token) {
    $data = json_decode(file_get_contents('php://input'), true);
    if (!empty($data['token'])) $token = $data['token'];
    if (!$token && !empty($_GET['token'])) $token = $_GET['token'];
}

if (!$token) {
    echo json_encode(['valid' => false]);
    exit;
}

// Ensure DB connection
if (empty($conn) || $conn === null) {
    $conn = connect_read();
}
if (!$conn) {
    http_response_code(500);
    echo json_encode(['valid' => false]);
    exit;
}

$sql = "SELECT IS_ACTIVE FROM USER_SESSIONS WHERE SESSION_ID = :session_id";
$stmt = @oci_parse($conn, $sql);
if (!$stmt) {
    echo json_encode(['valid' => false]);
    exit;
}
oci_bind_by_name($stmt, ':session_id', $token);
if (!@oci_execute($stmt)) {
    oci_free_statement($stmt);
    echo json_encode(['valid' => false]);
    exit;
}
$row = oci_fetch_assoc($stmt);
oci_free_statement($stmt);

if (!$row) {
    echo json_encode(['valid' => false]);
    exit;
}

$isActive = strtoupper(trim($row['IS_ACTIVE'] ?? 'N')) === 'Y';
echo json_encode(['valid' => $isActive]);
exit;

?>
