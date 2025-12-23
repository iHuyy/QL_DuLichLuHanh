using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using DuLich.Models.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DuLich.Services
{
    // Interceptor sets CLIENT_IDENTIFIER on the Oracle session according to the logged-in staff's branch
    public class OracleClientIdentifierInterceptor : DbConnectionInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServiceProvider _provider;
        private readonly ILogger<OracleClientIdentifierInterceptor> _logger;

        public OracleClientIdentifierInterceptor(IHttpContextAccessor httpContextAccessor, IServiceProvider provider, ILogger<OracleClientIdentifierInterceptor> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _provider = provider;
            _logger = logger;
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null || !user.Identity?.IsAuthenticated == true) return;

                // Only set for staff role
                if (!user.IsInRole("ROLE_STAFF")) return;

                var username = user.Identity?.Name;
                if (string.IsNullOrEmpty(username)) return;

                // Resolve a scoped DbContext to read staff branch (create scope to avoid using same connection)
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Use FromSqlRaw with parameters to prevent SQL injection, and UPPER for case-insensitivity
                var staff = await db.NhanViens
                    .FromSqlRaw("SELECT * FROM TADMIN.NHANVIEN WHERE UPPER(ORACLE_USERNAME) = UPPER(:username)",
                              new Oracle.ManagedDataAccess.Client.OracleParameter("username", username))
                    .FirstOrDefaultAsync(cancellationToken);

                var branchId = staff?.MaChiNhanh?.ToString();
                if (string.IsNullOrEmpty(branchId)) 
                {
                    // Log if branch is not found for a logged-in staff member
                    _logger.LogWarning($"Branch not found for staff user: {username}");
                    return;
                }

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "BEGIN DBMS_SESSION.SET_IDENTIFIER(:id); END;";
                var p = cmd.CreateParameter();
                p.ParameterName = "id";
                p.Value = branchId;
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error in OracleClientIdentifierInterceptor.");
                // swallow - don't want to break normal DB ops if interceptor fails
            }
        }
    }
}