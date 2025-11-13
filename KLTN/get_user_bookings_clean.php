<?php
/**
 * File: get_user_bookings.php
 * Lay danh sach cac phieu dat tour cua khach hang
 * Ket noi DatTour, Tour, va HoaDon de lay thong tin day du
 */

header('Content-Type: application/json; charset=UTF-8');

// Kiem tra xem MaKhachHang duoc truyen vao khong
if (!isset($_GET['makhachhang'])) {
    echo json_encode([
        'success' => false,
        'error' => 'MaKhachHang parameter is required',
    ]);
    exit;
}

$maKhachHang = intval($_GET['makhachhang']);

// Kiem tra MaKhachHang phai > 0
if ($maKhachHang <= 0) {
    echo json_encode([
        'success' => false,
        'error' => 'MaKhachHang must be greater than 0',
        'makhachhang_received' => $_GET['makhachhang'],
    ]);
    exit;
}

// Ket noi den Oracle Database
$tnsname = 'SERVICE_NAME=ORCLPDB1;Addr=(PROTOCOL=TCP)(HOST=100.91.47.90)(PORT=1521)';
$conn = oci_new_connect('TADMIN', '123456', $tnsname);

if (!$conn) {
    $error = oci_error();
    echo json_encode([
        'success' => false,
        'error' => 'Connection failed',
        'details' => $error['message'],
    ]);
    exit;
}

try {
    // Query: Lay danh sach booking kem thong tin tour
    $query = "
        SELECT 
            DT.MaDatTour,
            DT.MaKhachHang,
            DT.MaTour,
            DT.NgayDat,
            DT.SoNguoiLon,
            DT.SoTreEm,
            DT.TongTien,
            DT.TrangThaiDat,
            DT.TrangThaiThanhToan,
            DT.YeuCauDacBiet,
            T.TieuDe,
            T.MoTa,
            T.NoiKhoiHanh,
            T.NoiDen,
            T.ThanhPho,
            T.ThoiGian,
            T.GiaNguoiLon,
            T.GiaTreEm,
            NVL(A.DuongDanAnh, '') as HinhAnh,
            NVL(HD.MaHoaDon, NULL) as MaHoaDon,
            NVL(HD.SoTien, 0) as HoaDonSoTien,
            NVL(HD.TrangThai, '') as HoaDonTrangThai
        FROM DatTour DT
        LEFT JOIN Tour T ON DT.MaTour = T.MaTour
        LEFT JOIN AnhTour A ON T.MaTour = A.MaTour AND ROWNUM = 1
        LEFT JOIN HoaDon HD ON DT.MaDatTour = HD.MaDatTour
        WHERE DT.MaKhachHang = :makhachhang
        ORDER BY DT.NgayDat DESC
    ";

    $stmt = oci_parse($conn, $query);
    oci_bind_by_name($stmt, ':makhachhang', $maKhachHang);

    if (!oci_execute($stmt)) {
        $error = oci_error($stmt);
        echo json_encode([
            'success' => false,
            'error' => 'Query execution failed',
            'oracle_error' => $error['message'],
            'query' => $query,
        ]);
        exit;
    }

    // Fetch du lieu
    $bookings = [];
    while ($row = oci_fetch_assoc($stmt)) {
        $bookings[] = [
            'maDatTour' => intval($row['MADATTOUR']),
            'maTour' => intval($row['MATOUR']),
            'ngayDat' => $row['NGAYDAT'],
            'soNguoiLon' => intval($row['SONGUOILON']),
            'soTreEm' => intval($row['SOTREEМ']),
            'tongTien' => floatval($row['TONGTIEN']),
            'trangThaiDat' => trim($row['TRANGTHAIDAD']),
            'trangThaiThanhToan' => trim($row['TRANGTHAITHANHH']),
            'yeuCauDacBiet' => trim($row['YEUCAUDACBIET']),
            'tieuDe' => trim($row['TIEUDE']),
            'moTa' => trim($row['MOTA']),
            'noiKhoiHanh' => trim($row['NOIKHOIHANH']),
            'noiDen' => trim($row['NOIDEN']),
            'thanhPho' => trim($row['THANHPHO']),
            'thoiGian' => $row['THOIGIAN'],
            'giaNguoiLon' => floatval($row['GIANGUOILON']),
            'giaTreEm' => floatval($row['GIATREEEМ']),
            'hinhAnh' => trim($row['HINHANH']),
            'maHoaDon' => !is_null($row['MAHOADON']) ? intval($row['MAHOADON']) : null,
            'hoaDonSoTien' => floatval($row['HOADONSOTIEN']),
            'hoaDonTrangThai' => trim($row['HOADONTRANGTHAI']),
        ];
    }

    oci_free_statement($stmt);
    oci_close($conn);

    echo json_encode([
        'success' => true,
        'bookings' => $bookings,
        'count' => count($bookings),
    ]);

} catch (Exception $e) {
    oci_close($conn);
    echo json_encode([
        'success' => false,
        'error' => $e->getMessage(),
    ]);
}
?>
