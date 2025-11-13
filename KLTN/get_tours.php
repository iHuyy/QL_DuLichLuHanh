<?php
header('Content-Type: application/json');
include 'connect.php';

$query = 'SELECT MaTour, TieuDe, MoTa, NoiKhoiHanh, NoiDen, ThanhPho, ThoiGian, GiaNguoiLon, GiaTreEm, SoLuong, ChiNhanh FROM Tour';

$stid = oci_parse($conn, $query);
oci_execute($stid);

$tours = array();
while ($row = oci_fetch_array($stid, OCI_ASSOC+OCI_RETURN_NULLS)) {
    $tours[] = $row;
}

echo json_encode($tours);

oci_free_statement($stid);
oci_close($conn);
