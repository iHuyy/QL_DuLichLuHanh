<?php
header('Content-Type: application/json; charset=utf-8');
require_once 'connect.php';
require_once __DIR__ . '/auth_middleware.php'; // (THÊM)

try {
    $session = require_auth(); // (THÊM - Buộc xác thực)

    // Return tours with both branch id and branch name (join to ChiNhanh)
    // Include BLOB image (DuLieuAnh) and MIME type (LoaiAnh) from TOUR table
    $query = "SELECT t.MATOUR, t.TIEUDE, t.MOTA, t.NOIKHOIHANH, t.NOIDEN, t.THANHPHO, t.THOIGIAN, t.GIANGUOILON, t.GIATREEM, t.SOLUONG, t.MACHINHANH, n.TENCHINHANH, A.DuLieuAnh as DULIEUANH, NVL(A.LoaiAnh, '') as LOAIANH
              FROM TOUR t
              LEFT JOIN CHINHANH n ON t.MACHINHANH = n.MACHINHANH
              LEFT JOIN ANHTOUR A ON t.MATOUR = A.MATOUR AND ROWNUM = 1";
    
    $stid = oci_parse($conn, $query);
    if (!$stid) {
        $err = oci_error($conn);
        http_response_code(500);
        echo json_encode(['error' => $err['message'] ?? 'Parse failed']);
        close_conn($conn);
        exit;
    }
    
    if (!oci_execute($stid)) {
        $err = oci_error($stid) ?: oci_error($conn);
        http_response_code(500);
        echo json_encode(['error' => $err['message'] ?? 'Execute failed']);
        oci_free_statement($stid);
        close_conn($conn);
        exit;
    }
    
    $tours = array();
    while ($row = oci_fetch_assoc($stid)) {
        // normalize and expose both id and name keys (mixed-case and upper-case)
        $maChi = isset($row['MACHINHANH']) ? (int)$row['MACHINHANH'] : null;
        $tenChi = isset($row['TENCHINHANH']) ? $row['TENCHINHANH'] : '';
    
        // Convert BLOB to base64 data URL
        $imageUrl = '';
        if (isset($row['DULIEUANH']) && $row['DULIEUANH'] !== null && $row['DULIEUANH'] !== '') {
            try {
                $blob = $row['DULIEUANH'];
                $data = null;
                if (is_object($blob) && method_exists($blob, 'load')) {
                    $data = $blob->load();
                } else if (is_resource($blob)) {
                    $data = stream_get_contents($blob);
                } else {
                    $data = $blob;
                }
                if ($data !== null && $data !== '') {
                    $b64 = base64_encode($data);
                    $mime = isset($row['LOAIANH']) && $row['LOAIANH'] !== '' ? trim($row['LOAIANH']) : 'image/jpeg';
                    $imageUrl = 'data:' . $mime . ';base64,' . $b64;
                }
            } catch (Exception $e) {
                // fallback: leave imageUrl empty
            }
        }
    
        $tours[] = [
            'MaTour' => $row['MATOUR'] ?? null,
            'TieuDe' => $row['TIEUDE'] ?? '',
            'MoTa' => $row['MOTA'] ?? '',
            'NoiKhoiHanh' => $row['NOIKHOIHANH'] ?? '',
            'NoiDen' => $row['NOIDEN'] ?? '',
            'ThanhPho' => $row['THANHPHO'] ?? '',
            'ThoiGian' => $row['THOIGIAN'] ?? null,
            'GiaNguoiLon' => $row['GIANGUOILON'] ?? null,
            'GiaTreEm' => $row['GIATREEM'] ?? null,
            'SoLuong' => $row['SOLUONG'] ?? null,
            // branch id and name (both forms)
            'MaChiNhanh' => $maChi,
            'MACHINHANH' => $maChi,
            'ChiNhanh' => $tenChi,
            'CHINHANH' => $tenChi,
            // Image (base64 data URL or empty)
            'DuLieuAnh' => $imageUrl,
            'DULIEUANH' => $imageUrl,
            'LoaiAnh' => isset($row['LOAIANH']) ? trim($row['LOAIANH']) : 'image/jpeg',
            'LOAIANH' => isset($row['LOAIANH']) ? trim($row['LOAIANH']) : 'image/jpeg',
        ];
    }
    
    oci_free_statement($stid);
    
    echo json_encode($tours, JSON_UNESCAPED_UNICODE);
    
    close_conn($conn);

} catch (Exception $e) {
    // (THÊM) Bắt lỗi 401 hoặc lỗi 500
    // Mã 401 sẽ tự động được đặt bởi auth_middleware.php
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
}
?>