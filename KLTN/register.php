<?php
// KLTN/register.php
require_once __DIR__ . '/connect.php';

// Nhận dữ liệu từ Flutter
$data = json_decode(file_get_contents("php://input"), true);
$newUser = trim($data["username"] ?? '');
$newPass = trim($data["password"] ?? '');
$hoTen = trim($data["hoTen"] ?? '');
$email = trim($data["email"] ?? '');
$soDienThoai = trim($data["soDienThoai"] ?? '');
$diaChi = trim($data["diaChi"] ?? '');

// Validate dữ liệu
if ($newUser === '' || $newPass === '' || $hoTen === '' || $email === '') {
    echo json_encode(["success" => false, "message" => "Vui lòng nhập đầy đủ thông tin bắt buộc."]);
    exit;
}
if (!preg_match('/^[A-Za-z][A-Za-z0-9_]{1,29}$/', $newUser)) {
    echo json_encode(["success" => false, "message" => "Username không hợp lệ (Bắt đầu bằng chữ, chỉ chứa chữ, số, _ )"]);
    exit;
}
if (strlen($newPass) < 6) {
    echo json_encode(["success" => false, "message" => "Mật khẩu phải từ 6 ký tự trở lên"]);
    exit;
}

// Kiểm tra kết nối (Hàm này nằm trong connect.php)
check_db_connection(); 

// 1. Tạo User Oracle
$oracleUser = strtoupper($newUser);
$escapedPass = str_replace('"', '""', $newPass); // Escape dấu ngoặc kép

$sql_create_user = "
    CREATE USER \"$oracleUser\" IDENTIFIED BY \"$escapedPass\"
    PROFILE cus_profile
    DEFAULT TABLESPACE USERS
    TEMPORARY TABLESPACE TEMP
";

$stmt_create = @oci_parse($conn, $sql_create_user);
if (!@oci_execute($stmt_create, OCI_NO_AUTO_COMMIT)) {
    $err = oci_error($stmt_create);
    echo json_encode(["success" => false, "message" => "Không thể tạo User Oracle: " . ($err['message'] ?? 'Unknown error')]);
    exit;
}
oci_free_statement($stmt_create);

// 2. Cấp quyền
$grants = [
    "GRANT ROLE_CUSTOMER TO \"$oracleUser\"",
    "GRANT UNLIMITED TABLESPACE TO \"$oracleUser\""
];

foreach ($grants as $g) {
    $s = @oci_parse($conn, $g);
    if (!@oci_execute($s, OCI_NO_AUTO_COMMIT)) {
        $err = oci_error($s);
        // Rollback: Xóa user vừa tạo nếu lỗi quyền
        @oci_execute(oci_parse($conn, "DROP USER \"$oracleUser\" CASCADE"));
        echo json_encode(["success" => false, "message" => "Lỗi cấp quyền: " . $err['message']]);
        exit;
    }
    oci_free_statement($s);
}

// 3. Insert vào bảng KhachHang
// Xác định Schema Owner (thường là TADMIN)
$owner = 'TADMIN'; // Mặc định
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
    // Rollback toàn bộ
    @oci_execute(oci_parse($conn, "DROP USER \"$oracleUser\" CASCADE"));
    
    $msg = $err['message'];
    if (strpos($msg, 'ORA-00001') !== false) {
        $msg = "Email này đã được sử dụng.";
    }
    echo json_encode(["success" => false, "message" => "Lỗi lưu thông tin: " . $msg]);
    exit;
}

// 4. Commit Transaction
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
    "data" => [
        "MaKhachHang" => (int)$newId,
        "OracleUsername" => $oracleUser
    ]
]);
?>