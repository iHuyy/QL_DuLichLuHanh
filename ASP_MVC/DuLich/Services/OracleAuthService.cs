using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace DuLich.Services
{
    public class OracleAuthService
    {
        private readonly string _connectionString;

        // DTOs for admin UI
        public record UserInfo(string Username, DateTime? Created, string? DefaultTablespace);
        public record UserDetail(string Username, DateTime? Created, string? DefaultTablespace, IEnumerable<string> Roles);

        public OracleAuthService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<(bool success, string role)> ValidateLoginAsync(string username, string password)
        {
            try
            {
                var builder = new OracleConnectionStringBuilder(_connectionString)
                {
                    UserID = username.ToUpper(),
                    Password = password
                };

                using var connection = new OracleConnection(builder.ConnectionString);
                await connection.OpenAsync();
                Console.WriteLine($"Successfully connected as {username.ToUpper()}");

                // Determine the user's role (if any)
                var role = await GetUserRoleAsync(connection);
                Console.WriteLine($"User {username.ToUpper()} has role: {role}");

                if (!string.IsNullOrEmpty(role))
                {
                    return (true, role);
                }

                // If no role assigned yet, check existence and grant ROLE_CUSTOMER via admin connection
                using (var sysConnection = new OracleConnection(_connectionString))
                {
                    await sysConnection.OpenAsync();

                    using var checkCommand = sysConnection.CreateCommand();
                    checkCommand.CommandText = "SELECT COUNT(*) FROM all_users WHERE username = :username";
                    checkCommand.Parameters.Add("username", OracleDbType.Varchar2).Value = username.ToUpper();
                    var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                    if (count == 0)
                    {
                        // user does not exist
                        return (false, string.Empty);
                    }

                    using var grantCommand = sysConnection.CreateCommand();
                    grantCommand.CommandText = $@"GRANT ROLE_CUSTOMER TO ""{username.ToUpper()}""";
                    await grantCommand.ExecuteNonQueryAsync();
                    Console.WriteLine($"Granted ROLE_CUSTOMER to existing user {username.ToUpper()}");
                    return (true, "ROLE_CUSTOMER");
                }
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"Oracle login error: {ex.Message}");
                return (false, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return (false, string.Empty);
            }
        }

        private async Task<string> GetUserRoleAsync(OracleConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT GRANTED_ROLE 
                FROM USER_ROLE_PRIVS 
                WHERE GRANTED_ROLE IN ('ROLE_ADMIN', 'ROLE_CUSTOMER')";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader.GetString(0);
            }
            return string.Empty;
        }

        // Admin helpers
        public async Task<IEnumerable<UserInfo>> ListAllUsersAsync()
        {
            var result = new List<UserInfo>();
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            // DEFAULT_TABLESPACE is available in DBA_USERS, not ALL_USERS
            cmd.CommandText = @"SELECT username, created, default_tablespace FROM dba_users ORDER BY username";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var username = reader.GetString(0);
                DateTime? created = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                var tablespace = reader.IsDBNull(2) ? null : reader.GetString(2);
                result.Add(new UserInfo(username, created, tablespace));
            }
            return result;
        }

        public async Task<UserDetail?> GetUserAsync(string username)
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            // Use DBA_USERS to get default_tablespace
            cmd.CommandText = @"SELECT username, created, default_tablespace FROM dba_users WHERE username = :username";
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username.ToUpper();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            var uname = reader.GetString(0);
            DateTime? created = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
            var tablespace = reader.IsDBNull(2) ? null : reader.GetString(2);

            // get roles
            using var roleCmd = conn.CreateCommand();
            roleCmd.CommandText = @"SELECT GRANTED_ROLE FROM dba_role_privs WHERE grantee = :username";
            roleCmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username.ToUpper();
            var roles = new List<string>();
            using var rReader = await roleCmd.ExecuteReaderAsync();
            while (await rReader.ReadAsync()) roles.Add(rReader.GetString(0));

            return new UserDetail(uname, created, tablespace, roles);
        }

        public async Task<(bool success, string message)> DeleteUserAsync(string username)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"DROP USER \"{username.ToUpper()}\" CASCADE";
                await cmd.ExecuteNonQueryAsync();
                return (true, "Người dùng đã được xóa");
            }
            catch (OracleException ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool success, string message)> GrantRoleAsync(string username, string role)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"GRANT {role} TO \"{username.ToUpper()}\"";
                await cmd.ExecuteNonQueryAsync();
                return (true, $"Đã cấp {role} cho {username}");
            }
            catch (OracleException ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool success, string message)> RegisterCustomerAsync(string username, string password, string hoTen, string email, string? soDienThoai = null, string? diaChi = null)
        {
            using var adminConnection = new OracleConnection(_connectionString);
            await adminConnection.OpenAsync();

            try
            {
                // 1. Kiểm tra xem user đã tồn tại chưa
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

                // 2. Kiểm tra email đã tồn tại chưa
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
                    // 3. Tạo Oracle User
                    using (var createUserCommand = adminConnection.CreateCommand())
                    {
                        createUserCommand.CommandText = $@"
                            CREATE USER ""{username.ToUpper()}"" IDENTIFIED BY ""{password}""
                            PROFILE cus_profile
                            DEFAULT TABLESPACE USERS
                            TEMPORARY TABLESPACE TEMP";
                        await createUserCommand.ExecuteNonQueryAsync();
                    }

                    // 4. Cấp quyền cho user
                    using (var grantCommand = adminConnection.CreateCommand())
                    {
                        grantCommand.CommandText = $@"GRANT ROLE_CUSTOMER TO ""{username.ToUpper()}""";
                        await grantCommand.ExecuteNonQueryAsync();

                        grantCommand.CommandText = $@"GRANT UNLIMITED TABLESPACE TO ""{username.ToUpper()}""";
                        await grantCommand.ExecuteNonQueryAsync();
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"Error creating Oracle user: {ex.Message}");
                    return (false, $"Lỗi khi tạo tài khoản: {ex.Message}");
                }

                // 5. Thêm thông tin vào bảng KhachHang
                try
                {
                    using (var insertCommand = adminConnection.CreateCommand())
                    {
                        insertCommand.CommandText = @"
                            INSERT INTO TADMIN.KhachHang (HoTen, Email, SoDienThoai, DiaChi, ORACLE_USERNAME)
                            VALUES (:HoTen, :Email, :SoDienThoai, :DiaChi, :Username)";

                        insertCommand.Parameters.Add("HoTen", OracleDbType.NVarchar2).Value = hoTen;
                        insertCommand.Parameters.Add("Email", OracleDbType.NVarchar2).Value = email;
                        insertCommand.Parameters.Add("SoDienThoai", OracleDbType.NVarchar2).Value =
                            (object?)soDienThoai ?? DBNull.Value;
                        insertCommand.Parameters.Add("DiaChi", OracleDbType.NVarchar2).Value =
                            (object?)diaChi ?? DBNull.Value;
                        insertCommand.Parameters.Add("Username", OracleDbType.NVarchar2).Value = username.ToUpper();

                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"Error inserting into KhachHang: {ex.Message}");
                    // Nếu lỗi khi thêm vào bảng KhachHang, xóa user đã tạo
                    using (var dropCommand = adminConnection.CreateCommand())
                    {
                        dropCommand.CommandText = $@"DROP USER ""{username.ToUpper()}""";
                        await dropCommand.ExecuteNonQueryAsync();
                    }
                    return (false, $"Lỗi khi lưu thông tin: {ex.Message}");
                }

                return (true, "Đăng ký thành công");
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"Oracle error: {ex.Message}");
                return (false, $"Lỗi khi đăng ký: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return (false, "Đăng ký không thành công, vui lòng thử lại sau");
            }
        }
    }
}