<?php
// register.php
require_once __DIR__ . '/connect.php';
header("Content-Type: application/json; charset=utf-8");

// Nhận dữ liệu từ Flutter
$data = json_decode(file_get_contents("php://input"), true);
$newUser = trim($data["username"] ?? '');
$newPass = trim($data["password"] ?? '');
$hoTen = trim($data["hoTen"] ?? '');
$email = trim($data["email"] ?? '');
$soDienThoai = trim($data["soDienThoai"] ?? '');
$diaChi = trim($data["diaChi"] ?? '');

// Kiểm tra đầu vào cơ bản
if ($newUser === '' || $newPass === '' || $hoTen === '' || $email === '') {
    echo json_encode(["success" => false, "message" => "Vui lòng nhập đầy đủ Tên đăng nhập, Mật khẩu, Họ tên và Email."]);
    exit;
}
if (!preg_match('/^[A-Za-z][A-Za-z0-9_]{1,29}$/', $newUser)) {
    echo json_encode(["success" => false, "message" => "Tên đăng nhập không hợp lệ (chỉ chữ, số, '_' và bắt đầu bằng chữ)"]);
    exit;
}
if (strlen($newPass) < 6) {
    echo json_encode(["success" => false, "message" => "Mật khẩu phải ít nhất 6 ký tự"]);
    exit;
}
if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
    echo json_encode(["success" => false, "message" => "Email không hợp lệ."]);
    exit;
}

// Kết nối bằng admin (từ connect.php)
$conn = connect_admin();
if (!$conn) {
    echo json_encode(["success" => false, "message" => "Không thể kết nối tới Oracle bằng account admin."]);
    exit;
}

// Tạo user Oracle
$oracleUser = strtoupper($newUser);
$escapedPass = str_replace('"', '""', $newPass);

$sql_create_user = "
    CREATE USER $oracleUser IDENTIFIED BY \"$escapedPass\"
    PROFILE cus_profile
    DEFAULT TABLESPACE USERS
    TEMPORARY TABLESPACE TEMP
";
$stmt_create = oci_parse($conn, $sql_create_user);
$ok1 = @oci_execute($stmt_create, OCI_NO_AUTO_COMMIT);
oci_free_statement($stmt_create);
if (!$ok1) {
    $err = oci_error($conn);
    close_conn($conn);
    echo json_encode(["success" => false, "message" => "Tạo User Oracle thất bại: " . ($err['message'] ?? 'unknown')]);
    exit;
}

// Grant role and unlimited tablespace
$grants = [
    "GRANT ROLE_CUSTOMER TO $oracleUser",
    "GRANT UNLIMITED TABLESPACE TO $oracleUser"
];

foreach ($grants as $g) {
    $s = oci_parse($conn, $g);
    $ok = @oci_execute($s, OCI_NO_AUTO_COMMIT);
    oci_free_statement($s);
    if (!$ok) {
        $err = oci_error($conn);
        // rollback and drop user to avoid orphan user
        @oci_rollback($conn);
        @oci_execute(oci_parse($conn, "DROP USER $oracleUser CASCADE"), OCI_COMMIT_ON_SUCCESS);
        close_conn($conn);
        echo json_encode(["success" => false, "message" => "Cấp quyền thất bại: " . ($err['message'] ?? 'unknown')]);
        exit;
    }
}

// Xác định schema owner đang kết nối (để insert vào đúng bảng)
$owner = null;
$stmt_owner = oci_parse($conn, "SELECT USER FROM DUAL");
if ($stmt_owner && oci_execute($stmt_owner)) {
    $row = oci_fetch_row($stmt_owner);
    if ($row && isset($row[0])) $owner = strtoupper(trim($row[0]));
}
if ($stmt_owner) oci_free_statement($stmt_owner);

if (!$owner) {
    // fallback: dùng TADMIN nếu không lấy được
    $owner = 'TADMIN';
}

// Chuẩn bị INSERT vào bảng KhachHang với schema-qualified name
$tableName = $owner . ".KhachHang";

$sql_insert_kh = "
    INSERT INTO $tableName (HoTen, Email, SoDienThoai, DiaChi, VaiTro, ORACLE_USERNAME)
    VALUES (:hoTen_bv, :email_bv, :sdt_bv, :diaChi_bv, 'KhachHang', :oracleUser_bv)
    RETURNING MaKhachHang INTO :new_id
";

$stmt_insert = oci_parse($conn, $sql_insert_kh);
if (!$stmt_insert) {
    $err = oci_error($conn);
    // rollback and drop user
    @oci_rollback($conn);
    @oci_execute(oci_parse($conn, "DROP USER $oracleUser CASCADE"), OCI_COMMIT_ON_SUCCESS);
    close_conn($conn);
    echo json_encode(["success" => false, "message" => "Lỗi chuẩn bị INSERT: " . ($err['message'] ?? 'unknown')]);
    exit;
}

oci_bind_by_name($stmt_insert, ':hoTen_bv', $hoTen);
oci_bind_by_name($stmt_insert, ':email_bv', $email);
oci_bind_by_name($stmt_insert, ':sdt_bv', $soDienThoai);
oci_bind_by_name($stmt_insert, ':diaChi_bv', $diaChi);
oci_bind_by_name($stmt_insert, ':oracleUser_bv', $oracleUser);
oci_bind_by_name($stmt_insert, ':new_id', $newId, 32); // MaKhachHang trả về

$ok4 = @oci_execute($stmt_insert, OCI_NO_AUTO_COMMIT);
if (!$ok4) {
    $err = oci_error($stmt_insert) ?: oci_error($conn);
    @oci_free_statement($stmt_insert);
    // rollback and drop user to avoid orphan user
    @oci_rollback($conn);
    @oci_execute(oci_parse($conn, "DROP USER $oracleUser CASCADE"), OCI_COMMIT_ON_SUCCESS);
    close_conn($conn);

    $errMsg = $err['message'] ?? 'unknown';
    if (strpos($errMsg, 'ORA-00001') !== false) {
        $errMsg = "Email đã tồn tại hoặc trùng lặp dữ liệu Khách hàng.";
    }
    echo json_encode(["success" => false, "message" => "Thêm thông tin Khách hàng thất bại: " . $errMsg]);
    exit;
}

// Commit tất cả (DDL/DML)
$committed = @oci_commit($conn);
if (!$committed) {
    $err = oci_error($conn);
    // rollback and drop user
    @oci_rollback($conn);
    @oci_execute(oci_parse($conn, "DROP USER $oracleUser CASCADE"), OCI_COMMIT_ON_SUCCESS);
    @oci_free_statement($stmt_insert);
    close_conn($conn);
    echo json_encode(["success" => false, "message" => "Lỗi Commit Transaction: " . ($err['message'] ?? 'unknown')]);
    exit;
}

oci_free_statement($stmt_insert);
close_conn($conn);

// Trả kết quả thành công, kèm MaKhachHang nếu có
echo json_encode([
    "success" => true,
    "message" => "Tài khoản khách hàng $newUser được tạo thành công! Đã cấp ROLE_CUSTOMER và PROFILE cus_profile.",
    "data" => [
        "MaKhachHang" => isset($newId) ? intval($newId) : null,
        "OracleUsername" => $oracleUser
    ]
]);
exit;
?>