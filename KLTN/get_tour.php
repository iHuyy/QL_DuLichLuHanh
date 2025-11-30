<?php
// KLTN/get_tour.php
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';

$id = $_GET['id'] ?? null;
if (!$id) {
    echo json_encode(['success' => false, 'message' => 'Missing ID']);
    exit;
}

check_db_connection();

// 1. Lấy thông tin chi tiết Tour
$sql = "SELECT t.MATOUR, t.TIEUDE, t.MOTA, t.NOIKHOIHANH, t.NOIDEN, t.THANHPHO, 
               TO_CHAR(t.THOIGIAN, 'DD/MM/YYYY') AS THOIGIAN_DEP, 
               t.GIANGUOILON, t.GIATREEM, t.SOLUONG, 
               c.TENCHINHANH,
               
               (SELECT NVL(SUM(dt.SoNguoiLon + dt.SoTreEm), 0) 
                FROM DATTOUR dt 
                WHERE dt.MaTour = t.MaTour 
                AND dt.TrangThaiDat != 'Đã hủy' 
                AND dt.TrangThaiDat != 'Cancelled') AS DA_DAT

        FROM Tour t 
        LEFT JOIN ChiNhanh c ON t.MaChiNhanh = c.MaChiNhanh 
        WHERE t.MaTour = :param_tour_id";

$stmt = oci_parse($conn, $sql);
oci_bind_by_name($stmt, ':param_tour_id', $id);

if (oci_execute($stmt)) {
    $tour = oci_fetch_assoc($stmt);
    
    if ($tour) {
        // 2. Lấy danh sách ảnh
        $sqlImg = "SELECT DuLieuAnh, LoaiAnh FROM AnhTour WHERE MaTour = :p_tour_id_img";
        $stmtImg = oci_parse($conn, $sqlImg);
        oci_bind_by_name($stmtImg, ':p_tour_id_img', $id);
        oci_execute($stmtImg);
        
        $images = [];
        $firstImage = ''; // Biến lưu ảnh đại diện
        $firstMime = '';

        while ($rowImg = oci_fetch_assoc($stmtImg)) {
            if (isset($rowImg['DULIEUANH']) && $rowImg['DULIEUANH'] !== null) {
                $blob = $rowImg['DULIEUANH'];
                $data = null;
                if (is_object($blob) && method_exists($blob, 'load')) {
                    $data = $blob->load();
                } else {
                    $data = (string)$blob;
                }

                if ($data) {
                    $mime = $rowImg['LOAIANH'] ?: 'image/jpeg';
                    $b64 = "data:$mime;base64," . base64_encode($data);
                    $images[] = $b64;
                    
                    // Lưu ảnh đầu tiên làm ảnh đại diện
                    if (empty($firstImage)) {
                        $firstImage = $b64;
                        $firstMime = $mime;
                    }
                }
            }
        }
        oci_free_statement($stmtImg);

        // Tính số chỗ còn lại
        $tongSoCho = intval($tour['SOLUONG']);
        $daDat = intval($tour['DA_DAT']);
        $conLai = $tongSoCho - $daDat;
        if ($conLai < 0) $conLai = 0;

        $response = [
            'maTour' => $tour['MATOUR'],
            'tieuDe' => $tour['TIEUDE'],
            'moTa' => $tour['MOTA'],
            'noiKhoiHanh' => $tour['NOIKHOIHANH'],
            'noiDen' => $tour['NOIDEN'],
            'thoiGian' => $tour['THOIGIAN_DEP'],
            'giaNguoiLon' => $tour['GIANGUOILON'],
            'giaTreEm' => $tour['GIATREEM'],
            'soLuong' => $tour['SOLUONG'],
            
            'soChoConLai' => $conLai,
            'chiNhanh' => $tour['TENCHINHANH'] ?? 'Trụ sở chính', 
            
            'images' => $images,
            
            // QUAN TRỌNG: Trả về key này để Flutter không bị mất ảnh khi reload
            'HINHANH' => $firstImage,
            'DULIEUANH' => $firstImage,
            'LOAIANH' => $firstMime
        ];
        
        echo json_encode(['success' => true, 'data' => $response]);
    } else {
        echo json_encode(['success' => false, 'message' => 'Tour not found']);
    }
} else {
    echo json_encode(['success' => false, 'message' => 'Query failed']);
}

oci_free_statement($stmt);
if ($conn) oci_close($conn);
?>