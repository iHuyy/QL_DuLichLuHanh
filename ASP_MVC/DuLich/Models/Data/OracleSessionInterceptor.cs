using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oracle.ManagedDataAccess.Client;

namespace DuLich.Models.Data
{
    public class OracleSessionInterceptor : DbConnectionInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OracleSessionInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            var identifier = _httpContextAccessor?.HttpContext?.User?.FindFirst("ChiNhanh")?.Value;

            if (!string.IsNullOrEmpty(identifier) && connection is OracleConnection oraConn)
            {
                using var cmd = oraConn.CreateCommand();
                cmd.CommandText = "BEGIN DBMS_SESSION.SET_IDENTIFIER(:id); END;";
                var p = cmd.CreateParameter();
                p.ParameterName = "id";
                p.Value = identifier;
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}