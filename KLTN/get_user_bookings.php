<?php
// KLTN/get_user_bookings.php
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

try {
    $userId = null;

    // 1. Kiểm tra tham số
    if (isset($_GET['makhachhang']) && !empty($_GET['makhachhang'])) {
        $userId = intval($_GET['makhachhang']);
    } else {
        $session = require_auth(); 
        $userId = $session['user_id'];
    }

    check_db_connection();

    if (!$userId) {
        echo json_encode(['success' => false, 'message' => 'Không xác định được khách hàng']);
        exit;
    }

    // 2. Truy vấn
    // Lấy đầy đủ các trường để khớp với Model Flutter
    $sql = "SELECT 
                dt.MaDatTour, 
                dt.MaTour, 
                dt.NgayDat, 
                dt.SoNguoiLon, 
                dt.SoTreEm, 
                dt.TongTien, 
                dt.TrangThaiDat, 
                dt.TrangThaiThanhToan,
                dt.YeuCauDacBiet,
                t.TieuDe,
                t.MoTa,
                t.NoiKhoiHanh,
                t.NoiDen,
                t.ThanhPho,
                t.ThoiGian,
                t.GiaNguoiLon,
                t.GiaTreEm,
                hd.MaHoaDon,
                hd.SoTien AS HoaDonSoTien,
                hd.TrangThai AS TrangThaiHoaDon,
                (SELECT DuLieuAnh FROM AnhTour WHERE MaTour = t.MaTour AND ROWNUM = 1) AS AnhThumb,
                (SELECT LoaiAnh FROM AnhTour WHERE MaTour = t.MaTour AND ROWNUM = 1) AS MimeThumb
            FROM DatTour dt
            JOIN Tour t ON dt.MaTour = t.MaTour
            LEFT JOIN HoaDon hd ON dt.MaDatTour = hd.MaDatTour
            WHERE dt.MaKhachHang = :param_kh_id
            ORDER BY dt.NgayDat DESC";

    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) throw new Exception("Lỗi Parse SQL: " . (oci_error($conn)['message'] ?? 'Unknown'));

    oci_bind_by_name($stmt, ':param_kh_id', $userId);

    if (!@oci_execute($stmt)) throw new Exception("Lỗi Thực thi SQL: " . (oci_error($stmt)['message'] ?? 'Unknown'));

    $bookings = [];
    while ($row = oci_fetch_assoc($stmt)) {
        // Xử lý ảnh (như cũ)
        $imgUrl = '';
        if (isset($row['ANHTHUMB']) && $row['ANHTHUMB'] !== null) {
            $blob = $row['ANHTHUMB'];
            $data = null;
            if (is_object($blob) && method_exists($blob, 'load')) {
                try {
                    $data = $blob->load();
                    $blob->free();
                } catch (Exception $e) {}
            } else if (is_resource($blob)) {
                $data = stream_get_contents($blob);
            } else {
                $data = $blob;
            }

            if ($data) {
                $mime = !empty($row['MIMETHUMB']) ? $row['MIMETHUMB'] : 'image/jpeg';
                $imgUrl = "data:$mime;base64," . base64_encode($data);
            }
        }

        // *** MAPPING CHÍNH XÁC VỚI MODEL FLUTTER ***
        // Lưu ý: Các trường số (int/double) nên được cast về đúng kiểu hoặc để string tùy logic decode
        // Nhưng ở đây ta trả về giá trị thô, json_encode sẽ biến số thành số (nếu PHP nhận diện đc) hoặc chuỗi
        $bookings[] = [
            'maDatTour' => (int)$row['MADATTOUR'],
            'maTour' => (int)$row['MATOUR'],
            'ngayDat' => $row['NGAYDAT'], // String date
            'soNguoiLon' => (int)$row['SONGUOILON'],
            'soTreEm' => (int)$row['SOTREEM'],
            'tongTien' => (double)$row['TONGTIEN'],
            'trangThaiDat' => $row['TRANGTHAIDAT'],
            'trangThaiThanhToan' => $row['TRANGTHAITHANHTOAN'],
            'yeuCauDacBiet' => $row['YEUCAUDACBIET'] ?? '',
            'tieuDe' => $row['TIEUDE'],
            'moTa' => $row['MOTA'] ?? '',
            'noiKhoiHanh' => $row['NOIKHOIHANH'],
            'noiDen' => $row['NOIDEN'],
            'thanhPho' => $row['THANHPHO'] ?? '',
            'thoiGian' => $row['THOIGIAN'], // String date
            'giaNguoiLon' => (double)($row['GIANGUOILON'] ?? 0),
            'giaTreEm' => (double)($row['GIATREEM'] ?? 0),
            'hinhAnh' => $imgUrl, // Key khớp với model
            'maHoaDon' => isset($row['MAHOADON']) ? (int)$row['MAHOADON'] : null,
            'hoaDonSoTien' => (double)($row['HOADONSOTIEN'] ?? 0),
            'hoaDonTrangThai' => $row['TRANGTHAIHOADON'] ?? ''
        ];
    }

    oci_free_statement($stmt);
    
    // Trả về key 'data' để khớp với Flutter
    echo json_encode(['success' => true, 'data' => $bookings]);

} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => 'Server Error: ' . $e->getMessage()]);
} finally {
    if (!empty($conn)) @oci_close($conn);
}
?>