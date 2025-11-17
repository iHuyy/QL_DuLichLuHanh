<?php
header('Content-Type: application/json; charset=utf-8');
ini_set('display_errors', 0);

try {
    $conn = oci_connect('SYSTEM', 'quangson2002', 'localhost/ORCL', 'AL32UTF8');
    if (!$conn) {
        http_response_code(500);
        echo json_encode(['error' => 'Connection failed']);
        exit;
    }

    $sql = 'SELECT MaChiNhanh, TenChiNhanh, DiaChi, SoDienThoai FROM ChiNhanh ORDER BY MaChiNhanh';
    $stmt = oci_parse($conn, $sql);
    oci_execute($stmt, OCI_NO_AUTO_COMMIT);

    $branches = array();
    while ($row = oci_fetch_array($stmt, OCI_ASSOC + OCI_RETURN_NULLS)) {
        $branches[] = array(
            'MaChiNhanh' => intval($row['MACHINHАNH']),
            'TenChiNhanh' => $row['TENCHINHАNH'],
            'DiaChi' => $row['DIACHI'],
            'SoDienThoai' => $row['SODIENTHОAI']
        );
    }

    oci_free_statement($stmt);
    oci_close($conn);

    echo json_encode($branches);

} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(array('error' => $e->getMessage()));
}
?>
