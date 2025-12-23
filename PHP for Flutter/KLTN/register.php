<?php
// KLTN/register.php
require_once __DIR__ . '/connect.php';

// Nhận dữ liệu từ Flutter
$data = json_decode(file_get_contents("php://input"), true);

// 1. Lấy thông tin đăng ký
$newUser = trim($data["username"] ?? '');
$newPass = trim($data["password"] ?? '');
$hoTen = trim($data["hoTen"] ?? '');
$email = trim($data["email"] ?? '');
$soDienThoai = trim($data["soDienThoai"] ?? '');
$diaChi = trim($data["diaChi"] ?? '');

// 2. Lấy thông tin xác thực OTP
$userOtp = trim($data["otp"] ?? '');
$serverHash = trim($data["otp_hash"] ?? '');
$otpExpiry = intval($data["otp_expiry"] ?? 0);

// --- VALIDATE DỮ LIỆU ---
if ($newUser === '' || $newPass === '' || $hoTen === '' || $email === '') {
    echo json_encode(["success" => false, "message" => "Vui lòng nhập đầy đủ thông tin bắt buộc."]);
    exit;
}

// Validate Username
if (!preg_match('/^[A-Za-z][A-Za-z0-9_]{1,29}$/', $newUser)) {
    echo json_encode(["success" => false, "message" => "Username không hợp lệ (Bắt đầu bằng chữ, chỉ chứa chữ, số, _ )"]);
    exit;
}

// Validate Password
if (strlen($newPass) < 8) {
    echo json_encode(["success" => false, "message" => "Mật khẩu phải có ít nhất 8 ký tự."]);
    exit;
}
if (!preg_match('/[A-Z]/', $newPass)) {
    echo json_encode(["success" => false, "message" => "Mật khẩu phải có ít nhất 1 ký tự viết hoa."]);
    exit;
}
if (!preg_match('/[0-9]/', $newPass)) {
    echo json_encode(["success" => false, "message" => "Mật khẩu phải có ít nhất 1 ký tự số."]);
    exit;
}
if (!preg_match('/[!@#$%^&*(),.?":{}|<>]/', $newPass)) {
    echo json_encode(["success" => false, "message" => "Mật khẩu phải có ít nhất 1 ký tự đặc biệt."]);
    exit;
}

// *** Validate Số điện thoại (10 số, bắt đầu bằng 0) ***
if (!preg_match('/^0\d{9}$/', $soDienThoai)) {
    echo json_encode(["success" => false, "message" => "Số điện thoại không hợp lệ. Phải gồm 10 chữ số và bắt đầu bằng số 0."]);
    exit;
}

// --- XÁC THỰC OTP ---
if (empty($userOtp) || empty($serverHash)) {
    echo json_encode(["success" => false, "message" => "Thiếu thông tin xác thực OTP."]);
    exit;
}

if (time() > $otpExpiry) {
    echo json_encode(["success" => false, "message" => "Mã OTP đã hết hạn."]);
    exit;
}

$secretKey = "KLTN_2024_SecretKey_!@#"; 
$dataToSign = $email . "|" . $userOtp . "|" . $otpExpiry;
$calculatedHash = base64_encode(hash_hmac('sha256', $dataToSign, $secretKey, true));

if (!hash_equals($calculatedHash, $serverHash)) {
    echo json_encode(["success" => false, "message" => "Mã OTP không chính xác."]);
    exit;
}

// --- TẠO TÀI KHOẢN ---
check_db_connection(); 
$oracleUser = strtoupper($newUser);

// A. Check User
$sql_check_user = "SELECT count(*) FROM all_users WHERE username = :u";
$stmt_check = oci_parse($conn, $sql_check_user);
oci_bind_by_name($stmt_check, ":u", $oracleUser);
oci_execute($stmt_check);
$row_user = oci_fetch_array($stmt_check, OCI_NUM);
oci_free_statement($stmt_check);

if ($row_user[0] > 0) {
    echo json_encode(["success" => false, "message" => "Tên đăng nhập '$newUser' đã tồn tại."]);
    oci_close($conn);
    exit;
}

// B. Check Email
$sql_check_email = "SELECT count(*) FROM TADMIN.KhachHang WHERE EMAIL = :e";
$stmt_email = oci_parse($conn, $sql_check_email);
oci_bind_by_name($stmt_email, ":e", $email);
oci_execute($stmt_email);
$row_email = oci_fetch_array($stmt_email, OCI_NUM);
oci_free_statement($stmt_email);

if ($row_email[0] > 0) {
    echo json_encode(["success" => false, "message" => "Email '$email' đã được sử dụng."]);
    oci_close($conn);
    exit;
}

// C. Create Oracle User
$escapedPass = str_replace('"', '""', $newPass);
$sql_create_user = "
    CREATE USER \"$oracleUser\" IDENTIFIED BY \"$escapedPass\"
    PROFILE cus_profile
    DEFAULT TABLESPACE USERS
    TEMPORARY TABLESPACE TEMP
";

$stmt_create = @oci_parse($conn, $sql_create_user);
if (!@oci_execute($stmt_create, OCI_NO_AUTO_COMMIT)) {
    $err = oci_error($stmt_create);
    echo json_encode(["success" => false, "message" => "Lỗi tạo tài khoản: " . ($err['message'] ?? 'Unknown')]);
    exit;
}
oci_free_statement($stmt_create);

// D. Grant
$grants = [
    "GRANT ROLE_CUSTOMER TO \"$oracleUser\"",
    "GRANT UNLIMITED TABLESPACE TO \"$oracleUser\""
];
foreach ($grants as $g) {
    $s = @oci_parse($conn, $g);
    if (!@oci_execute($s, OCI_NO_AUTO_COMMIT)) {
        $err = oci_error($s);
        @oci_execute(oci_parse($conn, "DROP USER \"$oracleUser\" CASCADE"));
        echo json_encode(["success" => false, "message" => "Lỗi cấp quyền: " . $err['message']]);
        exit;
    }
    oci_free_statement($s);
}

// E. Insert KhachHang
$owner = 'TADMIN';
$stmt_owner = @oci_parse($conn, "SELECT USER FROM DUAL");
if (@oci_execute($stmt_owner)) {
    $row = oci_fetch_row($stmt_owner);
    if ($row) $owner = strtoupper($row[0]);
}
oci_free_statement($stmt_owner);

$tableName = $owner . ".KhachHang";
$sql_insert = "
    INSERT INTO $tableName (HoTen, Email, SoDienThoai, DiaChi, VaiTro, ORACLE_USERNAME)
    VALUES (:hoTen, :email, :sdt, :diaChi, 'KhachHang', :oracleUser)
    RETURNING MaKhachHang INTO :new_id
";

$stmt_insert = @oci_parse($conn, $sql_insert);
oci_bind_by_name($stmt_insert, ':hoTen', $hoTen);
oci_bind_by_name($stmt_insert, ':email', $email);
oci_bind_by_name($stmt_insert, ':sdt', $soDienThoai);
oci_bind_by_name($stmt_insert, ':diaChi', $diaChi);
oci_bind_by_name($stmt_insert, ':oracleUser', $oracleUser);
oci_bind_by_name($stmt_insert, ':new_id', $newId, 32);

if (!@oci_execute($stmt_insert, OCI_NO_AUTO_COMMIT)) {
    $err = oci_error($stmt_insert);
    @oci_execute(oci_parse($conn, "DROP USER \"$oracleUser\" CASCADE"));
    echo json_encode(["success" => false, "message" => "Lỗi lưu thông tin: " . $err['message']]);
    exit;
}

// F. Commit
if (!@oci_commit($conn)) {
    $err = oci_error($conn);
    @oci_execute(oci_parse($conn, "DROP USER \"$oracleUser\" CASCADE"));
    echo json_encode(["success" => false, "message" => "Lỗi Commit: " . $err['message']]);
    exit;
}

oci_free_statement($stmt_insert);
oci_close($conn);

echo json_encode([
    "success" => true,
    "message" => "Đăng ký thành công!",
    "data" => ["MaKhachHang" => (int)$newId]
]);
?>