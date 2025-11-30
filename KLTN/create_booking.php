<?php
// KLTN/create_booking.php
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/connect.php';

// 1. Nhận dữ liệu
$data = json_decode(file_get_contents("php://input"), true);

$maTour = intval($data['maTour'] ?? 0);
$maKhachHang = intval($data['maKhachHang'] ?? 0);
$soNguoiLon = max(0, intval($data['soNguoiLon'] ?? 0));
$soTreEm = max(0, intval($data['soTreEm'] ?? 0));
$hoTen = trim($data['hoTen'] ?? '');
$soDienThoai = trim($data['soDienThoai'] ?? '');
$email = trim($data['email'] ?? '');
$ghiChu = trim($data['ghiChu'] ?? '');

// 2. Validate cơ bản
if ($maKhachHang <= 0) {
    echo json_encode(["success" => false, "message" => "Lỗi: Mã khách hàng không hợp lệ."]);
    exit;
}
if (!$maTour || ($soNguoiLon + $soTreEm) <= 0) {
    echo json_encode(["success" => false, "message" => "Vui lòng chọn số lượng khách."]);
    exit;
}

check_db_connection();

// 3. KIỂM TRA SỐ CHỖ CÒN LẠI
$tongKhachDat = $soNguoiLon + $soTreEm;

$sqlCheck = "SELECT t.SoLuong, 
                    (SELECT NVL(SUM(dt.SoNguoiLon + dt.SoTreEm), 0) 
                     FROM DatTour dt 
                     WHERE dt.MaTour = t.MaTour 
                     AND dt.TrangThaiDat != 'Đã hủy' 
                     AND dt.TrangThaiDat != 'Cancelled') as DaDat
             FROM Tour t 
             WHERE t.MaTour = :maTour";

$stmtCheck = oci_parse($conn, $sqlCheck);
oci_bind_by_name($stmtCheck, ":maTour", $maTour);
oci_execute($stmtCheck);
$rowCheck = oci_fetch_assoc($stmtCheck);
oci_free_statement($stmtCheck);

if (!$rowCheck) {
    echo json_encode(["success" => false, "message" => "Tour không tồn tại."]);
    exit;
}

$tongSoCho = intval($rowCheck['SOLUONG']);
$daDat = intval($rowCheck['DADAT']);
$conLai = $tongSoCho - $daDat;

if ($tongKhachDat > $conLai) {
    echo json_encode([
        "success" => false, 
        "message" => "Rất tiếc, tour này chỉ còn $conLai chỗ trống. Bạn đang đặt $tongKhachDat chỗ."
    ]);
    close_conn($conn);
    exit;
}

// 4. Lấy giá tour để tính tiền
$sqlPrice = "SELECT GiaNguoiLon, GiaTreEm FROM Tour WHERE MaTour = :maTour";
$stmtPrice = oci_parse($conn, $sqlPrice);
oci_bind_by_name($stmtPrice, ":maTour", $maTour);
oci_execute($stmtPrice);
$rowPrice = oci_fetch_assoc($stmtPrice);
oci_free_statement($stmtPrice);

$giaNguoiLon = floatval($rowPrice['GIANGUOILON']);
$giaTreEm = floatval($rowPrice['GIATREEM']);
$tongTien = ($giaNguoiLon * $soNguoiLon) + ($giaTreEm * $soTreEm);
$tongTienSauPhi = $tongTien; 

// 5. INSERT DAT TOUR
$trangThaiThanhToan = 'Chưa thanh toán';
$trangThaiDat = 'Chưa xác nhận';

// Dùng SYSDATE cho DatTour
$sqlInsert = "INSERT INTO DatTour (
        MaTour, MaKhachHang, SoNguoiLon, SoTreEm, 
        TongTien, YeuCauDacBiet, NgayDat, TrangThaiThanhToan, TrangThaiDat
    ) VALUES (
        :maTour, :maKhachHang, :soNguoiLon, :soTreEm, 
        :tongTien, :yeuCauDacBiet, SYSDATE, :trangThaiThanhToan, :trangThaiDat
    ) RETURNING MaDatTour INTO :maDatTour";

$stmtInsert = @oci_parse($conn, $sqlInsert);
if (!$stmtInsert) {
    echo json_encode(["success" => false, "message" => "Lỗi chuẩn bị Insert DatTour"]);
    exit;
}

oci_bind_by_name($stmtInsert, ":maTour", $maTour);
oci_bind_by_name($stmtInsert, ":maKhachHang", $maKhachHang);
oci_bind_by_name($stmtInsert, ":soNguoiLon", $soNguoiLon);
oci_bind_by_name($stmtInsert, ":soTreEm", $soTreEm);
oci_bind_by_name($stmtInsert, ":tongTien", $tongTienSauPhi);
oci_bind_by_name($stmtInsert, ":yeuCauDacBiet", $ghiChu);
oci_bind_by_name($stmtInsert, ":trangThaiThanhToan", $trangThaiThanhToan);
oci_bind_by_name($stmtInsert, ":trangThaiDat", $trangThaiDat);
oci_bind_by_name($stmtInsert, ":maDatTour", $maDatTour, 32);

if (!@oci_execute($stmtInsert, OCI_COMMIT_ON_SUCCESS)) {
    $err = oci_error($stmtInsert);
    echo json_encode(["success" => false, "message" => "Lỗi đặt tour: " . $err['message']]);
    exit;
}
oci_free_statement($stmtInsert);

// 6. CHUẨN BỊ DỮ LIỆU KÝ SỐ (ĐỒNG BỘ C#)
// Lưu ý: Chúng ta phải tạo ngày trong PHP để đảm bảo chuỗi ký và dữ liệu lưu DB giống hệt nhau.
$nowStr = date('Y-m-d H:i:s'); // Định dạng yyyy-MM-dd HH:mm:ss

// Tạo HoaDon trước để lấy MaHoaDon
$maHoaDon = null;

// Kiểm tra trigger đã tạo HoaDon chưa (thường trigger sẽ tạo với ngày SYSDATE)
// Nếu trigger đã tạo, ta cần UPDATE lại ngày cho khớp với $nowStr của PHP để ký cho đúng.
$sqlCheckHd = "SELECT MaHoaDon FROM HoaDon WHERE MaDatTour = :mdt";
$stmtCheckHd = oci_parse($conn, $sqlCheckHd);
oci_bind_by_name($stmtCheckHd, ':mdt', $maDatTour);
oci_execute($stmtCheckHd);
$rowHd = oci_fetch_assoc($stmtCheckHd);
oci_free_statement($stmtCheckHd);

if ($rowHd) {
    $maHoaDon = intval($rowHd['MAHOADON']);
    // Update lại ngày để đồng bộ
    $sqlUpdDate = "UPDATE HoaDon SET NgayXuat = TO_DATE(:nds, 'YYYY-MM-DD HH24:MI:SS'), SoTien = :st WHERE MaHoaDon = :mhd";
    $sUpd = oci_parse($conn, $sqlUpdDate);
    oci_bind_by_name($sUpd, ':nds', $nowStr);
    oci_bind_by_name($sUpd, ':st', $tongTienSauPhi);
    oci_bind_by_name($sUpd, ':mhd', $maHoaDon);
    oci_execute($sUpd, OCI_COMMIT_ON_SUCCESS);
    oci_free_statement($sUpd);
} else {
    // Insert mới nếu chưa có
    $sqlIns = "INSERT INTO HoaDon (MaDatTour, SoTien, TrangThai, NgayXuat) 
               VALUES (:mdt, :st, 'Chưa thanh toán', TO_DATE(:nds, 'YYYY-MM-DD HH24:MI:SS')) 
               RETURNING MaHoaDon INTO :maHoaDon";
    $stmtIns = oci_parse($conn, $sqlIns);
    oci_bind_by_name($stmtIns, ':mdt', $maDatTour);
    oci_bind_by_name($stmtIns, ':st', $tongTienSauPhi);
    oci_bind_by_name($stmtIns, ':nds', $nowStr);
    oci_bind_by_name($stmtIns, ':maHoaDon', $maHoaDon, 32);
    @oci_execute($stmtIns, OCI_COMMIT_ON_SUCCESS);
    oci_free_statement($stmtIns);
}

// --- TẠO PAYLOAD CHUẨN (Format C#: MaHoaDon=...|SoTien=...|NgayXuat=...) ---
// Format tiền: bỏ số 0 dư, không dấu phẩy (giống C# 0.##)
// Ví dụ: 35000.0 -> 35000
$amountStr = (string)(float)$tongTienSauPhi; 

// Chuỗi payload
$payloadStr = "MaHoaDon=$maHoaDon|SoTien=$amountStr|NgayXuat=$nowStr";

// 7. KÝ SỐ
$privateKey = null;
$candidates = [
    'G:/Study/KLTN/AppQLDVDLLH/app_dllh_may_that/Keys/private_key_unencrypted.pem',
    __DIR__ . '/Keys/private_key_unencrypted.pem',
    __DIR__ . '/../app_dllh/Keys/private_key_unencrypted.pem'
];
foreach ($candidates as $p) {
    if (file_exists($p)) { $privateKey = file_get_contents($p); break; }
}

if (!$privateKey) {
    // Xóa dữ liệu rác nếu lỗi key
    // (Tùy chọn: rollback DB hoặc để lại để debug)
    echo json_encode(["success" => false, "message" => "Lỗi Server: Không tìm thấy Private Key."]);
    exit;
}

$signature = '';
if (openssl_sign($payloadStr, $signatureBin, $privateKey, OPENSSL_ALGO_SHA256)) {
    $signature = base64_encode($signatureBin);
} else {
    echo json_encode(["success" => false, "message" => "Lỗi tạo chữ ký số"]);
    exit;
}

// 8. LƯU PAYLOAD VÀ CHỮ KÝ VÀO DB
$sqlFinal = "UPDATE HoaDon SET ChuKySo = :sig, Payload = :pl WHERE MaHoaDon = :mhd";
$stmtFinal = oci_parse($conn, $sqlFinal);
oci_bind_by_name($stmtFinal, ':sig', $signature);
oci_bind_by_name($stmtFinal, ':pl', $payloadStr);
oci_bind_by_name($stmtFinal, ':mhd', $maHoaDon);

if (@oci_execute($stmtFinal, OCI_COMMIT_ON_SUCCESS)) {
    echo json_encode([
        "success" => true,
        "message" => "Đặt tour thành công!",
        "bookingId" => (int)$maDatTour,
        "totalAmount" => $tongTienSauPhi
    ]);
} else {
    $e = oci_error($stmtFinal);
    echo json_encode(["success" => false, "message" => "Lỗi cập nhật chữ ký: " . $e['message']]);
}

oci_free_statement($stmtFinal);
oci_close($conn);
?>