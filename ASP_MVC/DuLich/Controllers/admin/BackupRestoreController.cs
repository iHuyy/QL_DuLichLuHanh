using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DuLich.Controllers.Admin
{
    [Authorize(Roles = "ROLE_ADMIN")]
    [Route("admin/backup-restore")]
    public class BackupRestoreController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BackupRestoreController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new BackupRestoreViewModel();
            var fileList = new System.Collections.Generic.List<string>();
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT FILENAME FROM TADMIN.BACKUP_FILES_EXTERNAL ORDER BY FILENAME DESC";
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var fullPath = reader.GetString(0);
                            if (!string.IsNullOrEmpty(fullPath))
                            {
                                fileList.Add(Path.GetFileName(fullPath));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If something goes wrong (e.g., external table not set up), don't crash the page.
                // Just show an error message.
                TempData["Error"] = "Không thể lấy danh sách file sao lưu. Vui lòng kiểm tra lại cấu hình bảng ngoài (external table). Lỗi: " + ex.Message;
            }
            finally
            {
                // Ensure the connection is closed
                if (_context.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                {
                    await _context.Database.GetDbConnection().CloseAsync();
                }
            }

            model.BackupFiles = fileList;
            return View("~/Views/admin/BackupRestore.cshtml", model);
        }

        [HttpPost("create-backup")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBackup()
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("BEGIN TADMIN.SP_TAO_BAN_SAO_LUU; END;");
                TempData["Success"] = "Yêu cầu sao lưu đã được bắt đầu thành công. Quá trình này sẽ chạy trong nền. Vui lòng tải lại trang sau ít phút để xem file mới.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Đã xảy ra lỗi khi bắt đầu quá trình sao lưu: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost("restore-backup")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreBackup(BackupRestoreViewModel model)
        {
            if (string.IsNullOrEmpty(model.BackupFileName))
            {
                TempData["Error"] = "Vui lòng chọn một file sao lưu để phục hồi.";
                return RedirectToAction("Index");
            }

            try
            {
                var sql = "BEGIN TADMIN.SP_PHUC_HOI_TU_BAN_SAO_LUU(:p_dump_file); END;";
                await _context.Database.ExecuteSqlRawAsync(sql, new Oracle.ManagedDataAccess.Client.OracleParameter("p_dump_file", model.BackupFileName));

                TempData["Success"] = "Yêu cầu phục hồi đã được bắt đầu thành công. Quá trình này sẽ chạy trong nền.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Đã xảy ra lỗi khi bắt đầu quá trình phục hồi: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
