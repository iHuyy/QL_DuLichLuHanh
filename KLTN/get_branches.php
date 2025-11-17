<?php
require_once 'connect.php';
require_once __DIR__ . '/auth_middleware.php'; // (THÊM)

try {
    $session = require_auth(); // (THÊM - Buộc xác thực)

    if (!$conn) {
        $e = oci_error();
        throw new Exception('Connection failed: ' . ($e['message'] ?? 'Unknown error'));
    }

    // Use Latin characters for table/column names (no Cyrillic).
    $sql = 'SELECT MACHINHANH, TENCHINHANH, DIACHI, SODIENTHOAI FROM CHINHANH ORDER BY MACHINHANH';
    $stmt = oci_parse($conn, $sql);
    if (!$stmt) {
        throw new Exception('Parse failed: ' . oci_error($conn)['message']);
    }

    if (!oci_execute($stmt)) {
        $err = oci_error($stmt) ?: oci_error($conn);
        throw new Exception('Execute failed: ' . ($err['message'] ?? 'Unknown error'));
    }

    $branches = [];
    // use oci_fetch_assoc for predictable associative keys (uppercase)
    while ($row = oci_fetch_assoc($stmt)) {
        $branches[] = [
            'MaChiNhanh' => (int)($row['MACHINHANH'] ?? 0),
            'TenChiNhanh' => (string)($row['TENCHINHANH'] ?? ''),
            'DiaChi' => (string)($row['DIACHI'] ?? ''),
            'SoDienThoai' => (string)($row['SODIENTHOAI'] ?? '')
        ];
    }

    oci_free_statement($stmt);

    echo json_encode($branches, JSON_UNESCAPED_UNICODE);
} catch (Exception $e) {
    // (THÊM) Bắt lỗi 401 hoặc lỗi 500
    // Mã 401 sẽ tự động được đặt bởi auth_middleware.php
    echo json_encode(['error' => $e->getMessage()]);
} finally {
    close_conn($conn);
}
?>