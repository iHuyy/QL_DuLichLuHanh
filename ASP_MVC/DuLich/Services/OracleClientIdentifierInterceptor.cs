using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using DuLich.Models.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DuLich.Services
{
    // Interceptor sets CLIENT_IDENTIFIER on the Oracle session according to the logged-in staff's branch
    public class OracleClientIdentifierInterceptor : DbConnectionInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServiceProvider _provider;

        public OracleClientIdentifierInterceptor(IHttpContextAccessor httpContextAccessor, IServiceProvider provider)
        {
            _httpContextAccessor = httpContextAccessor;
            _provider = provider;
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

                // Use FromSqlRaw with parameters to prevent SQL injection
                var staff = await db.NhanViens
                    .FromSqlRaw("SELECT * FROM TADMIN.NHANVIEN WHERE ORACLE_USERNAME = :username",
                              new Oracle.ManagedDataAccess.Client.OracleParameter("username", username))
                    .FirstOrDefaultAsync(cancellationToken);

                var branch = staff?.ChiNhanh;
                if (string.IsNullOrEmpty(branch)) return;

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "BEGIN DBMS_SESSION.SET_IDENTIFIER(:id); END;";
                var p = cmd.CreateParameter();
                p.ParameterName = "id";
                p.Value = branch;
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // swallow - don't want to break normal DB ops if interceptor fails
            }
        }
    }
}
