<?php
// KLTN/get_invoice_detail.php
require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

try {
    // Lấy dữ liệu
    $data = json_decode(file_get_contents('php://input'), true) ?: [];
    $maHoaDon = $data['mahoadon'] ?? $_GET['mahoadon'] ?? null;
    $maKhachHang = $data['makhachhang'] ?? $_GET['makhachhang'] ?? null;

    // Nếu không truyền mã khách hàng, thử lấy từ token
    if (!$maKhachHang) {
        try {
            $session = require_auth();
            $maKhachHang = $session['user_id'];
        } catch (Exception $e) {
            // Không bắt buộc auth nếu chỉ query theo mã hóa đơn
        }
    }

    check_db_connection();

    // TRƯỜNG HỢP 1: Lấy chi tiết 1 hóa đơn
    if ($maHoaDon) {
        // [CẬP NHẬT] JOIN với DatTour để lấy TrangThaiDat
        $sql = "SELECT h.MaHoaDon, h.MaDatTour, h.SoTien, h.TrangThai, h.ChuKySo, h.Payload,
                       TO_CHAR(h.NGAYXUAT, 'YYYY-MM-DD HH24:MI:SS') as NGAYXUAT,
                       dt.TrangThaiDat 
                FROM HOADON h 
                LEFT JOIN DATTOUR dt ON h.MaDatTour = dt.MaDatTour
                WHERE h.MaHoaDon = :md";
        
        $stmt = oci_parse($conn, $sql);
        oci_bind_by_name($stmt, ':md', $maHoaDon);
        oci_execute($stmt);
        $invoice = oci_fetch_assoc($stmt);
        oci_free_statement($stmt);

        if (!$invoice) {
            echo json_encode(['success' => false, 'message' => 'Không tìm thấy hóa đơn']);
            exit;
        }

        // Lấy Payload (CLOB)
        if (!empty($invoice['PAYLOAD']) && is_object($invoice['PAYLOAD'])) {
            $invoice['PAYLOAD'] = $invoice['PAYLOAD']->load();
        }

        // Lấy thêm thông tin Tour liên quan (Ảnh thumb)
        $tourInfo = null;
        if ($invoice['MADATTOUR']) {
            $sqlT = "SELECT dt.MaTour, (SELECT DuLieuAnh FROM AnhTour WHERE MaTour=dt.MaTour AND ROWNUM=1) as ANH 
                     FROM DatTour dt WHERE dt.MaDatTour = :mdt";
            $stmtT = oci_parse($conn, $sqlT);
            oci_bind_by_name($stmtT, ':mdt', $invoice['MADATTOUR']);
            oci_execute($stmtT);
            $rowT = oci_fetch_assoc($stmtT);
            if ($rowT) {
                $imgUrl = '';
                if (!empty($rowT['ANH'])) {
                    $imgUrl = 'data:image/jpeg;base64,' . base64_encode($rowT['ANH']->load());
                }
                $tourInfo = ['MATOUR' => $rowT['MATOUR'], 'THUMB' => $imgUrl];
            }
            oci_free_statement($stmtT);
        }

        echo json_encode([
            'success' => true, 
            'invoice' => array_change_key_case($invoice, CASE_UPPER),
            'tour' => $tourInfo
        ]);
        exit;
    }

    // TRƯỜNG HỢP 2: Lấy danh sách hóa đơn của khách
    if ($maKhachHang) {
        $sqlList = "SELECT h.MaHoaDon, h.SoTien, h.TrangThai, TO_CHAR(h.NGAYXUAT, 'YYYY-MM-DD') as NGAYLAP 
                    FROM HoaDon h 
                    JOIN DatTour dt ON h.MaDatTour = dt.MaDatTour 
                    WHERE dt.MaKhachHang = :mk 
                    ORDER BY h.MaHoaDon DESC";
        $stmtList = oci_parse($conn, $sqlList);
        oci_bind_by_name($stmtList, ':mk', $maKhachHang);
        oci_execute($stmtList);
        
        $invoices = [];
        while ($r = oci_fetch_assoc($stmtList)) {
            $invoices[] = array_change_key_case($r, CASE_UPPER);
        }
        oci_free_statement($stmtList);
        
        echo json_encode(['success' => true, 'invoices' => $invoices]);
        exit;
    }

    echo json_encode(['success' => false, 'message' => 'Thiếu tham số']);

} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if($conn) oci_close($conn);
}
?>