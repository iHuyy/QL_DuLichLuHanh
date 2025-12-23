using DuLich.Models;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace DuLich.Services
{
    public class OracleAuthService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache _cache;
        private readonly EmailService _emailService;

        public OracleAuthService(IConfiguration configuration, IMemoryCache cache, EmailService emailService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _cache = cache;
            _emailService = emailService;
        }

        public async Task<(bool success, string message)> PrepareRegistrationAndSendOtpAsync(RegisterModel model)
        {
            await using var adminConnection = new OracleConnection(_connectionString);
            await adminConnection.OpenAsync();

            using (var checkUserCmd = adminConnection.CreateCommand())
            {
                checkUserCmd.CommandText = "SELECT COUNT(*) FROM all_users WHERE username = :username";
                checkUserCmd.Parameters.Add("username", OracleDbType.Varchar2).Value = model.Username.ToUpper();
                if (Convert.ToInt32(await checkUserCmd.ExecuteScalarAsync()) > 0)
                {
                    return (false, "Tên đăng nhập đã tồn tại.");
                }
            }

            using (var checkEmailCmd = adminConnection.CreateCommand())
            {
                checkEmailCmd.CommandText = "SELECT COUNT(*) FROM (SELECT Email FROM TADMIN.KhachHang UNION ALL SELECT Email FROM TADMIN.NHANVIEN) WHERE Email = :email";
                checkEmailCmd.Parameters.Add("email", OracleDbType.Varchar2).Value = model.Email;
                if (Convert.ToInt32(await checkEmailCmd.ExecuteScalarAsync()) > 0)
                {
                    return (false, "Email đã được sử dụng.");
                }
            }

            return await GenerateAndSendOtp(model, isResend: false);
        }

        public async Task<(bool success, string message)> ResendOtpAsync(string email)
        {
            var cacheKey = $"REG_OTP_{email.ToUpper()}";
            if (_cache.TryGetValue(cacheKey, out Tuple<string, RegisterModel> cachedData))
            {
                return await GenerateAndSendOtp(cachedData.Item2, isResend: true);
            }
            return (false, "Phiên đăng ký đã hết hạn. Vui lòng bắt đầu lại.");
        }

        private async Task<(bool success, string message)> GenerateAndSendOtp(RegisterModel model, bool isResend)
        {
            var otp = new Random().Next(100000, 999999).ToString();
            var cacheKey = $"REG_OTP_{model.Email.ToUpper()}";
            var cacheData = new Tuple<string, RegisterModel>(otp, model);

            _cache.Set(cacheKey, cacheData, TimeSpan.FromSeconds(180));

            try
            {
                await _emailService.SendEmailAsync(model.Email, "Xác thực đăng ký tài khoản Du Lịch",
                    $"<h3>Mã xác thực của bạn là: <b style='color:red'>{otp}</b></h3><p>Mã này có hiệu lực trong 3 phút.</p>");

                var successMessage = isResend ? "Một mã OTP mới đã được gửi đến email của bạn." : "Mã xác thực đã được gửi đến email của bạn.";
                return (true, successMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OTP Email Error: {ex.Message}");
                return (false, "Lỗi hệ thống khi gửi email xác thực. Vui lòng thử lại.");
            }
        }

        public async Task<(bool success, string message)> VerifyAndCompleteRegistrationAsync(string email, string otp)
        {
            var cacheKey = $"REG_OTP_{email.ToUpper()}";
            if (_cache.TryGetValue(cacheKey, out Tuple<string, RegisterModel> cachedData))
            {
                if (cachedData.Item1 == otp)
                {
                    var model = cachedData.Item2;
                    var result = await _ConfirmRegistrationAsync(model.Username, model.Password, model.HoTen, model.Email, model.SoDienThoai, model.DiaChi);
                    if (result.success)
                    {
                        _cache.Remove(cacheKey);
                    }
                    return result;
                }
                return (false, "Mã xác thực không đúng.");
            }
            return (false, "Mã xác thực đã hết hạn hoặc không tồn tại. Vui lòng thử lại.");
        }

        private async Task<(bool success, string message)> _ConfirmRegistrationAsync(string username, string password, string hoTen, string email, string? soDienThoai = null, string? diaChi = null)
        {
            using var adminConnection = new OracleConnection(_connectionString);
            await adminConnection.OpenAsync();
            try
            {
                try
                {
                    using (var createUserCommand = adminConnection.CreateCommand())
                    {
                        createUserCommand.CommandText = $"\n                            CREATE USER \"{username.ToUpper()}\" IDENTIFIED BY \"{password}\" \n                            PROFILE cus_profile\n                            DEFAULT TABLESPACE USERS\n                            TEMPORARY TABLESPACE TEMP";
                        await createUserCommand.ExecuteNonQueryAsync();
                    }
                    using (var grantCommand = adminConnection.CreateCommand())
                    {
                        grantCommand.CommandText = $"GRANT ROLE_CUSTOMER TO \"{username.ToUpper()}\"";
                        await grantCommand.ExecuteNonQueryAsync();
                        grantCommand.CommandText = $"GRANT UNLIMITED TABLESPACE TO \"{username.ToUpper()}\"";
                        await grantCommand.ExecuteNonQueryAsync();
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"Error creating Oracle user: {ex.Message}");
                    return (false, $"Lỗi khi tạo tài khoản database: {ex.Message}");
                }
                try
                {
                    using (var insertCommand = adminConnection.CreateCommand())
                    {
                        insertCommand.CommandText = @"
                            INSERT INTO TADMIN.KhachHang (HoTen, Email, SoDienThoai, DiaChi, ORACLE_USERNAME)
                            VALUES (:HoTen, :Email, :SoDienThoai, :DiaChi, :Username)";
                        insertCommand.Parameters.Add("HoTen", OracleDbType.NVarchar2).Value = hoTen;
                        insertCommand.Parameters.Add("Email", OracleDbType.NVarchar2).Value = email;
                        insertCommand.Parameters.Add("SoDienThoai", OracleDbType.NVarchar2).Value = (object?)soDienThoai ?? DBNull.Value;
                        insertCommand.Parameters.Add("DiaChi", OracleDbType.NVarchar2).Value = (object?)diaChi ?? DBNull.Value;
                        insertCommand.Parameters.Add("Username", OracleDbType.NVarchar2).Value = username.ToUpper();
                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"Error inserting into KhachHang: {ex.Message}");
                    using (var dropCommand = adminConnection.CreateCommand())
                    {
                        dropCommand.CommandText = $"DROP USER \"{username.ToUpper()}\" CASCADE";
                        await dropCommand.ExecuteNonQueryAsync();
                    }
                    return (false, $"Lỗi khi lưu thông tin người dùng: {ex.Message}");
                }
                return (true, "Đăng ký thành công! Vui lòng đăng nhập.");
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"Oracle error: {ex.Message}");
                return (false, $"Lỗi hệ thống database: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return (false, "Đăng ký không thành công, vui lòng thử lại sau");
            }
        }

        public async Task<(bool success, string role, string errorMessage)> ValidateLoginAsync(string username, string password)
        {
            try
            {
                var builder = new OracleConnectionStringBuilder(_connectionString)
                {
                    UserID = username.ToUpper(),
                    Password = password,
                    ConnectionTimeout = 60
                };

                using var connection = new OracleConnection(builder.ConnectionString);

                var retryCount = 3;
                while (retryCount > 0)
                {
                    try
                    {
                        await connection.OpenAsync();
                        break;
                    }
                    catch (OracleException ex) when (ex.Number == 50201)
                    {
                        retryCount--;
                        if (retryCount == 0) throw;
                        await Task.Delay(2000);
                    }
                }
                Console.WriteLine($"Successfully connected as {username.ToUpper()}");

                var role = await GetUserRoleAsync(connection);
                Console.WriteLine($"User {username.ToUpper()} has role: {role}");

                if (!string.IsNullOrEmpty(role))
                {
                    return (true, role, string.Empty);
                }

                using (var sysConnection = new OracleConnection(_connectionString))
                {
                    await sysConnection.OpenAsync();

                    using var checkCommand = sysConnection.CreateCommand();
                    checkCommand.CommandText = "SELECT COUNT(*) FROM all_users WHERE username = :username";
                    checkCommand.Parameters.Add("username", OracleDbType.Varchar2).Value = username.ToUpper();
                    var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                    if (count == 0)
                    {
                        return (false, string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
                    }

                    return (true, "ROLE_CUSTOMER", string.Empty);
                }
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"Oracle login error: {ex.Message}");
                if (ex.Number == 28000)
                {
                    return (false, string.Empty, "Tài khoản của bạn đã bị khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau ít phút.");
                }
                // Bắt lỗi ORA-28001: Mật khẩu hết hạn
                if (ex.Number == 28001)
                {
                    return (false, string.Empty, "PASSWORD_EXPIRED");
                }
                if (ex.Number == 1017)
                {
                    return (false, string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
                }
                return (false, string.Empty, "Lỗi kết nối cơ sở dữ liệu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return (false, string.Empty, "Đã có lỗi xảy ra. Vui lòng thử lại.");
            }
        }

        private async Task<string> GetUserRoleAsync(OracleConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT GRANTED_ROLE
                FROM USER_ROLE_PRIVS
                WHERE GRANTED_ROLE IN ('ROLE_ADMIN', 'ROLE_CUSTOMER', 'ROLE_STAFF')";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader.GetString(0);
            }
            return string.Empty;
        }

        public async Task<(bool success, string message)> RegisterStaffAsync(string username, string password, string hoTen, string email, string? soDienThoai = null, int? maChiNhanh = null)
        {
            using var adminConnection = new OracleConnection(_connectionString);
            await adminConnection.OpenAsync();

            try
            {
                using (var checkCommand = adminConnection.CreateCommand())
                {
                    checkCommand.CommandText = "SELECT COUNT(*) FROM all_users WHERE username = :username";
                    checkCommand.Parameters.Add("username", OracleDbType.Varchar2).Value = username.ToUpper();
                    var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        return (false, "Tên đăng nhập đã tồn tại");
                    }
                }

                using (var checkEmailCommand = adminConnection.CreateCommand())
                {
                    checkEmailCommand.CommandText = "SELECT COUNT(*) FROM TADMIN.KhachHang WHERE Email = :email";
                    checkEmailCommand.Parameters.Add("email", OracleDbType.Varchar2).Value = email;
                    var count = Convert.ToInt32(await checkEmailCommand.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        return (false, "Email đã được sử dụng");
                    }
                }

                try
                {
                    using (var createUserCommand = adminConnection.CreateCommand())
                    {
                        createUserCommand.CommandText = $"\n                            CREATE USER \"{username.ToUpper()}\" IDENTIFIED BY \"{password}\" \n                            PROFILE staff_profile\n                            DEFAULT TABLESPACE USERS\n                            TEMPORARY TABLESPACE TEMP";
                        await createUserCommand.ExecuteNonQueryAsync();
                    }
                    using (var grantCommand = adminConnection.CreateCommand())
                    {
                        grantCommand.CommandText = $"GRANT ROLE_STAFF TO \"{username.ToUpper()}\"";
                        await grantCommand.ExecuteNonQueryAsync();

                        grantCommand.CommandText = $"GRANT UNLIMITED TABLESPACE TO \"{username.ToUpper()}\"";
                        await grantCommand.ExecuteNonQueryAsync();
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"Error creating Oracle staff user: {ex.Message}");
                    return (false, $"Lỗi khi tạo tài khoản: {ex.Message}");
                }
                try
                {
                    using (var insertCommand = adminConnection.CreateCommand())
                    {
                        insertCommand.CommandText = @"
                            INSERT INTO TADMIN.NHANVIEN (HoTen, Email, SoDienThoai, ORACLE_USERNAME, VaiTro, MACHINHANH)
                            VALUES (:HoTen, :Email, :SoDienThoai, :Username, :VaiTro, :MaChiNhanh)";
                        insertCommand.Parameters.Add("HoTen", OracleDbType.NVarchar2).Value = hoTen;
                        insertCommand.Parameters.Add("Email", OracleDbType.NVarchar2).Value = email;
                        insertCommand.Parameters.Add("SoDienThoai", OracleDbType.NVarchar2).Value = (object?)soDienThoai ?? DBNull.Value;
                        insertCommand.Parameters.Add("Username", OracleDbType.NVarchar2).Value = username.ToUpper();
                        insertCommand.Parameters.Add("VaiTro", OracleDbType.NVarchar2).Value = "NhanVien";
                        insertCommand.Parameters.Add("MaChiNhanh", OracleDbType.Int32).Value = (object?)maChiNhanh ?? DBNull.Value;

                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"Error inserting staff into NHANVIEN: {ex.Message}");
                    using (var dropCommand = adminConnection.CreateCommand())
                    {
                        dropCommand.CommandText = $"DROP USER \"{username.ToUpper()}\" CASCADE";
                        await dropCommand.ExecuteNonQueryAsync();
                    }
                    return (false, $"Lỗi khi lưu thông tin: {ex.Message}");
                }

                return (true, "Tạo nhân viên thành công");
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"Oracle error: {ex.Message}");
                return (false, $"Lỗi khi tạo tài khoản: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return (false, "Không thể tạo nhân viên, vui lòng thử lại sau");
            }
        }

        public async Task<(bool success, string message)> RegisterAdminAsync(string username, string password, string hoTen, string email, string? soDienThoai = null, string? diaChi = null)
        {
            using var adminConnection = new OracleConnection(_connectionString);
            await adminConnection.OpenAsync();
            try
            {
                using (var checkCommand = adminConnection.CreateCommand())
                {
                    checkCommand.CommandText = "SELECT COUNT(*) FROM all_users WHERE username = :username";
                    checkCommand.Parameters.Add("username", OracleDbType.Varchar2).Value = username.ToUpper();
                    var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        return (false, "Tên đăng nhập đã tồn tại");
                    }
                }

                using (var checkEmailCommand = adminConnection.CreateCommand())
                {
                    checkEmailCommand.CommandText = "SELECT COUNT(*) FROM TADMIN.KhachHang WHERE Email = :email";
                    checkEmailCommand.Parameters.Add("email", OracleDbType.Varchar2).Value = email;
                    var count = Convert.ToInt32(await checkEmailCommand.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        return (false, "Email đã được sử dụng");
                    }
                }

                try
                {
                    using (var createUserCommand = adminConnection.CreateCommand())
                    {
                        createUserCommand.CommandText = $"\n                            CREATE USER \"{username.ToUpper()}\" IDENTIFIED BY \"{password}\" \n                            PROFILE admin_profile\n                            DEFAULT TABLESPACE USERS\n                            TEMPORARY TABLESPACE TEMP";
                        await createUserCommand.ExecuteNonQueryAsync();
                    }

                    using (var grantCommand = adminConnection.CreateCommand())
                    {
                        grantCommand.CommandText = $"GRANT ROLE_ADMIN TO \"{username.ToUpper()}\"";
                        await grantCommand.ExecuteNonQueryAsync();

                        grantCommand.CommandText = $"GRANT UNLIMITED TABLESPACE TO \"{username.ToUpper()}\"";
                        await grantCommand.ExecuteNonQueryAsync();
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"Error creating Oracle admin user: {ex.Message}");
                    return (false, $"Lỗi khi tạo tài khoản: {ex.Message}");
                }

                try
                {
                    using (var insertCommand = adminConnection.CreateCommand())
                    {
                        insertCommand.CommandText = @"
                            INSERT INTO TADMIN.KhachHang (HoTen, Email, SoDienThoai, DiaChi, ORACLE_USERNAME, VaiTro)
                            VALUES (:HoTen, :Email, :SoDienThoai, :DiaChi, :Username, :VaiTro)";

                        insertCommand.Parameters.Add("HoTen", OracleDbType.NVarchar2).Value = hoTen;
                        insertCommand.Parameters.Add("Email", OracleDbType.NVarchar2).Value = email;
                        insertCommand.Parameters.Add("SoDienThoai", OracleDbType.NVarchar2).Value = (object?)soDienThoai ?? DBNull.Value;
                        insertCommand.Parameters.Add("DiaChi", OracleDbType.NVarchar2).Value = (object?)diaChi ?? DBNull.Value;
                        insertCommand.Parameters.Add("Username", OracleDbType.NVarchar2).Value = username.ToUpper();
                        insertCommand.Parameters.Add("VaiTro", OracleDbType.NVarchar2).Value = "Admin";

                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"Error inserting admin into KhachHang: {ex.Message}");
                    using (var dropCommand = adminConnection.CreateCommand())
                    {
                        dropCommand.CommandText = $"DROP USER \"{username.ToUpper()}\" CASCADE";
                        await dropCommand.ExecuteNonQueryAsync();
                    }
                    return (false, $"Lỗi khi lưu thông tin: {ex.Message}");
                }

                return (true, "Tạo admin thành công");
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"Oracle error: {ex.Message}");
                return (false, $"Lỗi khi tạo tài khoản: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return (false, "Không thể tạo admin, vui lòng thử lại sau");
            }
        }

        public async Task<(bool success, string message)> ChangePasswordAsync(string username, string newPassword)
        {
            try
            {
                string sanitizedPassword = newPassword.Replace("\"", "\"\"");

                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = $"ALTER USER \"{username.ToUpper()}\" IDENTIFIED BY \"{sanitizedPassword}\"";

                await cmd.ExecuteNonQueryAsync();
                return (true, "Mật khẩu đã được thay đổi thành công.");
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"Oracle error changing password for {username}: {ex.Message}");
                return (false, $"Lỗi khi thay đổi mật khẩu: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error changing password for {username}: {ex.Message}");
                return (false, "Lỗi không xác định khi thay đổi mật khẩu.");
            }
        }

        // [MỚI] Hàm xử lý đổi mật khẩu bắt buộc (Force Change Password)
        public async Task<(bool success, string message)> ForceChangePasswordAsync(string username, string oldPassword, string newPassword)
        {
            if (oldPassword == newPassword)
            {
                return (false, "Mật khẩu mới không được trùng với mật khẩu cũ.");
            }

            // 1. Xác minh mật khẩu cũ
            bool isOldPasswordCorrect = false;
            try
            {
                var builder = new OracleConnectionStringBuilder(_connectionString)
                {
                    UserID = username.ToUpper(),
                    Password = oldPassword
                };
                using var connUser = new OracleConnection(builder.ConnectionString);
                await connUser.OpenAsync();
                isOldPasswordCorrect = true;
            }
            catch (OracleException ex)
            {
                // Nếu lỗi ORA-28001 nghĩa là pass đúng nhưng đã hết hạn -> Hợp lệ
                if (ex.Number == 28001)
                {
                    isOldPasswordCorrect = true;
                }
            }

            if (!isOldPasswordCorrect)
            {
                return (false, "Mật khẩu cũ không chính xác.");
            }

            // 2. Thực hiện đổi mật khẩu và mở khóa bằng quyền Admin
            try
            {
                var changeResult = await ChangePasswordAsync(username, newPassword);
                if (!changeResult.success) return changeResult;

                using var adminConn = new OracleConnection(_connectionString);
                await adminConn.OpenAsync();
                using var cmdUnlock = adminConn.CreateCommand();
                cmdUnlock.CommandText = $"ALTER USER \"{username.ToUpper()}\" ACCOUNT UNLOCK";
                await cmdUnlock.ExecuteNonQueryAsync();

                return (true, "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.");
            }
            catch (Exception ex)
            {
                return (false, "Lỗi hệ thống khi đổi mật khẩu: " + ex.Message);
            }
        }

        public async Task<(bool success, string message)> SetAccountLockAsync(string username, bool locked)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return (false, "Tên đăng nhập không hợp lệ.");
            }

            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = locked
                ? $"ALTER USER \"{username.ToUpperInvariant()}\" ACCOUNT LOCK"
                : $"ALTER USER \"{username.ToUpperInvariant()}\" ACCOUNT UNLOCK";
            try
            {
                await cmd.ExecuteNonQueryAsync();
                return (true, locked ? "Tài khoản đã bị khóa." : "Tài khoản đã được mở khóa.");
            }
            catch (OracleException ex)
            {
                var msg = locked ? "Không thể khóa tài khoản" : "Không thể mở khóa tài khoản";
                return (false, $"{msg}: {ex.Message}");
            }
        }

        public async Task<UserDetail?> GetUserAsync(string username)
        {
            username = username?.ToUpper() ?? string.Empty;
            if (string.IsNullOrEmpty(username)) return null;

            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT USERNAME, CREATED, DEFAULT_TABLESPACE
                FROM ALL_USERS
                WHERE USERNAME = :username";
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var detail = new UserDetail
            {
                Username = reader["USERNAME"]?.ToString() ?? username,
                Created = reader["CREATED"] as DateTime?,
                DefaultTablespace = reader["DEFAULT_TABLESPACE"]?.ToString() ?? string.Empty
            };

            using var roleCmd = connection.CreateCommand();
            roleCmd.CommandText = @"
                SELECT GRANTED_ROLE
                FROM USER_ROLE_PRIVS
                WHERE USERNAME = :username";
            roleCmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            using var roleReader = await roleCmd.ExecuteReaderAsync();
            while (await roleReader.ReadAsync())
            {
                var role = roleReader["GRANTED_ROLE"]?.ToString();
                if (!string.IsNullOrEmpty(role))
                {
                    detail.Roles.Add(role);
                }
            }

            return detail;
        }

        public class UserDetail
        {
            public string Username { get; set; } = string.Empty;
            public DateTime? Created { get; set; }
            public string DefaultTablespace { get; set; } = string.Empty;
            public List<string> Roles { get; set; } = new List<string>();
        }

        public Task<(bool success, string message)> RegisterCustomerAsync(string username, string password, string hoTen, string email, string? soDienThoai = null, string? diaChi = null)
        {
            return _ConfirmRegistrationAsync(username, password, hoTen, email, soDienThoai, diaChi);
        }

        public async Task<(bool success, string message)> DeleteUserAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return (false, "Tên đăng nhập không hợp lệ.");
            }

            username = username.ToUpperInvariant();

            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                using (var deleteKhach = connection.CreateCommand())
                {
                    deleteKhach.Transaction = transaction;
                    deleteKhach.CommandText = "DELETE FROM TADMIN.KhachHang WHERE ORACLE_USERNAME = :username";
                    deleteKhach.Parameters.Add("username", OracleDbType.Varchar2).Value = username;
                    await deleteKhach.ExecuteNonQueryAsync();
                }

                using (var deleteNhanVien = connection.CreateCommand())
                {
                    deleteNhanVien.Transaction = transaction;
                    deleteNhanVien.CommandText = "DELETE FROM TADMIN.NHANVIEN WHERE ORACLE_USERNAME = :username";
                    deleteNhanVien.Parameters.Add("username", OracleDbType.Varchar2).Value = username;
                    await deleteNhanVien.ExecuteNonQueryAsync();
                }

                using (var dropCmd = connection.CreateCommand())
                {
                    dropCmd.Transaction = transaction;
                    dropCmd.CommandText = $"DROP USER \"{username}\" CASCADE";
                    await dropCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return (true, $"Đã xóa người dùng {username}.");
            }
            catch (OracleException ex)
            {
                try
                {
                    transaction.Rollback();
                }
                catch { }

                if (ex.Number == 1918)
                {
                    return (false, "Người dùng không tồn tại."); // ORA-01918
                }

                Console.WriteLine($"DeleteUserAsync error: {ex.Message}");
                return (false, $"Lỗi khi xóa người dùng: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> GrantRoleAsync(string username, string role)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(role))
            {
                return (false, "Tên đăng nhập hoặc quyền không được để trống.");
            }

            var normalizedRole = role.ToUpperInvariant();
            var allowedRoles = new HashSet<string> { "ROLE_CUSTOMER", "ROLE_ADMIN", "ROLE_STAFF", "ROLE_READ_ONLY" };
            if (!allowedRoles.Contains(normalizedRole))
            {
                return (false, "Quyền không hợp lệ.");
            }

            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"GRANT {normalizedRole} TO \"{username.ToUpperInvariant()}\"";
                await cmd.ExecuteNonQueryAsync();
                return (true, $"Đã cấp quyền {normalizedRole} cho {username.ToUpperInvariant()}.");
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"GrantRoleAsync error: {ex.Message}");
                return (false, $"Lỗi khi cấp quyền: {ex.Message}");
            }
        }
    }
}