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
// $phuongThuc = $data['phuongThuc'] ?? ''; // Không lưu theo yêu cầu

// 2. Validate (Giữ nguyên)
if ($maKhachHang <= 0) {
    echo json_encode(["success" => false, "message" => "Lỗi: Mã khách hàng không hợp lệ."]);
    exit;
}
if (!$maTour || ($soNguoiLon + $soTreEm) <= 0) {
    echo json_encode(["success" => false, "message" => "Vui lòng chọn số lượng khách."]);
    exit;
}

check_db_connection();

// 3. KIỂM TRA SỐ CHỖ (Giữ nguyên logic kiểm tra số chỗ)
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

if (($soNguoiLon + $soTreEm) > $conLai) {
    echo json_encode(["success" => false, "message" => "Chỉ còn $conLai chỗ trống."]);
    close_conn($conn);
    exit;
}

// 4. Lấy giá và tính tiền (Giữ nguyên)
$sqlPrice = "SELECT GiaNguoiLon, GiaTreEm FROM Tour WHERE MaTour = :maTour";
$stmtPrice = oci_parse($conn, $sqlPrice);
oci_bind_by_name($stmtPrice, ":maTour", $maTour);
oci_execute($stmtPrice);
$rowPrice = oci_fetch_assoc($stmtPrice);
oci_free_statement($stmtPrice);

$giaNguoiLon = floatval($rowPrice['GIANGUOILON']);
$giaTreEm = floatval($rowPrice['GIATREEM']);
$tongTien = ($giaNguoiLon * $soNguoiLon) + ($giaTreEm * $soTreEm);

// 5. INSERT DAT TOUR (CẬP NHẬT TRẠNG THÁI)
// Yêu cầu: TrangThaiThanhToan = "Đã thanh toán", TrangThaiDat = "Chờ xác nhận"
$trangThaiThanhToan = 'Đã thanh toán'; 
$trangThaiDat = 'Chờ xác nhận';

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
oci_bind_by_name($stmtInsert, ":tongTien", $tongTien);
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

// --- ĐÃ XÓA PHẦN TẠO HÓA ĐƠN VÀ CHỮ KÝ SỐ ---
// Lý do: Hóa đơn sẽ được tạo khi Nhân viên xác nhận trên Web Admin.

echo json_encode([
    "success" => true,
    "message" => "Đặt tour thành công!",
    "bookingId" => (int)$maDatTour,
    "totalAmount" => $tongTien
]);

oci_close($conn);
?>