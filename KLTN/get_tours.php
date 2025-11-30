<?php
// KLTN/get_tours.php
ini_set('display_errors', 0);
error_reporting(E_ALL);
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';

try {
    check_db_connection();

    // SQL Query: Thêm subquery DA_DAT để đếm số người đã đặt
    $query = "SELECT t.MATOUR, t.TIEUDE, t.MOTA, t.NOIKHOIHANH, t.NOIDEN, t.THANHPHO, 
                     TO_CHAR(t.THOIGIAN, 'YYYY-MM-DD') as THOIGIAN, 
                     t.GIANGUOILON, t.GIATREEM, t.SOLUONG, t.MACHINHANH, 
                     n.TENCHINHANH, 
                     A.DuLieuAnh, NVL(A.LoaiAnh, 'image/jpeg') as LOAIANH,
                     
                     -- Tính tổng người lớn + trẻ em đã đặt (trừ đơn hủy)
                     (SELECT NVL(SUM(dt.SoNguoiLon + dt.SoTreEm), 0) 
                      FROM DATTOUR dt 
                      WHERE dt.MaTour = t.MaTour 
                      AND dt.TrangThaiDat != 'Đã hủy' 
                      AND dt.TrangThaiDat != 'Cancelled') AS DA_DAT
                     
              FROM TOUR t
              LEFT JOIN CHINHANH n ON t.MACHINHANH = n.MACHINHANH
              LEFT JOIN ANHTOUR A ON t.MATOUR = A.MATOUR AND ROWNUM = 1
              ORDER BY t.MATOUR DESC";

    $stid = @oci_parse($conn, $query);
    if (!$stid) throw new Exception(oci_error($conn)['message']);

    if (!@oci_execute($stid)) throw new Exception(oci_error($stid)['message']);

    $tours = [];
    while ($row = oci_fetch_assoc($stid)) {
        // Xử lý ảnh
        $imageUrl = '';
        if (!empty($row['DULIEUANH'])) {
            $blob = $row['DULIEUANH'];
            if (is_object($blob)) {
                $data = $blob->load();
                $mime = $row['LOAIANH'];
                $imageUrl = "data:$mime;base64," . base64_encode($data);
                $blob->free();
            }
        }

        // --- TÍNH SỐ CHỖ CÒN LẠI ---
        $tongSoCho = intval($row['SOLUONG']);
        $daDat = intval($row['DA_DAT']);
        $conLai = $tongSoCho - $daDat;
        if ($conLai < 0) $conLai = 0;

        $tours[] = [
            'MATOUR' => $row['MATOUR'],
            'TIEUDE' => $row['TIEUDE'],
            'MOTA' => $row['MOTA'],
            'NOIKHOIHANH' => $row['NOIKHOIHANH'],
            'NOIDEN' => $row['NOIDEN'],
            'THANHPHO' => $row['THANHPHO'],
            'THOIGIAN' => $row['THOIGIAN'],
            'GIANGUOILON' => $row['GIANGUOILON'],
            'GIATREEM' => $row['GIATREEM'],
            'SOLUONG' => $row['SOLUONG'],
            
            // TRẢ VỀ TRƯỜNG NÀY ĐỂ MODEL FLUTTER HỨNG
            'SOCHOCONLAI' => $conLai,
            
            'MACHINHANH' => $row['MACHINHANH'],
            'TENCHINHANH' => $row['TENCHINHANH'],
            'DULIEUANH' => $imageUrl,
            'LOAIANH' => $row['LOAIANH']
        ];
    }

    oci_free_statement($stid);
    echo json_encode($tours);

} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (!empty($conn)) @oci_close($conn);
}
?>