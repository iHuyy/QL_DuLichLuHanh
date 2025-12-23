<?php
// KLTN/pay_invoice.php

// Bắt lỗi Fatal Error để trả về JSON thay vì crash
register_shutdown_function(function() {
    $error = error_get_last();
    if ($error !== null && $error['type'] === E_ERROR) {
        echo json_encode(['success' => false, 'message' => 'Fatal Error: ' . $error['message']]);
    }
});

ini_set('display_errors', 0); 
error_reporting(E_ALL); 
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

try {
    $session = require_auth();
    $input = json_decode(file_get_contents('php://input'), true) ?? [];
    $maHoaDon = intval($input['maHoaDon'] ?? 0);
    // Lấy phương thức thanh toán từ App gửi lên (mặc định là Chuyển khoản)
    $phuongThuc = trim($input['phuongThuc'] ?? 'Chuyển khoản');

    if ($maHoaDon <= 0) throw new Exception("Thiếu mã hóa đơn");

    check_db_connection();

    // 1. Lấy thông tin hóa đơn
    $sql = "SELECT MaHoaDon, MaDatTour, TrangThai, ChuKySo, Payload 
            FROM HoaDon WHERE MaHoaDon = :m FOR UPDATE";
    $stmt = oci_parse($conn, $sql);
    oci_bind_by_name($stmt, ':m', $maHoaDon);
    
    if (!@oci_execute($stmt, OCI_NO_AUTO_COMMIT)) {
        throw new Exception("Lỗi truy vấn: " . oci_error($stmt)['message']);
    }
    
    $row = oci_fetch_assoc($stmt);
    if (!$row) throw new Exception("Hóa đơn không tồn tại");

    // --- KIỂM TRA TRẠNG THÁI ĐẶT TOUR ---
    if (!empty($row['MADATTOUR'])) {
        $sqlCheckDt = "SELECT TrangThaiDat FROM DatTour WHERE MaDatTour = :mdt";
        $stmtCheckDt = oci_parse($conn, $sqlCheckDt);
        oci_bind_by_name($stmtCheckDt, ':mdt', $row['MADATTOUR']);
        oci_execute($stmtCheckDt);
        $rowDt = oci_fetch_assoc($stmtCheckDt);
        oci_free_statement($stmtCheckDt);

        if ($rowDt) {
            $dtStatus = mb_strtolower($rowDt['TRANGTHAIDAT'], 'UTF-8');
            if (strpos($dtStatus, 'hủy') !== false || strpos($dtStatus, 'cancelled') !== false) {
                 throw new Exception("Tour này đã bị hủy, không thể thực hiện thanh toán.");
            }
        }
    }

    // 2. XỬ LÝ PAYLOAD
    $payloadRaw = "";
    if (isset($row['PAYLOAD'])) {
        $pl = $row['PAYLOAD'];
        if (is_object($pl)) {
            if (method_exists($pl, 'load')) {
                $payloadRaw = $pl->load();
                $pl->free(); 
            } else {
                $payloadRaw = (string)$pl;
            }
        } else {
            $payloadRaw = $pl;
        }
    }

    if (empty($payloadRaw)) {
        throw new Exception("Dữ liệu gốc (Payload) bị rỗng. Không thể xác thực.");
    }

    // 3. Load Public Key
    $publicKey = null;
    $validPath = 'G:/Study/KLTN/AppQLDVDLLH/app_dllh_may_that/Keys/public_key.pem';
    
    if (file_exists($validPath)) {
        $publicKey = file_get_contents($validPath);
    } else {
        $localPath = __DIR__ . '/Keys/public_key.pem';
        if (file_exists($localPath)) $publicKey = file_get_contents($localPath);
    }

    if (!$publicKey) throw new Exception("Không tìm thấy Public Key trên server.");

    // 4. Xác thực Chữ ký
    $signature = base64_decode($row['CHUKYSO']);
    $ok = openssl_verify($payloadRaw, $signature, $publicKey, OPENSSL_ALGO_SHA256);

    if ($ok === 1) {
        // ---> THÀNH CÔNG
        
        // Cập nhật Hóa đơn & Phương thức thanh toán
        // Thêm cột PHUONGTHUCTHANHTOAN vào câu lệnh Update
        $sqlUpd = "UPDATE HoaDon SET TrangThai = 'Đã thanh toán', PHUONGTHUCTHANHTOAN = :pt WHERE MaHoaDon = :m";
        $s1 = oci_parse($conn, $sqlUpd);
        oci_bind_by_name($s1, ':m', $maHoaDon);
        oci_bind_by_name($s1, ':pt', $phuongThuc); // Bind biến phương thức

        if (!@oci_execute($s1, OCI_NO_AUTO_COMMIT)) throw new Exception("Lỗi update hóa đơn");

        // Cập nhật Đặt tour
        if ($row['MADATTOUR']) {
            $sqlBk = "UPDATE DatTour SET TrangThaiThanhToan = 'Đã thanh toán' WHERE MaDatTour = :mdt";
            $s2 = oci_parse($conn, $sqlBk);
            oci_bind_by_name($s2, ':mdt', $row['MADATTOUR']);
            @oci_execute($s2, OCI_NO_AUTO_COMMIT);
        }

        oci_commit($conn);
        echo json_encode([
            'success' => true, 
            'message' => "Thanh toán thành công ($phuongThuc)",
            'verified' => true
        ]);

    } elseif ($ok === 0) {
        oci_rollback($conn);
        echo json_encode([
            'success' => false, 
            'message' => 'Chữ ký số KHÔNG khớp. Dữ liệu hóa đơn có thể đã bị thay đổi trái phép.',
            'verified' => false
        ]);
    } else {
        oci_rollback($conn);
        $sslErr = "";
        while ($msg = openssl_error_string()) $sslErr .= $msg;
        throw new Exception("Lỗi OpenSSL: " . $sslErr);
    }

    if (isset($stmt)) oci_free_statement($stmt);

} catch (Exception $e) {
    if (isset($conn) && $conn) oci_rollback($conn);
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
} finally {
    if (isset($conn) && $conn) oci_close($conn);
}
?>