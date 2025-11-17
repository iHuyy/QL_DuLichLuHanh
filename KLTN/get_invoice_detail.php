<?php
// get_invoice_detail.php
// Returns one invoice by `mahoadon` or a list by `makhachhang` (convenience for browser testing)
ini_set('display_errors', 0);
error_reporting(0);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/auth_middleware.php';

try {
    // accept JSON body or query params
    $data = json_decode(file_get_contents('php://input'), true) ?: [];
    $maHoaDon = null;
    if (!empty($data['mahoadon'])) $maHoaDon = trim($data['mahoadon']);
    if (!$maHoaDon && isset($_GET['mahoadon'])) $maHoaDon = trim($_GET['mahoadon']);

    $makhachhang = null;
    if (!empty($data['makhachhang'])) $makhachhang = trim($data['makhachhang']);
    if (!$makhachhang && isset($_GET['makhachhang'])) $makhachhang = trim($_GET['makhachhang']);

    // If caller didn't provide makhachhang, prefer auth token
    $userId = null;
    if ($makhachhang && $makhachhang !== '') {
        $userId = intval($makhachhang);
    } else {
        // try to require auth; if token missing, require_auth() will emit error
        try {
            $session = require_auth();
            if (isset($session['user_id'])) $userId = intval($session['user_id']);
        } catch (Exception $e) {
            // no token: leave $userId null — some flows accept browser query only
            $userId = null;
        }
    }

    global $conn;
    if (empty($conn) || $conn === null) $conn = connect_read();
    if (!$conn) {
        http_response_code(500);
        echo json_encode(['success' => false, 'message' => 'DB connection failed']);
        exit;
    }

    // If mahoadon provided -> return detailed invoice
    if ($maHoaDon && $maHoaDon !== '') {
        $sql = "SELECT h.MaHoaDon AS MAHOADON, h.MaDatTour AS MADATTOUR, h.SoTien AS SOTIEN, h.TrangThai AS TRANGTHAI, h.ChuKySo AS CHUKYSO, TO_CHAR(h.NGAYXUAT,'YYYY-MM-DD HH24:MI:SS') AS NGAYXUAT FROM HOADON h WHERE h.MaHoaDon = :md";
        $stid = @oci_parse($conn, $sql);
        if (!$stid) {
            echo json_encode(['success' => false, 'message' => 'Invalid query or missing table']);
            exit;
        }
        oci_bind_by_name($stid, ':md', $maHoaDon);
        if (!@oci_execute($stid)) {
            oci_free_statement($stid);
            echo json_encode(['success' => false, 'message' => 'Failed to execute invoice query']);
            exit;
        }

        $row = oci_fetch_assoc($stid);
        oci_free_statement($stid);
        if (!$row) {
            echo json_encode(['success' => false, 'message' => 'Invoice not found']);
            exit;
        }

        // normalize keys and keep values
        $row = array_change_key_case($row, CASE_UPPER);

        // attempt to fetch payload from HoaDonPayload (if table exists)
        $payload = null;
        $stmt2 = @oci_parse($conn, 'SELECT Payload FROM HoaDonPayload WHERE MaHoaDon = :md');
        if ($stmt2) {
            oci_bind_by_name($stmt2, ':md', $maHoaDon);
            if (@oci_execute($stmt2)) {
                $p = oci_fetch_assoc($stmt2);
                if ($p && isset($p['PAYLOAD'])) {
                    $pl = $p['PAYLOAD'];
                    if (is_object($pl) && method_exists($pl, 'load')) {
                        $payload = $pl->load();
                    } else if (is_resource($pl)) {
                        $payload = stream_get_contents($pl);
                    } else {
                        $payload = $pl;
                    }
                }
            }
            oci_free_statement($stmt2);
        }

        // Try to find related tour and one image (if any)
        $tour = null;
        if (!empty($row['MADATTOUR'])) {
            $maDatTour = $row['MADATTOUR'];
            $s = @oci_parse($conn, 'SELECT MaTour FROM DATTour WHERE MaDatTour = :mdt');
            if ($s) {
                oci_bind_by_name($s, ':mdt', $maDatTour);
                if (@oci_execute($s)) {
                    $dt = oci_fetch_assoc($s);
                    if ($dt && isset($dt['MATOUR'])) {
                        $maTour = $dt['MATOUR'];
                        // fetch one image from ANHTOUR if present
                        $si = @oci_parse($conn, "SELECT A.DuLieuAnh AS DULIEUANH, NVL(A.LoaiAnh,'') AS LOAIANH FROM ANHTOUR A WHERE A.MaTour = :mt AND ROWNUM = 1");
                        if ($si) {
                            oci_bind_by_name($si, ':mt', $maTour);
                            if (@oci_execute($si)) {
                                $img = oci_fetch_assoc($si);
                                if ($img && isset($img['DULIEUANH']) && $img['DULIEUANH'] !== null && $img['DULIEUANH'] !== '') {
                                    $blob = $img['DULIEUANH'];
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
                                        $mime = isset($img['LOAIANH']) && $img['LOAIANH'] !== '' ? trim($img['LOAIANH']) : 'image/jpeg';
                                        $imgUrl = 'data:' . $mime . ';base64,' . $b64;
                                        $tour = ['MATOUR' => $maTour, 'THUMB' => $imgUrl];
                                    }
                                }
                            }
                            oci_free_statement($si);
                        }
                    }
                }
                oci_free_statement($s);
            }
        }

        // include payload and tour info in response
        $out = ['success' => true, 'invoice' => $row];
        if ($payload !== null) $out['payload'] = $payload;
        if ($tour !== null) $out['tour'] = $tour;

        echo json_encode($out);
        exit;
    }

    // If no mahoadon: return invoices for a customer (makhachhang or authenticated user)
    $user = $userId ?? null;
    if (!$user) {
        echo json_encode(['success' => false, 'message' => 'Missing mahoadon and no customer context']);
        exit;
    }

    $sql2 = "SELECT h.MaHoaDon AS MAHOADON, dt.MaKhachHang AS MAKHACHHANG, h.SoTien AS SOTIEN, h.TrangThai AS TRANGTHAI, TO_CHAR(h.NGAYXUAT,'YYYY-MM-DD HH24:MI:SS') AS NGAYLAP FROM HOADON h JOIN DATTour dt ON h.MaDatTour = dt.MaDatTour WHERE dt.MaKhachHang = :mk ORDER BY h.NGAYXUAT DESC";
    $st = @oci_parse($conn, $sql2);
    if (!$st) {
        echo json_encode(['success' => true, 'invoices' => []]);
        exit;
    }
    oci_bind_by_name($st, ':mk', $user);
    if (!@oci_execute($st)) {
        oci_free_statement($st);
        echo json_encode(['success' => false, 'message' => 'Failed to execute invoice list query']);
        exit;
    }
    $rows = [];
    while ($r = oci_fetch_assoc($st)) $rows[] = $r;
    oci_free_statement($st);

    echo json_encode(['success' => true, 'invoices' => $rows]);
    exit;

} catch (Exception $e) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
}

?>
