<?php
// KLTN/get_branches.php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';
// require_once __DIR__ . '/auth_middleware.php'; // <-- BỎ DÒNG NÀY (Không cần auth cho dữ liệu công khai)

try {
    // $session = require_auth(); // <-- BỎ DÒNG NÀY (Để ai cũng lấy được danh sách chi nhánh)

    check_db_connection();

    // Truy vấn danh sách chi nhánh
    $sql = 'SELECT MACHINHANH, TENCHINHANH, DIACHI, SODIENTHOAI FROM CHINHANH ORDER BY TENCHINHANH'; // Sắp xếp theo tên cho đẹp
    $stmt = oci_parse($conn, $sql);
    
    if (!$stmt) {
        // Nếu lỗi parse, ném exception để catch bên dưới xử lý
        $e = oci_error($conn);
        throw new Exception('Parse failed: ' . $e['message']);
    }

    if (!@oci_execute($stmt)) {
        $e = oci_error($stmt);
        throw new Exception('Execute failed: ' . $e['message']);
    }

    $branches = [];
    while ($row = oci_fetch_assoc($stmt)) {
        $branches[] = [
            'MaChiNhanh' => (int)($row['MACHINHANH'] ?? 0),
            'TenChiNhanh' => (string)($row['TENCHINHANH'] ?? ''),
            'DiaChi' => (string)($row['DIACHI'] ?? ''),
            'SoDienThoai' => (string)($row['SODIENTHOAI'] ?? '')
        ];
    }

    oci_free_statement($stmt);

    // Trả về JSON dạng Mảng (List) -> Flutter sẽ đọc được
    echo json_encode($branches, JSON_UNESCAPED_UNICODE);

} catch (Exception $e) {
    // Nếu có lỗi, trả về mảng rỗng hoặc object lỗi (Flutter hiện tại đang catch và trả về mảng rỗng)
    http_response_code(500);
    echo json_encode(['error' => $e->getMessage()]);
} finally {
    if (isset($conn) && $conn) oci_close($conn);
}
?>