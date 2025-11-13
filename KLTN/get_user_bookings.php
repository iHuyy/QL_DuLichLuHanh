<?php
/**
 * File: get_user_bookings.php
 * Lấy danh sách các phiếu đặt tour của khách hàng
 * Kết nối DatTour, Tour, và HoaDon để lấy thông tin đầy đủ
 */

require_once __DIR__ . '/connect.php';

// Kiểm tra xem MaKhachHang được truyền vào không
if (!isset($_GET['makhachhang'])) {
    echo json_encode([
        'success' => false,
        'error' => 'MaKhachHang parameter is required',
    ]);
    exit;
}

$maKhachHang = intval($_GET['makhachhang']);

// Kiểm tra MaKhachHang phải > 0
if ($maKhachHang <= 0) {
    echo json_encode([
        'success' => false,
        'error' => 'MaKhachHang must be greater than 0',
        'makhachhang_received' => $_GET['makhachhang'],
    ]);
    exit;
}

// Sử dụng connection từ connect.php
if (!$conn) {
    $conn = connect_admin();
}

if (!$conn) {
    $error = oci_error();
    echo json_encode([
        'success' => false,
        'error' => 'Connection failed',
        'details' => $error['message'] ?? 'Unknown error',
    ]);
    exit;
}

try {
    // Query: Lấy danh sách booking kèm thông tin tour
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

    // Fetch dữ liệu
    $bookings = [];
    while ($row = oci_fetch_assoc($stmt)) {
        // Debug: print array keys để kiểm tra
        if (empty($bookings)) {
            error_log('Row keys: ' . json_encode(array_keys($row)));
        }
        
        $bookings[] = [
            'maDatTour' => intval($row['MADATTOUR'] ?? 0),
            'maTour' => intval($row['MATOUR'] ?? 0),
            'ngayDat' => $row['NGAYDAT'] ?? '',
            'soNguoiLon' => intval($row['SONGUOILON'] ?? 0),
            'soTreEm' => intval($row['SOTREEМ'] ?? 0),
            'tongTien' => floatval($row['TONGTIEN'] ?? 0),
            'trangThaiDat' => trim($row['TRANGTHAIDAD'] ?? ''),
            'trangThaiThanhToan' => trim($row['TRANGTHAITHANHH'] ?? ''),
            'yeuCauDacBiet' => trim($row['YEUCAUDACBIET'] ?? ''),
            'tieuDe' => trim($row['TIEUDE'] ?? ''),
            'moTa' => trim($row['MOTA'] ?? ''),
            'noiKhoiHanh' => trim($row['NOIKHOIHANH'] ?? ''),
            'noiDen' => trim($row['NOIDEN'] ?? ''),
            'thanhPho' => trim($row['THANHPHO'] ?? ''),
            'thoiGian' => $row['THOIGIAN'] ?? '',
            'giaNguoiLon' => floatval($row['GIANGUOILON'] ?? 0),
            'giaTreEm' => floatval($row['GIATREEEМ'] ?? 0),
            'hinhAnh' => trim($row['HINHANH'] ?? ''),
            'maHoaDon' => !is_null($row['MAHOADON']) ? intval($row['MAHOADON']) : null,
            'hoaDonSoTien' => floatval($row['HOADONSOTIEN'] ?? 0),
            'hoaDonTrangThai' => trim($row['HOADONTRANGTHAI'] ?? ''),
        ];
    }

    oci_free_statement($stmt);
    close_conn($conn);

    echo json_encode([
        'success' => true,
        'bookings' => $bookings,
        'count' => count($bookings),
    ]);

} catch (Exception $e) {
    close_conn($conn);
    echo json_encode([
        'success' => false,
        'error' => $e->getMessage(),
    ]);
}
?>
