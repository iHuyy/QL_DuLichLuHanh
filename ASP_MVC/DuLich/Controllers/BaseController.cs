using DuLich.Models.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DuLich.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;

        protected BaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            await SetUserContext();
            await next();
        }

        private async Task SetUserContext()
        { 
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var chiNhanhIdStr = User.FindFirst("MaChiNhanh")?.Value;
            int.TryParse(chiNhanhIdStr, out var chiNhanhId); // chiNhanhId will be 0 if not found or not a number

            if (!string.IsNullOrEmpty(role))
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "BEGIN TADMIN.pkg_tour_management.set_user_context(:role_name, :branch_id); END;";
                
                var roleParam = cmd.CreateParameter();
                roleParam.ParameterName = "role_name";
                roleParam.Value = role;
                cmd.Parameters.Add(roleParam);

                var branchParam = cmd.CreateParameter();
                branchParam.ParameterName = "branch_id";
                branchParam.Value = chiNhanhId == 0 ? (object)DBNull.Value : chiNhanhId;
                cmd.Parameters.Add(branchParam);
                
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
