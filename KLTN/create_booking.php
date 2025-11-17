<?php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';

// Nhận dữ liệu từ Flutter
$data = json_decode(file_get_contents("php://input"), true);

$maTour = intval($data['maTour'] ?? 0);
$maKhachHang = intval($data['maKhachHang'] ?? 0);
$soNguoiLon = max(0, intval($data['soNguoiLon'] ?? 0));
$soTreEm = max(0, intval($data['soTreEm'] ?? 0));
$hoTen = trim($data['hoTen'] ?? '');
$soDienThoai = trim($data['soDienThoai'] ?? '');
$email = trim($data['email'] ?? '');
$ghiChu = trim($data['ghiChu'] ?? '');

// Validate - check MaKhachHang first (most common issue)
if ($maKhachHang <= 0) {
    echo json_encode([
        "success" => false,
        "message" => "Invalid MaKhachHang (customer ID): received $maKhachHang. Please check login flow - getCustomerId should return a valid number > 0.",
        "received_data" => $data
    ]);
    exit;
}

if (!$maTour || !$hoTen || !$soDienThoai || !$email || ($soNguoiLon + $soTreEm) <= 0) {
    echo json_encode(["success" => false, "message" => "Invalid booking data", "details" => [
        "maTour" => $maTour,
        "hoTen" => $hoTen,
        "soDienThoai" => $soDienThoai,
        "email" => $email,
        "total_people" => ($soNguoiLon + $soTreEm)
    ]]);
    exit;
}

if (empty($conn) || $conn === null) {
    $conn = connect_read();
}
if (!$conn) {
    http_response_code(500);
    echo json_encode(["success" => false, "message" => "DB connection failed"]);
    exit;
}

// Lấy giá tour
$sqlTour = "SELECT GiaNguoiLon, GiaTreEm FROM Tour WHERE MaTour = :maTour";
$stmtTour = @oci_parse($conn, $sqlTour);
if (!$stmtTour) {
    echo json_encode(["success" => false, "message" => "Parse tour error"]);
    close_conn($conn);
    exit;
}
oci_bind_by_name($stmtTour, ":maTour", $maTour);
if (!@oci_execute($stmtTour)) {
    echo json_encode(["success" => false, "message" => "Execute tour query failed"]);
    oci_free_statement($stmtTour);
    close_conn($conn);
    exit;
}

$rowTour = oci_fetch_assoc($stmtTour);
oci_free_statement($stmtTour);

if (!$rowTour) {
    echo json_encode(["success" => false, "message" => "Tour not found"]);
    close_conn($conn);
    exit;
}

$giaNguoiLon = floatval($rowTour['GIANGUOILON']);
$giaTreEm = floatval($rowTour['GIATREEM']);

// Tính tổng tiền (Không áp dụng service charge)
$tongTien = ($giaNguoiLon * $soNguoiLon) + ($giaTreEm * $soTreEm);
$tongTienSauPhi = $tongTien; // No extra service fee

// Tạo chữ ký số (dùng private key từ folder Keys)
$privateKey = null;
$triedPaths = [];
// Candidate paths to look for the private key. Adjust or add paths if your key is stored elsewhere.
$candidates = [
    __DIR__ . '/Keys/private_key_unencrypted.pem',
    __DIR__ . '/../app_dllh/Keys/private_key_unencrypted.pem',
    __DIR__ . '/private_key_unencrypted.pem',
    // Common absolute path in your workspace (Windows style)
    'G:/Study/KLTN/AppQLDVDLLH/app_dllh/Keys/private_key_unencrypted.pem',
    'G:\\Study\\KLTN\\AppQLDVDLLH\\app_dllh\\Keys\\private_key_unencrypted.pem',
];

foreach ($candidates as $p) {
    $triedPaths[] = $p;
    if (file_exists($p)) {
        $privateKey = file_get_contents($p);
        $privateKeyPath = $p;
        break;
    }
}

if ($privateKey === null) {
    echo json_encode([
        "success" => false,
        "message" => "Private key not found",
        "tried_paths" => $triedPaths,
        "note" => "Place private_key_unencrypted.pem into one of the above paths or update create_booking.php with the correct path."
    ]);
    close_conn($conn);
    exit;
}
$bookingData = json_encode([
    'maTour' => $maTour,
    'hoTen' => $hoTen,
    'soDienThoai' => $soDienThoai,
    'email' => $email,
    'soNguoiLon' => $soNguoiLon,
    'soTreEm' => $soTreEm,
    'tongTien' => $tongTienSauPhi,
    'timestamp' => time(),
]);

$signature = '';
if (openssl_sign($bookingData, $signature, $privateKey, 'RSA-SHA256')) {
    $signature = base64_encode($signature);
} else {
    echo json_encode(["success" => false, "message" => "Signature generation failed"]);
    close_conn($conn);
    exit;
}

// INSERT vào DatTour (DatTour DOES NOT contain ChuKySo; HoaDon table holds ChuKySo)
// DatTour schema (see QL_DuLichLuHanh.sql):
// MaDatTour, MaKhachHang, MaTour, NgayDat, SoNguoiLon, SoTreEm, TongTien, TrangThaiThanhToan, TrangThaiDat, YeuCauDacBiet
$trangThaiThanhToan = 'Chưa thanh toán'; // Mặc định: chưa thanh toán
$trangThaiDat = 'Chưa xác nhận'; // Mặc định: đã xác nhận

$sqlInsert = "
    INSERT INTO DatTour (
        MaTour, MaKhachHang, SoNguoiLon, SoTreEm, 
        TongTien, YeuCauDacBiet, NgayDat, TrangThaiThanhToan, TrangThaiDat
    ) VALUES (
        :maTour, :maKhachHang, :soNguoiLon, :soTreEm, 
        :tongTien, :yeuCauDacBiet, SYSDATE, :trangThaiThanhToan, :trangThaiDat
    ) RETURNING MaDatTour INTO :maDatTour
";

$stmtInsert = @oci_parse($conn, $sqlInsert);
if (!$stmtInsert) {
    echo json_encode(["success" => false, "message" => "Parse insert error"]);
    close_conn($conn);
    exit;
}

oci_bind_by_name($stmtInsert, ":maTour", $maTour);
oci_bind_by_name($stmtInsert, ":maKhachHang", $maKhachHang);
oci_bind_by_name($stmtInsert, ":soNguoiLon", $soNguoiLon);
oci_bind_by_name($stmtInsert, ":soTreEm", $soTreEm);
// DatTour stores the special request in YeuCauDacBiet
oci_bind_by_name($stmtInsert, ":yeuCauDacBiet", $ghiChu);
oci_bind_by_name($stmtInsert, ":tongTien", $tongTienSauPhi);
oci_bind_by_name($stmtInsert, ":trangThaiThanhToan", $trangThaiThanhToan);
oci_bind_by_name($stmtInsert, ":trangThaiDat", $trangThaiDat);
oci_bind_by_name($stmtInsert, ":maDatTour", $maDatTour, 32);

if (!@oci_execute($stmtInsert, OCI_COMMIT_ON_SUCCESS)) {
    $err = oci_error($stmtInsert) ?: oci_error($conn);
    oci_free_statement($stmtInsert);
    $debug = [
        'maTour' => $maTour,
        'maKhachHang' => $maKhachHang,
        'soNguoiLon' => $soNguoiLon,
        'soTreEm' => $soTreEm,
        'tongTien' => $tongTienSauPhi,
        'yeuCauDacBiet' => $ghiChu
    ];
    echo json_encode([
        "success" => false,
        "message" => "Booking insert failed: " . ($err['message'] ?? 'unknown'),
        "oracle_error" => $err,
        "bind_values" => $debug
    ]);
    close_conn($conn);
    exit;
}

oci_free_statement($stmtInsert);

// Now ensure HoaDon exists and store the signature into the invoice (HoaDon)
$signatureUpdated = false;
$maHoaDon = null;
$updateErrorMsg = null; // Khởi tạo biến $updateErrorMsg

// First try to update existing HoaDon (if trigger created it), capturing MaHoaDon
$sqlUpdateHoaDon = "UPDATE HoaDon SET ChuKySo = :chuKySo WHERE MaDatTour = :maDatTour RETURNING MaHoaDon INTO :maHoaDon";
$stmtUpd = @oci_parse($conn, $sqlUpdateHoaDon);
if ($stmtUpd) {
    oci_bind_by_name($stmtUpd, ":chuKySo", $signature);
    oci_bind_by_name($stmtUpd, ":maDatTour", $maDatTour);
    oci_bind_by_name($stmtUpd, ":maHoaDon", $maHoaDon, 32);
    if (@oci_execute($stmtUpd, OCI_COMMIT_ON_SUCCESS)) {
        $signatureUpdated = true;
    } else {
        // collect error but do not treat as fatal to DatTour creation
        $errUpd = oci_error($stmtUpd) ?: oci_error($conn);
        $updateErrorMsg = $errUpd['message'] ?? 'unknown';
    }
    oci_free_statement($stmtUpd);
}

// If update didn't return MaHoaDon, try to SELECT existing HoaDon by MaDatTour
if (empty($maHoaDon)) {
    $sel = @oci_parse($conn, 'SELECT MaHoaDon FROM HoaDon WHERE MaDatTour = :md');
    if ($sel) {
        oci_bind_by_name($sel, ':md', $maDatTour);
        if (@oci_execute($sel)) {
            $r = oci_fetch_assoc($sel);
            if ($r && isset($r['MAHOADON'])) {
                $maHoaDon = $r['MAHOADON'];
            }
        }
        oci_free_statement($sel);
    }
}

// If still no HoaDon, create one so we have MaHoaDon and can store payload/signature
if (empty($maHoaDon)) {
    $insertHoaDon = "INSERT INTO HoaDon (MaDatTour, SoTien, TrangThai, ChuKySo, NGAYXUAT) VALUES (:mdt, :sotien, :tt, :chuky, SYSDATE) RETURNING MaHoaDon INTO :maHoaDon";
    $insStmt = @oci_parse($conn, $insertHoaDon);
    if ($insStmt) {
        $tt = 'Chưa thanh toán';
        oci_bind_by_name($insStmt, ':mdt', $maDatTour);
        oci_bind_by_name($insStmt, ':sotien', $tongTienSauPhi);
        oci_bind_by_name($insStmt, ':tt', $tt);
        oci_bind_by_name($insStmt, ':chuky', $signature);
        oci_bind_by_name($insStmt, ':maHoaDon', $maHoaDon, 32);
        if (@oci_execute($insStmt, OCI_COMMIT_ON_SUCCESS)) {
            $signatureUpdated = true;
        } else {
            $errIns = oci_error($insStmt) ?: oci_error($conn);
            $updateErrorMsg = $errIns['message'] ?? 'insert failed';
        }
        oci_free_statement($insStmt);
    }
}

// *** PHẦN SỬA ĐỔI BẮT ĐẦU ***
// Store the actual signed payload for verification
// (Giả sử bảng HoaDonPayload(MaHoaDon, Payload CLOB) tồn tại)
if (!empty($maHoaDon) && !empty($bookingData)) {
    $sql_payload = "UPDATE HoaDon SET Payload = :payload_data WHERE MaHoaDon = :maHoaDon";
    
    $stmt_payload = @oci_parse($conn, $sql_payload);
    
    if ($stmt_payload) {
        oci_bind_by_name($stmt_payload, ':maHoaDon', $maHoaDon);
        oci_bind_by_name($stmt_payload, ':payload_data', $bookingData); // $bookingData là chuỗi JSON đã ký
        
        if (!@oci_execute($stmt_payload, OCI_COMMIT_ON_SUCCESS)) {
            $errPayload = oci_error($stmt_payload) ?: oci_error($conn);
            // Thêm lỗi vào $updateErrorMsg, không dừng hẳn
            $updateErrorMsg = ($updateErrorMsg ?? '') . '; Payload store failed: ' . ($errPayload['message'] ?? 'unknown');
        }
        oci_free_statement($stmt_payload);
    }
}
// *** PHẦN SỬA ĐỔI KẾT THÚC ***


close_conn($conn);

$response = [
    "success" => true,
    "message" => "Booking created successfully!",
    "bookingId" => $maDatTour,
    "totalAmount" => $tongTienSauPhi,
    "signature" => $signature,
    "signatureUpdated" => $signatureUpdated
];
if (isset($updateErrorMsg) && $updateErrorMsg !== null) {
  $response['warning'] = $updateErrorMsg;
}

echo json_encode($response);
?>