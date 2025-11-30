using System;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using DuLich.Models;
using DuLich.Models.Data;
using System.Data;
using System.Data.Common;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using DuLich.Services;

#nullable enable

namespace DuLich.Controllers.staff
{
    [Authorize(Roles = "ROLE_STAFF")]
    public class StaffController : BaseController
    {
        private readonly ILogger<StaffController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly OracleAuthService _authService;

        public StaffController(ApplicationDbContext context, ILogger<StaffController> logger, IWebHostEnvironment env, OracleAuthService authService) : base(context)
        {
            _logger = logger;
            _env = env;
            _authService = authService;
        }

        // Helper: best-effort log of current Oracle session VPD and OLS label
        private async Task LogOracleSessionStateAsync(DbConnection conn)
        {
            try
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                try
                {
                    using var vcmd = conn.CreateCommand();
                    vcmd.CommandText = "SELECT SYS_CONTEXT('tour_management_ctx','role') AS role, SYS_CONTEXT('tour_management_ctx','branch_id') AS branch FROM DUAL";
                    using var rdr = await vcmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                    if (await rdr.ReadAsync())
                    {
                        var ctxRole = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                        var ctxBranch = rdr.IsDBNull(1) ? null : rdr.GetValue(1)?.ToString();
                        _logger?.LogInformation("[Verify] Oracle session VPD context: role={Role}, branch={Branch}", ctxRole, ctxBranch);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[Verify] Failed to query SYS_CONTEXT for VPD");
                }

                try
                {
                    using var lcmd = conn.CreateCommand();
                    lcmd.CommandText = "SELECT SA_LABEL_ADMIN.LABEL_TO_CHAR('DULICH_OLS', SA_SESSION.GET_LABEL('DULICH_OLS')) FROM DUAL";
                    var labelObj = await lcmd.ExecuteScalarAsync();
                    if (labelObj != null && labelObj != DBNull.Value)
                    {
                        _logger?.LogInformation("[Verify] Oracle session OLS label for DULICH_OLS: {Label}", labelObj.ToString());
                    }
                    else
                    {
                        _logger?.LogInformation("[Verify] Oracle session OLS label for DULICH_OLS: <null>");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[Verify] Could not read OLS session label (GET_LABEL may be unavailable or lack privileges)");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[Verify] Error while verifying Oracle session state");
            }
        }

        private static string GetStatusClass(string status)
        {
            var normalized = status?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "chờ xác nhận" or "cho xac nhan" => "warning",
                "đã xác nhận" or "da xac nhan" => "success",
                "đã hủy" or "da huy" => "danger",
                "hoàn thành" or "hoan thanh" => "info",
                _ => "secondary"
            };
        }

        private static string GetTourStatusClass(string status)
        {
            var normalized = status?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "hoạt động" => "success",
                "đang diễn ra" => "primary",
                "hoàn thành" or "đã kết thúc" => "info",
                "tạm ngưng" => "warning",
                "đã hủy" => "danger",
                "ẩn" => "secondary",
                _ => "secondary"
            };
        }

        private async Task SetUserContextForBranchAsync(string? role, int? branchId)
        {
            if (string.IsNullOrWhiteSpace(role) || !branchId.HasValue)
            {
                return;
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "BEGIN TADMIN.pkg_tour_management.set_user_context(:role_name, :branch_id); END;";

            var roleParam = new OracleParameter("role_name", OracleDbType.Varchar2) { Value = (object?)role ?? DBNull.Value };
            var branchParam = new OracleParameter("branch_id", OracleDbType.Int32) { Value = branchId.Value };
            cmd.Parameters.Add(roleParam);
            cmd.Parameters.Add(branchParam);

            await cmd.ExecuteNonQueryAsync();

            // Verify context was applied on this session — useful to debug ORA-28115 (VPD check option)
            try
            {
                using var verifyCmd = conn.CreateCommand();
                verifyCmd.CommandText = "SELECT SYS_CONTEXT('tour_management_ctx','role') AS role, SYS_CONTEXT('tour_management_ctx','branch_id') AS branch_id FROM DUAL";
                using var reader = await verifyCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (await reader.ReadAsync())
                {
                    var setRole = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var setBranch = reader.IsDBNull(1) ? null : reader.GetValue(1)?.ToString();
                    _logger?.LogInformation("DB context after set_user_context: role={Role}, branch_id={Branch}", setRole, setBranch);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Unable to verify DB context after calling set_user_context");
            }
        }

        /// <summary>
        /// Ensure the Oracle session has both VPD context (role + branch) and OLS label set on the current connection.
        /// This avoids ORA-28115 when VPD/OLS check-control validates INSERT/UPDATE.
        /// </summary>
        private async Task EnsureOracleSecurityContextAsync(DbConnection conn, string? role, int branchId)
        {
            var effectiveRole = string.IsNullOrWhiteSpace(role) ? "ROLE_STAFF" : role.Trim().ToUpperInvariant();

            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
BEGIN
  TADMIN.pkg_tour_management.set_user_context(:role_name, :branch_id);
  SA_SESSION.SET_LABEL('DULICH_OLS', :p_label);
END;";

                if (cmd is OracleCommand ocmd)
                {
                    ocmd.BindByName = true;
                    ocmd.Parameters.Add(new OracleParameter("role_name", OracleDbType.Varchar2) { Value = (object?)effectiveRole ?? DBNull.Value });
                    ocmd.Parameters.Add(new OracleParameter("branch_id", OracleDbType.Int32) { Value = branchId });
                    var label = (effectiveRole == "ROLE_ADMIN" || effectiveRole == "ROLE_STAFF") ? "INT" : "PUB";
                    ocmd.Parameters.Add(new OracleParameter("p_label", OracleDbType.Varchar2) { Value = label });
                }
                else
                {
                    var pRole = cmd.CreateParameter();
                    pRole.ParameterName = "role_name";
                    pRole.Value = (object?)effectiveRole ?? DBNull.Value;
                    cmd.Parameters.Add(pRole);

                    var pBranch = cmd.CreateParameter();
                    pBranch.ParameterName = "branch_id";
                    pBranch.Value = branchId;
                    cmd.Parameters.Add(pBranch);

                    var pLabel = cmd.CreateParameter();
                    pLabel.ParameterName = "p_label";
                    pLabel.Value = (effectiveRole == "ROLE_ADMIN" || effectiveRole == "ROLE_STAFF") ? "INT" : "PUB";
                    cmd.Parameters.Add(pLabel);
                }

                await cmd.ExecuteNonQueryAsync();
            }

            // log what the DB session sees for VPD context (helps diagnose ORA-28115)
            try
            {
                using var verifyCmd = conn.CreateCommand();
                verifyCmd.CommandText = "SELECT SYS_CONTEXT('tour_management_ctx','role'), SYS_CONTEXT('tour_management_ctx','branch_id') FROM DUAL";
                using var reader = await verifyCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (await reader.ReadAsync())
                {
                    var ctxRole = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var ctxBranch = reader.IsDBNull(1) ? null : reader.GetValue(1)?.ToString();
                    _logger?.LogInformation("Oracle session context set: role={Role}, branch_id={Branch}", ctxRole, ctxBranch);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not verify Oracle session context after setting VPD/OLS");
            }

            // Best-effort: attempt to read the OLS session label for 'DULICH_OLS' and log it.
            try
            {
                using var lblCmd = conn.CreateCommand();
                lblCmd.CommandText = "SELECT SA_LABEL_ADMIN.LABEL_TO_CHAR('DULICH_OLS', SA_SESSION.GET_LABEL('DULICH_OLS')) FROM DUAL";
                var lblObj = await lblCmd.ExecuteScalarAsync();
                if (lblObj != null && lblObj != DBNull.Value)
                {
                    _logger?.LogInformation("Oracle session OLS label for policy DULICH_OLS: {Label}", lblObj.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not read OLS session label (SA_SESSION.GET_LABEL may be unavailable)");
            }
        }

        private async Task<int> InsertImageBlobRawAsync(int tourId, byte[] data, string mimeType, string? description)
        {
            var conn = _context.Database.GetDbConnection();
            if (!(conn is OracleConnection oracleConn))
                throw new InvalidOperationException("Database connection is not OracleConnection");

            if (oracleConn.State != ConnectionState.Open)
                await oracleConn.OpenAsync();

            // If EF has a current transaction, try to reuse it
            var dbTran = _context.Database.CurrentTransaction?.GetDbTransaction();
            OracleTransaction? oracleTran = dbTran as OracleTransaction;

            // 1) Insert row with EMPTY_BLOB() and RETURNING MAANH
            using (var cmdInsert = oracleConn.CreateCommand())
            {
                if (oracleTran != null)
                    cmdInsert.Transaction = oracleTran;

                cmdInsert.BindByName = true;
                cmdInsert.CommandText = @"INSERT INTO TADMIN.ANHTOUR (DULIEUANH, LOAIANH, MATOUR, MOTA, NGAYTAILEN)
VALUES (EMPTY_BLOB(), :loai, :matour, :mota, :ngay)
RETURNING MAANH INTO :id";

                cmdInsert.Parameters.Add(new OracleParameter("loai", OracleDbType.Varchar2) { Value = (object?)mimeType ?? DBNull.Value, Size = 2000 });
                cmdInsert.Parameters.Add(new OracleParameter("matour", OracleDbType.Int32) { Value = tourId });
                cmdInsert.Parameters.Add(new OracleParameter("mota", OracleDbType.Varchar2) { Value = (object?)description ?? DBNull.Value, Size = 2000 });
                cmdInsert.Parameters.Add(new OracleParameter("ngay", OracleDbType.Date) { Value = DateTime.Now });

                var pId = new OracleParameter("id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                cmdInsert.Parameters.Add(pId);

                await cmdInsert.ExecuteNonQueryAsync();

                var idObj = pId.Value;
                if (idObj == null || idObj == DBNull.Value)
                    throw new InvalidOperationException("Failed to obtain inserted image id");

                int newId;
                // Oracle returns OracleDecimal for numeric OUT parameters; handle accordingly
                if (idObj is OracleDecimal od)
                {
                    if (od.IsNull)
                        throw new InvalidOperationException("Inserted image id is null");
                    newId = od.ToInt32();
                }
                else
                {
                    try
                    {
                        newId = Convert.ToInt32(idObj);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to convert returned image id to int", ex);
                    }
                }

                // 2) Select the BLOB locator FOR UPDATE and write bytes
                using (var cmdSelect = oracleConn.CreateCommand())
                {
                    if (oracleTran != null)
                        cmdSelect.Transaction = oracleTran;

                    cmdSelect.BindByName = true;
                    cmdSelect.CommandText = "SELECT DULIEUANH FROM TADMIN.ANHTOUR WHERE MAANH = :id FOR UPDATE";
                    cmdSelect.Parameters.Add(new OracleParameter("id", OracleDbType.Int32) { Value = newId });

                    using var reader = await cmdSelect.ExecuteReaderAsync(CommandBehavior.SingleRow);
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException("Inserted image row could not be selected for update");

                    var blob = reader.GetOracleBlob(0);
                    blob.Write(data, 0, data.Length);
                    blob.Close();

                    return newId;
                }
            }
        }

        private static bool CanCancelBooking(DatTour booking)
        {
            if (booking == null || booking.Tour == null)
                return false;

            var status = booking.TrangThaiDat?.Trim().ToLowerInvariant();

            if (status == "đã hủy" || status == "da huy" ||
                status == "hoàn thành" || status == "hoan thanh")
                return false;

            return booking.Tour.ThoiGian != null &&
                   booking.Tour.ThoiGian.Value.Date > DateTime.Now.AddDays(3).Date;
        }

        [HttpGet]
        public IActionResult CreateTour()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTour([Bind("TieuDe,NoiDen,NoiKhoiHanh,ThoiGian,SoLuong,GiaNguoiLon,GiaTreEm,MoTa")] Tour tour, List<IFormFile>? TourImages)
        {
            _logger.LogInformation("CreateTour POST invoked by {User}", User?.Identity?.Name ?? "anonymous");

            try
            {
                _logger.LogInformation("Request Method: {Method}, Content-Length: {Len}, HasForm: {HasForm}", Request?.Method, Request?.ContentLength, Request?.HasFormContentType);
                if (Request?.HasFormContentType == true)
                {
                    _logger.LogInformation("Form keys: {Keys}", string.Join(",", Request.Form.Keys));
                    _logger.LogInformation("Uploaded files count: {Count}", Request.Form?.Files?.Count ?? 0);
                    foreach (var f in Request.Form?.Files ?? Enumerable.Empty<IFormFile>())
                    {
                        _logger.LogInformation("File: {Name}, FileName: {FileName}, Length: {Len}", f.Name, f.FileName, f.Length);
                    }
                }
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed reading request form info");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                if (errors.Any())
                {
                    _logger.LogWarning("CreateTour ModelState invalid: {Errors}", string.Join("; ", errors));
                    TempData["Error"] = "Vui long kiem tra loi thong tin nhap vao: " + string.Join("; ", errors);
                }
                else
                {
                    TempData["Error"] = "Vui long kiem tra loi thong tin nhap vao!";
                }
                return View(tour);
            }

            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                TempData["Error"] = "Khong xac dinh duoc tai khoan nhan vien. Vui long dang nhap lai.";
                return View(tour);
            }

            var upperUser = username.ToUpper();
            var staff = await _context.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == upperUser);
            if (staff == null || !staff.MaChiNhanh.HasValue)
            {
                TempData["Error"] = "Khong xac dinh duoc chi nhanh cua nhan vien. Vui long dang nhap lai hoac lien he quan tri.";
                return View(tour);
            }
            var branch = await _context.ChiNhanhs.FirstOrDefaultAsync(c => c.MaChiNhanh == staff.MaChiNhanh.Value);
            if (branch == null)
            {
                TempData["Error"] = "Khong tim thay thong tin chi nhanh. Vui long lien he quan tri.";
                return View(tour);
            }

            // Force role for staff flow to avoid missing/invalid claim causing VPD=1=0
            var role = "ROLE_STAFF";
            var strategy = _context.Database.CreateExecutionStrategy();

            var result = await strategy.ExecuteAsync(async () =>
            {
                var conn = _context.Database.GetDbConnection();
                var openedConnectionHere = false;
                try
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        await _context.Database.OpenConnectionAsync();
                        openedConnectionHere = true;
                    }

                    // Ensure VPD/OLS context is applied to this exact connection (required by policy CHECK_CONTROL)
                    await EnsureOracleSecurityContextAsync(conn, role, staff.MaChiNhanh.Value);

                    // Verify context matches the tour branch to avoid ORA-28115 before hitting DB
                    try
                    {
                        using var verifyCmd = conn.CreateCommand();
                        verifyCmd.CommandText = "SELECT SYS_CONTEXT('tour_management_ctx','role'), SYS_CONTEXT('tour_management_ctx','branch_id') FROM DUAL";
                        using var reader = await verifyCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                        if (await reader.ReadAsync())
                        {
                            var ctxRole = reader.IsDBNull(0) ? null : reader.GetString(0);
                            var ctxBranch = reader.IsDBNull(1) ? null : reader.GetValue(1)?.ToString();
                            _logger.LogInformation("Context before SaveChanges: role={Role}, branch_id={Branch}, tour.MaChiNhanh={TourBranch}", ctxRole, ctxBranch, staff.MaChiNhanh);
                            if (ctxBranch == null || ctxBranch != staff.MaChiNhanh.Value.ToString())
                            {
                                TempData["Error"] = "Chi nhanh trong session DB khong khop. Vui long dang nhap lai.";
                                return (IActionResult)View(tour);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not verify context before SaveChanges");
                    }

                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            // Set status to a value that satisfies TOUR_TRANGTHAI_CHK
                            tour.TrangThai = "Hoạt động";
                            tour.MaChiNhanh = staff.MaChiNhanh;

                            _context.Tours.Add(tour);
                            try
                            {
                                _logger.LogInformation("About to SaveChanges for tour with MaChiNhanh={Branch}", tour.MaChiNhanh);
                                // Ensure security context is applied on this connection immediately before insert
                                try { await EnsureOracleSecurityContextAsync(conn, role, staff.MaChiNhanh.Value); } catch (Exception ex) { _logger?.LogWarning(ex, "Failed to reapply Oracle security context before SaveChanges"); }
                                // Verify session state just before insert
                                try { await LogOracleSessionStateAsync(conn); } catch { }
                                await _context.SaveChangesAsync();
                            }
                            catch (DbUpdateException dbEx)
                            {
                                var inner = dbEx.InnerException;
                                _logger.LogError(dbEx, "CreateTour SaveChanges failed for tour {TourId}. Inner: {Inner}", tour.MaTour, inner?.ToString());
                                await transaction.RollbackAsync();
                                TempData["Error"] = "Khong the tao tour (luu Tour that bai): " + (inner?.Message ?? dbEx.Message);
                                return View(tour);
                            }

                            // Generate QR code and persist it immediately to avoid batching UPDATE + large BLOB INSERT
                            var scheme = Request?.Scheme ?? "http";
                            var tourUrl = Url.Action("TourDetails", "Customer", new { id = tour.MaTour }, scheme) ?? $"/Customer/TourDetails/{tour.MaTour}";
                            var qrCodeFileName = $"tour_{tour.MaTour}_{DateTime.Now:yyyyMMddHHmmss}.png";
                            var qrCodeDirectory = Path.Combine(_env.WebRootPath, "images", "qrcode");
                            Directory.CreateDirectory(qrCodeDirectory);

                            using (var qrGenerator = new QRCoder.QRCodeGenerator())
                            {
                                var qrCodeData = qrGenerator.CreateQrCode(tourUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
                                using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
                                var qrCodeImage = qrCode.GetGraphic(10);
                                var qrCodePath = Path.Combine(qrCodeDirectory, qrCodeFileName);
                                await System.IO.File.WriteAllBytesAsync(qrCodePath, qrCodeImage);
                                tour.QR = $"/images/qrcode/{qrCodeFileName}";
                            }

                            _logger.LogInformation("Saving QR for tour {TourId} before inserting images", tour.MaTour);
                            // Ensure security context before saving QR
                            try { await EnsureOracleSecurityContextAsync(conn, role, staff.MaChiNhanh.Value); } catch (Exception ex) { _logger?.LogWarning(ex, "Failed to reapply Oracle security context before SaveChanges (QR)"); }
                            // Verify session state before saving QR
                            try { await LogOracleSessionStateAsync(conn); } catch { }
                            await _context.SaveChangesAsync();

                            if (TourImages?.Any() == true)
                            {
                                var validImages = TourImages.Where(f => f != null && f.Length > 0).ToList();
                                _logger.LogInformation("Preparing to save {Count} image(s) for tour {TourId}", validImages.Count, tour.MaTour);

                                // Use Oracle-specific BLOB write to avoid ORA-01460 issues with EF batching
                                foreach (var imageFile in validImages)
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        await imageFile.CopyToAsync(ms);
                                        var imageData = ms.ToArray();

                                        string Truncate(string? input, int max)
                                        {
                                            if (string.IsNullOrEmpty(input)) return string.Empty;
                                            return input.Length <= max ? input : input.Substring(0, max);
                                        }

                                        try
                                        {
                                            // Insert the BLOB using a raw Oracle command (EMPTY_BLOB + SELECT ... FOR UPDATE)
                                            var newImageId = await InsertImageBlobRawAsync(tour.MaTour, imageData, Truncate(imageFile.ContentType, 50), Truncate(imageFile.FileName, 300));
                                            _logger.LogInformation("Inserted image id {ImageId} for tour {TourId}", newImageId, tour.MaTour);
                                        }
                                        catch (Exception imgEx)
                                        {
                                            _logger.LogError(imgEx, "Failed saving image for tour {TourId}. Rolling back.", tour.MaTour);
                                            await transaction.RollbackAsync();
                                            TempData["Error"] = "Loi khi luu anh: " + (imgEx.Message ?? "Khong ro");
                                            return (IActionResult)View(tour);
                                        }
                                    }
                                }
                            }

                            try
                            {
                                _logger.LogInformation("Saving tour and images to database for tour {TourId}", tour.MaTour);
                                // Ensure security context before final save/commit
                                try { await EnsureOracleSecurityContextAsync(conn, role, staff.MaChiNhanh.Value); } catch (Exception ex) { _logger?.LogWarning(ex, "Failed to reapply Oracle security context before final SaveChanges"); }
                                // Verify session state before final save/commit
                                try { await LogOracleSessionStateAsync(conn); } catch { }
                                await _context.SaveChangesAsync();
                                await transaction.CommitAsync();
                            }
                            catch (DbUpdateException dbEx)
                            {
                                var inner = dbEx.InnerException;
                                _logger.LogError(dbEx, "DbUpdateException while saving tour {TourId}. Inner: {Inner}", tour.MaTour, inner?.ToString());
                                await transaction.RollbackAsync();
                                var innerMsg = inner?.Message ?? dbEx.Message;
                                TempData["Error"] = "Da xay ra loi khi luu du lieu: " + innerMsg;
                                return (IActionResult)View(tour);
                            }

                            TempData["Success"] = "Them tour moi thanh cong!";
                            return (IActionResult)RedirectToAction(nameof(Tours));
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "Error creating tour. Transaction rolled back.");
                            TempData["Error"] = "Co loi xay ra, khong the tao tour: " + ex.Message;
                            return (IActionResult)View(tour);
                        }
                    }
                }
                finally
                {
                    if (openedConnectionHere)
                    {
                        try { await _context.Database.CloseConnectionAsync(); } catch { }
                    }
                }
            });

            return result;
        }

        [HttpGet]
        public async Task<IActionResult> EditTour(int id)
        {
            var t = await _context.Tours.FindAsync(id);
            if (t == null)
            {
                TempData["Error"] = "Tour không tồn tại hoặc bạn không có quyền sửa tour này.";
                return RedirectToAction(nameof(Tours));
            }
            return View(t);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTour(int id, [Bind("MaTour,TieuDe,NoiDen,NoiKhoiHanh,ThoiGian,SoLuong,GiaNguoiLon,GiaTreEm,MoTa,TrangThai,QR")] Tour tour)
        {
            if (id != tour.MaTour)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tour);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật tour thành công";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Tours.Any(e => e.MaTour == tour.MaTour))
                    {
                        return NotFound();
                    }
                    else
                    {
                        TempData["Error"] = "Tour không tồn tại hoặc bạn không có quyền sửa tour này.";
                    }
                }
                return RedirectToAction(nameof(Tours));
            }
            return View(tour);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTour(int id)
        {
            var t = await _context.Tours.FindAsync(id);
            if (t == null)
            {
                TempData["Error"] = "Tour không tồn tại hoặc bạn không có quyền xóa tour này.";
                return RedirectToAction(nameof(Tours));
            }

            var images = _context.AnhTours.Where(a => a.MaTour == id).ToList();
            if (images.Any())
            {
                _context.AnhTours.RemoveRange(images);
            }

            _context.Tours.Remove(t);

            try
            {
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(t.QR))
                {
                    var qrPath = Path.Combine(_env.WebRootPath, t.QR.TrimStart('/'));
                    if (System.IO.File.Exists(qrPath))
                    {
                        System.IO.File.Delete(qrPath);
                    }
                }

                TempData["Success"] = "Xóa tour thành công";
            }
            catch (DbUpdateException ex)
            {
                var innerException = ex.InnerException;
                if (innerException is Oracle.ManagedDataAccess.Client.OracleException oracleEx && oracleEx.Number == 2292)
                {
                    TempData["Error"] = "Không thể xóa tour. Có thể do đã có khách hàng đặt tour này hoặc có dữ liệu liên quan khác.";
                }
                else
                {
                    TempData["Error"] = "Đã xảy ra lỗi khi xóa tour: " + (innerException?.Message ?? ex.Message);
                }
            }

            return RedirectToAction(nameof(Tours));
        }

        // Trang tổng quan mặc định
        [HttpGet]
        public IActionResult Index() => View();

        // Trang Dashboard (alias nếu vào /Staff/Dashboard)
        [HttpGet]
        public IActionResult Dashboard() => View("Dashboard");

        // Alias nếu người dùng gõ /staff/booking (số ít)
        [HttpGet("Staff/Booking")]
        public Task<IActionResult> Booking() => Bookings();

        // Danh sách booking của nhân viên
        [HttpGet]
        public async Task<IActionResult> Bookings()
        {
            await UpdateDepartedBookingsToCompletedAsync();

            var bookings = await _context.DatTours
                .AsNoTracking()
                .Include(d => d.Tour)
                .Include(d => d.KhachHang)
                .OrderByDescending(d => d.NgayDat)
                .Take(100)
                .Select(d => new BookingViewModel
                {
                    Id = d.MaDatTour,
                    CustomerName = d.KhachHang == null ? "Không xác định" : d.KhachHang.HoTen ?? "Không xác định",
                    TourName = d.Tour == null ? "Không xác định" : d.Tour.TieuDe ?? "Không xác định",
                    BookingDate = d.NgayDat ?? DateTime.Now,
                    Quantity = (d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0),
                    TotalAmount = d.TongTien ?? 0,
                    Status = d.TrangThaiDat ?? "Chưa xác định",
                    StatusClass = GetStatusClass(d.TrangThaiDat ?? ""),
                    CanCancel = CanCancelBooking(d)
                })
                .ToListAsync();

            return View(bookings);
        }

        public async Task<IActionResult> Tours()
        {
            var tours = await _context.Tours.AsNoTracking().OrderByDescending(t => t.MaTour).ToListAsync();

            var bookedByTour = await _context.DatTours
                .Where(d => d.TrangThaiDat != null && d.TrangThaiDat.ToLower().Contains("xac nhan"))
                .GroupBy(d => d.MaTour)
                .Select(g => new
                {
                    MaTour = g.Key,
                    SoNguoi = g.Sum(d => (d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0))
                })
                .ToDictionaryAsync(x => x.MaTour.GetValueOrDefault(), x => x.SoNguoi);

            var tourViewModels = tours.Select(t =>
            {
                var used = bookedByTour.TryGetValue(t.MaTour, out var soNguoi) ? soNguoi : 0;
                var tongCho = t.SoLuong ?? 0;
                var soConLai = Math.Max(0, tongCho - used);

                return new TourViewModel
                {
                    Id = t.MaTour,
                    MaTour = t.MaTour.ToString(),
                    TenTour = t.TieuDe ?? "Chua dat ten",
                    DiemDen = t.NoiDen ?? "Chua xac dinh",
                    NgayKhoiHanh = t.ThoiGian ?? DateTime.Now,
                    Gia = t.GiaNguoiLon ?? 0,
                    SoLuong = tongCho,
                    SoChoConLai = soConLai,
                    TrangThai = t.TrangThai ?? "Chua xac dinh",
                    QR = t.QR ?? "",
                    StatusClass = GetTourStatusClass(t.TrangThai ?? "")
                };
            }).ToList();

            return View(tourViewModels);
        }


        [HttpGet]
        public IActionResult Invoices() => View();

        public async Task<IActionResult> TourDetails(int id)
        {
            var tour = await _context.Tours
               .Include(t => t.AnhTours)
               .AsNoTracking()
               .FirstOrDefaultAsync(m => m.MaTour == id);

            if (tour == null)
            {
                return NotFound();
            }

            var soLuongDat = await _context.DatTours
                .Where(d => d.MaTour == id && d.TrangThaiDat == "Đã xác nhận")
                .SumAsync(d => (int?)(d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0)) ?? 0;

            var tourViewModel = new TourViewModel
            {
                Id = tour.MaTour,
                MaTour = tour.MaTour.ToString(),
                TenTour = tour.TieuDe ?? "Chưa đặt tên",
                DiemDen = tour.NoiDen ?? "Chưa xác định",
                NoiKhoiHanh = tour.NoiKhoiHanh ?? "Chưa xác định",
                NgayKhoiHanh = tour.ThoiGian ?? DateTime.Now,
                Gia = tour.GiaNguoiLon ?? 0,
                GiaTreEm = tour.GiaTreEm,
                SoLuong = tour.SoLuong ?? 0,
                SoChoConLai = (tour.SoLuong ?? 0) - soLuongDat,
                TrangThai = tour.TrangThai ?? "Chưa xác định",
                MoTa = tour.MoTa ?? "",
                QR = tour.QR ?? "",
                ChiNhanh = tour.ChiNhanh?.TenChiNhanh ?? "",
                StatusClass = GetTourStatusClass(tour.TrangThai ?? ""),
                AnhTours = tour.AnhTours
            };

            return View(tourViewModel);
        }

        public async Task<IActionResult> Customers()
        {
            var customers = await _context.KhachHangs
                .AsNoTracking()
                .Where(k => k.VaiTro == "KhachHang")
                .Select(k => new CustomerViewModel
                {
                    Id = k.MaKhachHang,
                    MaKH = k.MaKhachHang.ToString(),
                    HoTen = k.HoTen ?? "Không xác định",
                    Email = k.Email ?? "",
                    SoDienThoai = k.SoDienThoai ?? "",
                    NgaySinh = k.NgayTao,
                    DiaChi = k.DiaChi ?? "",
                    SoTourDaDat = _context.DatTours
                        .Where(d => d.MaKhachHang == k.MaKhachHang)
                        .Count()
                })
                .Take(100)
                .ToListAsync();

            return View(customers);
        }

        [HttpPost]
        public async Task<IActionResult> RemoteLogout(int userId, string userType)
        {
            // Only staff/admin should call this. This action marks active sessions for the user as inactive.
            try
            {
                var sessions = _context.UserSessions.Where(s => s.UserId == userId && s.UserType == userType);
                if (sessions.Any())
                {
                    _context.UserSessions.RemoveRange(sessions);
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã đăng xuất các phiên của người dùng.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remote logout user {UserId}", userId);
                TempData["Error"] = "Không thể đăng xuất người dùng lúc này.";
            }

            return RedirectToAction(nameof(Customers));
        }

        public IActionResult Reports() => View();

        public async Task<IActionResult> Profile()
        {
            var model = await BuildStaffProfileViewModelAsync();
            if (model == null)
            {
                return RedirectToAction("Login", "Admin");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(StaffProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var existing = await BuildStaffProfileViewModelAsync();
                if (existing == null)
                {
                    return RedirectToAction("Login", "Admin");
                }
                existing.HoTen = model.HoTen;
                existing.Email = model.Email;
                existing.SoDienThoai = model.SoDienThoai;
                return View("Profile", existing);
            }

            var staff = await _context.NhanViens
                .FirstOrDefaultAsync(n => n.MaNhanVien == model.MaNhanVien);

            if (staff == null)
            {
                return NotFound();
            }

            staff.HoTen = model.HoTen;
            staff.Email = model.Email;
            staff.SoDienThoai = model.SoDienThoai;
            _context.NhanViens.Update(staff);
            await _context.SaveChangesAsync();

            TempData["SuccessProfile"] = "Thông tin cá nhân đã được cập nhật.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var viewModel = await BuildStaffProfileViewModelAsync(model);
                if (viewModel == null)
                {
                    return RedirectToAction("Login", "Admin");
                }
                return View("Profile", viewModel);
            }

            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Admin");
            }

            var (success, message) = await _authService.ChangePasswordAsync(username, model.NewPassword);
            if (success)
            {
                TempData["SuccessPassword"] = "Mật khẩu đã được đổi thành công.";
            }
            else
            {
                TempData["ErrorPassword"] = message ?? "Không thể đổi mật khẩu lúc này.";
            }

            return RedirectToAction(nameof(Profile));
        }

        private async Task<StaffProfileViewModel?> BuildStaffProfileViewModelAsync(ChangePasswordViewModel? changePassword = null)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return null;
            }

            var staff = await _context.NhanViens
                .Include(n => n.ChiNhanh)
                .FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == username.ToUpper());

            if (staff == null)
            {
                return null;
            }

            return new StaffProfileViewModel
            {
                MaNhanVien = staff.MaNhanVien,
                HoTen = staff.HoTen,
                Email = staff.Email,
                SoDienThoai = staff.SoDienThoai,
                BranchName = staff.ChiNhanh?.TenChiNhanh,
                ChangePassword = changePassword ?? new ChangePasswordViewModel()
            };
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var username = User?.Identity?.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    var staff = await _context.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == username.ToUpper());
                    if (staff != null)
                    {
                        var sessions = _context.UserSessions.Where(s => s.UserId == staff.MaNhanVien).ToList();
                        if (sessions.Any())
                        {
                            _context.UserSessions.RemoveRange(sessions);
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        // fallback: remove by cookie
                        var sessionId = Request.Cookies["USER_SESSION_ID"];
                        if (!string.IsNullOrEmpty(sessionId))
                        {
                            var sess = await _context.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                            if (sess != null)
                            {
                                _context.UserSessions.Remove(sess);
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignore DB cleanup errors during logout
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Admin");
        }
    }
}
