using System;
using System.Data.Common;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
                    // Map user role to OLS label (policy 'DULICH_OLS' uses labels 'PUB' and 'INT')
                    string labelValue = "PUB";
                    if (!string.IsNullOrEmpty(role))
                    {
                        var r = role.ToUpperInvariant();
                        if (r == "ROLE_ADMIN" || r == "ROLE_STAFF")
                        {
                            labelValue = "INT";
                        }
                        else if (r == "ROLE_CUSTOMER")
                        {
                            labelValue = "PUB";
                        }
                    }

                    using var cmd = oraConn.CreateCommand();
                    cmd.BindByName = true;
                    cmd.CommandText = @"
BEGIN
  -- Set OLS session label for the policy 'DULICH_OLS' using a simple label name (PUB/INT).
  SA_SESSION.SET_LABEL('DULICH_OLS', :label_char);

  -- Set VPD context for branch/role
  TADMIN.pkg_tour_management.set_user_context(:role_name, :branch_id);
  DBMS_SESSION.SET_IDENTIFIER(:identifier);
END;";

                    // label parameter (PUB/INT)
                    var pLabel = cmd.CreateParameter();
                    pLabel.ParameterName = "label_char";
                    pLabel.Value = (object?)labelValue ?? DBNull.Value;
                    cmd.Parameters.Add(pLabel);

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
                    // Verify what the DB session sees for VPD and OLS label (best-effort, don't throw on failure)
                    try
                    {
                        using var ver = oraConn.CreateCommand();
                        ver.CommandText = "SELECT SYS_CONTEXT('tour_management_ctx','role') AS role, SYS_CONTEXT('tour_management_ctx','branch_id') AS branch FROM DUAL";
                        using var rdr = await ver.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
                        if (await rdr.ReadAsync(cancellationToken))
                        {
                            var ctxRole = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                            var ctxBranch = rdr.IsDBNull(1) ? null : rdr.GetValue(1)?.ToString();
                            var logger = _httpContextAccessor?.HttpContext?.RequestServices?.GetService(typeof(ILogger<OracleSessionInterceptor>)) as ILogger<OracleSessionInterceptor>;
                            logger?.LogInformation("Oracle session VPD context: role={Role}, branch={Branch}", ctxRole, ctxBranch);
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            var logger = _httpContextAccessor?.HttpContext?.RequestServices?.GetService(typeof(ILogger<OracleSessionInterceptor>)) as ILogger<OracleSessionInterceptor>;
                            logger?.LogWarning(ex, "Failed to query VPD context after setting session context");
                        }
                        catch { }
                    }

                    try
                    {
                        using var lbl = oraConn.CreateCommand();
                        // Attempt to read the current session label for policy 'DULICH_OLS'. This may not be available in all DB setups,
                        // so wrap in try/catch and only log if successful.
                        lbl.CommandText = "SELECT SA_LABEL_ADMIN.LABEL_TO_CHAR('DULICH_OLS', SA_SESSION.GET_LABEL('DULICH_OLS')) FROM DUAL";
                        var labelObj = await lbl.ExecuteScalarAsync(cancellationToken);
                        if (labelObj != null && labelObj != DBNull.Value)
                        {
                            var logger = _httpContextAccessor?.HttpContext?.RequestServices?.GetService(typeof(ILogger<OracleSessionInterceptor>)) as ILogger<OracleSessionInterceptor>;
                            logger?.LogInformation("Oracle session OLS label for policy DULICH_OLS: {Label}", labelObj.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            var logger = _httpContextAccessor?.HttpContext?.RequestServices?.GetService(typeof(ILogger<OracleSessionInterceptor>)) as ILogger<OracleSessionInterceptor>;
                            logger?.LogWarning(ex, "Could not read OLS session label (SA_SESSION.GET_LABEL may be unavailable)");
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    // Log warning but do not block the request
                    try
                    {
                        var logger = _httpContextAccessor?.HttpContext?.RequestServices?.GetService(typeof(ILogger<OracleSessionInterceptor>)) as ILogger<OracleSessionInterceptor>;
                        logger?.LogWarning(ex, "Failed to set Oracle session context.");
                    }
                    catch
                    {
                        // swallow any logging issues
                    }
                }
            }

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
