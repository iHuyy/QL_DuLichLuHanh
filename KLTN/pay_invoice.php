<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';
require_once __DIR__ . '/auth_middleware.php';

// Require token (secure endpoint)
try {
    $session = require_auth();
    $userId = intval($session['user_id']);
} catch (Exception $e) {
    echo json_encode(['success' => false, 'message' => 'Unauthorized']);
    exit;
}

// Expect POST JSON { maHoaDon: number }
$input = json_decode(file_get_contents('php://input'), true) ?? [];
$maHoaDon = intval($input['maHoaDon'] ?? 0);
if ($maHoaDon <= 0) {
    echo json_encode(['success' => false, 'message' => 'maHoaDon required']);
    exit;
}

if (empty($conn) || $conn === null) $conn = connect_admin();
if (!$conn) { echo json_encode(['success'=>false,'message'=>'DB connection failed']); exit; }

try {
    // *** PHẦN SỬA ĐỔI QUERY (FIX LỖI) ***
    // Chỉ SELECT và KHÓA (FOR UPDATE) duy nhất bảng HoaDon.
    // Bỏ JOIN DatTour ra khỏi câu lệnh khóa để tránh lỗi quyền.
    // Chúng ta vẫn lấy được MaDatTour từ chính bảng HoaDon.
    $sql = '
        SELECT 
            hd.MaHoaDon, 
            hd.MaDatTour, 
            hd.TrangThai, 
            hd.ChuKySo,
            hd.Payload
        FROM HoaDon hd
        WHERE hd.MaHoaDon = :m
        FOR UPDATE OF hd.TrangThai
    ';
    
    $stmt = @oci_parse($conn, $sql);
    if (!$stmt) { 
        echo json_encode(['success'=>false,'message'=>'Prepare failed']); 
        exit; 
    }
    
    oci_bind_by_name($stmt, ':m', $maHoaDon);
    
    // Execute without auto-commit to hold transaction
    if (!@oci_execute($stmt, OCI_DEFAULT)) { 
        $err = oci_error($stmt) ?: oci_error($conn); 
        echo json_encode([
            'success'=>false,
            'message'=>'Query failed', // Lỗi xảy ra ở đây
            'oracle_error'=>$err['message'] ?? 'Unknown OCI Error'
        ]); 
        exit; 
    }
    
    $row = oci_fetch_assoc($stmt);
    if (!$row) { 
        oci_free_statement($stmt); 
        echo json_encode(['success'=>false,'message'=>'Invoice not found']); 
        exit; 
    }
    
    // (Thêm) Kiểm tra xem đã thanh toán chưa
    if (strpos(strtolower($row['TRANGTHAI']), 'đã') !== false || strpos(strtolower($row['TRANGTHAI']), 'paid') !== false) {
        @oci_rollback($conn); // Nhả khóa
        oci_free_statement($stmt);
        echo json_encode(['success' => true, 'message' => 'Invoice already paid']);
        exit;
    }


    $signatureB64 = $row['CHUKYSO'] ?? '';
    if (empty($signatureB64)) { 
        oci_free_statement($stmt); 
        echo json_encode(['success'=>false,'message'=>'No signature found for invoice']); 
        exit; 
    }

    // *** LOGIC XÁC THỰC (Giữ nguyên) ***
    // Lấy payload đã lưu từ CSDL (cột hd.Payload)
    $payloadToVerify = null;
    if (isset($row['PAYLOAD']) && $row['PAYLOAD'] !== null) {
        $pl = $row['PAYLOAD'];
        // Xử lý kiểu dữ liệu CLOB
        if (is_object($pl) && method_exists($pl, 'load')) {
            $payloadToVerify = $pl->load();
        } else if (is_resource($pl)) {
            $payloadToVerify = stream_get_contents($pl);
        } else {
            $payloadToVerify = $pl; // Nếu nó là string
        }
    }

    if (empty($payloadToVerify)) {
        oci_free_statement($stmt);
        echo json_encode([
            'success'=>false,
            'message'=>'Signature verification failed: No signed payload data found for this invoice. The booking might be old or corrupted.'
        ]);
        exit;
    }
    
    // Load public key (giữ nguyên)
    $pubPathCandidates = [
        __DIR__ . '/Keys/public_key.pem',
        __DIR__ . '/../app_dllh/Keys/public_key.pem',
        'G:/Study/KLTN/AppQLDVDLLH/app_dllh/Keys/public_key.pem'
    ];
    $publicKey = null;
    foreach ($pubPathCandidates as $p) {
        if (file_exists($p)) { 
            $publicKey = file_get_contents($p); 
            break; 
        }
    }
    if (!$publicKey) { 
        oci_free_statement($stmt); 
        echo json_encode(['success'=>false,'message'=>'Public key not found on server']); 
        exit; 
    }

    $sig = base64_decode($signatureB64);
    $verified = false;
    
    // Xác thực chữ ký bằng payload đã lưu
    $result = openssl_verify($payloadToVerify, $sig, $publicKey, OPENSSL_ALGO_SHA256);
    if ($result === 1) {
        $verified = true;
    }
    
    if (!$verified) {
        // Rollback any held transaction and release lock
        @oci_rollback($conn);
        oci_free_statement($stmt);
        echo json_encode([
            'success'=>false,
            'message'=>'Signature verification failed. Data does not match signature.',
            'note'=>'This may indicate tampering or data corruption. Please contact support.'
        ]);
        exit;
    }

    // *** PHẦN CẬP NHẬT TRẠNG THÁI (Giữ nguyên) ***
    // (Câu lệnh SELECT đã lấy MaDatTour cho chúng ta)
    $maDatTour = $row['MADATTOUR'] ?? null;

    $updateInvoiceSql = "UPDATE HoaDon SET TrangThai = :status WHERE MaHoaDon = :m";
    $uStmt = @oci_parse($conn, $updateInvoiceSql);
    if (!$uStmt) { 
        oci_free_statement($stmt); 
        echo json_encode(['success'=>false,'message'=>'Prepare update failed']); 
        exit; 
    }
    
    $newStatus = 'Đã thanh toán';
    oci_bind_by_name($uStmt, ':status', $newStatus);
    oci_bind_by_name($uStmt, ':m', $maHoaDon);
    $ok1 = @oci_execute($uStmt, OCI_DEFAULT);

    $ok2 = true;
    if ($maDatTour) {
        $updateBookingSql = "UPDATE DatTour SET TrangThaiThanhToan = :ttt WHERE MaDatTour = :mdt";
        $bStmt = @oci_parse($conn, $updateBookingSql);
        if ($bStmt) {
            $ttt = 'Đã thanh toán';
            oci_bind_by_name($bStmt, ':ttt', $ttt);
            oci_bind_by_name($bStmt, ':mdt', $maDatTour);
            $ok2 = @oci_execute($bStmt, OCI_DEFAULT);
            oci_free_statement($bStmt);
        }
    }

    if ($ok1 && $ok2) {
        if (!@oci_commit($conn)) {
            @oci_rollback($conn);
            oci_free_statement($uStmt);
            oci_free_statement($stmt);
            echo json_encode(['success'=>false,'message'=>'Commit failed']);
            exit;
        }
        oci_free_statement($uStmt);
        oci_free_statement($stmt);
        echo json_encode([
            'success'=>true,
            'message'=>'Invoice marked as paid',
            'maHoaDon'=>$maHoaDon,
            'maDatTour'=>$maDatTour,
            'verified'=>true
        ]);
        exit;
    } else {
        @oci_rollback($conn);
        oci_free_statement($uStmt);
        oci_free_statement($stmt);
        echo json_encode(['success'=>false,'message'=>'Failed to update records, rolled back']);
        exit;
    }
} catch (Exception $e) {
    echo json_encode(['success'=>false,'message'=>$e->getMessage()]);
    exit;
}

?>