<?php
header('Content-Type: application/json; charset=utf-8');
ini_set('display_errors', 0);

try {
    $oci = oci_connect('SYSTEM', 'quangson2002', 'localhost/ORCL', 'AL32UTF8');
    if (!$oci) {
        $e = oci_error();
        throw new Exception('Connection failed: ' . $e['message']);
    }

    $sql = 'SELECT MaChiNhanh, TenChiNhanh, DiaChi, SoDienThoai FROM ChiNhanh ORDER BY MaChiNhanh';
    $stmt = oci_parse($oci, $sql);
    if (!$stmt) {
        throw new Exception('Parse failed: ' . oci_error($oci)['message']);
    }

    if (!oci_execute($stmt, OCI_NO_AUTO_COMMIT)) {
        throw new Exception('Execute failed: ' . oci_error($stmt)['message']);
    }

    $branches = [];
    while ($row = oci_fetch_array($stmt, OCI_ASSOC)) {
        $branches[] = [
            'MaChiNhanh' => (int)$row['MACHINHАNH'],
            'TenChiNhanh' => $row['TENCHINHАNH'],
            'DiaChi' => $row['DIACHI'],
            'SoDienThoai' => $row['SODIENTHОAI']
        ];
    }

    oci_free_statement($stmt);
    oci_close($oci);

    echo json_encode($branches);
} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['error' => $e->getMessage()]);
}
?>
