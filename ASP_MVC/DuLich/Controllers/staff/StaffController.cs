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

#nullable enable

namespace DuLich.Controllers.staff
{
    [Authorize(Roles = "ROLE_STAFF")]
    public class StaffController : BaseController
    {
        private readonly ILogger<StaffController> _logger;
        private readonly IWebHostEnvironment _env;

        public StaffController(ApplicationDbContext context, ILogger<StaffController> logger, IWebHostEnvironment env) : base(context)
        {
            _logger = logger;
            _env = env;
        }

        private static string GetStatusClass(string status)
        {
            return status?.ToLower() switch
            {
                "chờ xử lý" => "warning",
                "đã xác nhận" => "success",
                "đã hủy" => "danger",
                "hoàn thành" => "info",
                _ => "secondary"
            };
        }

        private static string GetTourStatusClass(string status)
        {
            return status?.ToLower() switch
            {
                "hoạt động" => "success",
                "tạm ngưng" => "warning",
                "đã kết thúc" => "info",
                "đã hủy" => "danger",
                "ẩn" => "secondary",
                _ => "secondary"
            };
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

            if (booking.TrangThaiDat?.ToLower() == "đã hủy" ||
                booking.TrangThaiDat?.ToLower() == "hoàn thành")
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
                // Log ModelState errors to help debugging
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                if (errors.Any())
                {
                    _logger.LogWarning("CreateTour ModelState invalid: {Errors}", string.Join("; ", errors));
                    TempData["Error"] = "Vui lòng kiểm tra lại thông tin nhập vào: " + string.Join("; ", errors);
                }
                else
                {
                    TempData["Error"] = "Vui lòng kiểm tra lại thông tin nhập vào!";
                }
                return View(tour);
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    tour.TrangThai = "Hoạt động";
                    _context.Tours.Add(tour);
                    await _context.SaveChangesAsync();

                    // Generate QR code and persist it immediately to avoid batching UPDATE + large BLOB INSERT
                    var scheme = Request?.Scheme ?? "http";
                    var tourUrl = Url.Action("TourDetails", "Staff", new { id = tour.MaTour }, scheme) ?? $"/Staff/TourDetails/{tour.MaTour}";
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

                    // Save the QR update separately before inserting BLOB images
                    _logger.LogInformation("Saving QR for tour {TourId} before inserting images", tour.MaTour);
                    await _context.SaveChangesAsync();

                    // Lưu ảnh dưới dạng BLOB (ghi log chi tiết để debug lỗi DB)
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
                                    TempData["Error"] = "Lỗi khi lưu ảnh: " + (imgEx.Message ?? "Không rõ");
                                    return View(tour);
                                }
                            }
                        }
                    }

                    try
                    {
                        _logger.LogInformation("Saving tour and images to database for tour {TourId}", tour.MaTour);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch (DbUpdateException dbEx)
                    {
                        // Log inner exception details (Oracle exception info typically in InnerException)
                        var inner = dbEx.InnerException;
                        _logger.LogError(dbEx, "DbUpdateException while saving tour {TourId}. Inner: {Inner}", tour.MaTour, inner?.ToString());
                        await transaction.RollbackAsync();
                        var innerMsg = inner?.Message ?? dbEx.Message;
                        TempData["Error"] = "Đã xảy ra lỗi khi lưu dữ liệu: " + innerMsg;
                        return View(tour);
                    }

                    TempData["Success"] = "Thêm tour mới thành công!";
                    return RedirectToAction(nameof(Tours));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating tour. Transaction rolled back.");
                    // Surface the error message for easier debugging during development
                    TempData["Error"] = "Có lỗi xảy ra, không thể tạo tour: " + ex.Message;
                    return View(tour);
                }
            }
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

        public async Task<IActionResult> Tours()
        {
            var tours = await _context.Tours.AsNoTracking().OrderByDescending(t => t.MaTour).ToListAsync();
            
            var tourViewModels = tours.Select(t => new TourViewModel
            {
                Id = t.MaTour,
                MaTour = t.MaTour.ToString(),
                TenTour = t.TieuDe ?? "Chưa đặt tên",
                DiemDen = t.NoiDen ?? "Chưa xác định",
                NgayKhoiHanh = t.ThoiGian ?? DateTime.Now,
                Gia = t.GiaNguoiLon ?? 0,
                SoLuong = t.SoLuong ?? 0,
                TrangThai = t.TrangThai ?? "Chưa xác định",
                QR = t.QR ?? "",
                StatusClass = GetTourStatusClass(t.TrangThai ?? "")
            }).ToList();

            return View(tourViewModels);
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> Bookings()
        {
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
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Admin");
            }

            var staff = await _context.NhanViens
                .FirstOrDefaultAsync(k => k.ORACLE_USERNAME == username);

            if (staff == null)
            {
                return NotFound();
            }

            return View(staff);
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