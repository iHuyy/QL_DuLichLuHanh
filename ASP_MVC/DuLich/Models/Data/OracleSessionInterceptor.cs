using System;
using System.Data.Common;
using System.Security.Claims;
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
            // Get the username and role/branch from the HttpContext
            var user = _httpContextAccessor?.HttpContext?.User;
            var identifier = user?.Identity?.Name;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value;
            var branchClaim = user?.FindFirst("MaChiNhanh")?.Value;

            if (connection is OracleConnection oraConn)
            {
                // Set session language settings for proper character encoding
                try
                {
                    using var nlsCmd = oraConn.CreateCommand();
                    nlsCmd.CommandText = "ALTER SESSION SET NLS_LANGUAGE = 'VIETNAMESE' NLS_TERRITORY = 'VIETNAM'";
                    await nlsCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch
                {
                    // Log or handle the exception if NLS settings fail, but don't block the connection
                }

                try
                {
                    using var cmd = oraConn.CreateCommand();
                    cmd.BindByName = true;
                    cmd.CommandText = @"
BEGIN
  TADMIN.pkg_tour_management.set_user_context(:role_name, :branch_id);
  DBMS_SESSION.SET_IDENTIFIER(:identifier);
END;";

                    // role context
                    var pRole = cmd.CreateParameter();
                    pRole.ParameterName = "role_name";
                    pRole.Value = (object?)role ?? DBNull.Value;
                    cmd.Parameters.Add(pRole);

                    // branch context (nullable integer)
                    var pBranch = cmd.CreateParameter();
                    pBranch.ParameterName = "branch_id";
                    if (int.TryParse(branchClaim, out var branchId))
                        pBranch.Value = branchId;
                    else
                        pBranch.Value = DBNull.Value;
                    pBranch.DbType = System.Data.DbType.Int32;
                    cmd.Parameters.Add(pBranch);

                    // identifier for auditing
                    var pId = cmd.CreateParameter();
                    pId.ParameterName = "identifier";
                    pId.Value = (object?)identifier ?? DBNull.Value;
                    cmd.Parameters.Add(pId);

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch
                {
                    // Do not block requests if context setting fails
                }
            }

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
