using System;
using System.Linq;
using System.Security.Claims;
using DuLich.Models;
using DuLich.Models.Data;
using DuLich.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DuLich.Controllers.Api
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "ROLE_ADMIN,ROLE_STAFF")]
    public class AdminBookingsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly RSAService _rsaService;

        public AdminBookingsController(ApplicationDbContext db, RSAService rsaService)
        {
            _db = db;
            _rsaService = rsaService;
        }

        [HttpGet("bookings")]
        public async Task<IActionResult> GetBookings()
        {
            var query = _db.DatTours
                .Include(d => d.Tour)
                .Include(d => d.HoaDon)
                .Include(d => d.KhachHang)
                .AsQueryable();

            // If user is staff, filter by their branch. Admin sees all.
            if (User.IsInRole("ROLE_STAFF"))
            {
                var username = User.Identity?.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    var staff = await _db.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == username.ToUpper());
                    if (staff != null && staff.MaChiNhanh.HasValue)
                    {
                        query = query.Where(d => d.Tour != null && d.Tour.MaChiNhanh == staff.MaChiNhanh);
                    }
                    else
                    {
                        // Staff not found or no branch, return empty list
                        return Ok(new { data = new List<object>() });
                    }
                }
            }

            var data = await query
                .AsNoTracking()
                .Select(d => new
                {
                    maDatTour = d.MaDatTour,
                    maTour = d.MaTour,
                    tenTour = d.Tour != null ? d.Tour.TieuDe : string.Empty,
                    tenKhachHang = d.KhachHang != null ? d.KhachHang.HoTen : string.Empty,
                    soNguoiLon = d.SoNguoiLon ?? 0,
                    soTreEm = d.SoTreEm ?? 0,
                    tongTien = d.TongTien ?? 0,
                    ngayDat = d.NgayDat,
                    ngayBatDau = d.Tour != null ? d.Tour.ThoiGian : null,
                    trangThai = d.TrangThaiDat ?? "Chưa xác nhận",
                    maHoaDon = d.HoaDon != null ? d.HoaDon.MaHoaDon : (int?)null,
                    trangThaiHoaDon = d.HoaDon != null ? d.HoaDon.TrangThai : null
                })
                .OrderByDescending(x => x.ngayBatDau ?? x.ngayDat)
                .ToListAsync();

            return Ok(new { data });
        }

        [HttpPost("bookings/confirm")]
        public async Task<IActionResult> ConfirmBooking([FromForm] int bookingId)
        {
            var booking = await _db.DatTours
                .Include(d => d.Tour)
                .Include(d => d.KhachHang)
                .Include(d => d.HoaDon)
                .FirstOrDefaultAsync(d => d.MaDatTour == bookingId);

            if (booking == null)
                return NotFound(new { message = "Không tìm thấy booking" });

            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    HoaDon hoaDon;
                    string methodToInvoice = "Thanh toán tại văn phòng"; // FIX: Set payment method

                    // If invoice exists, use it. Otherwise, create a new one.
                    if (booking.HoaDon != null)
                    {
                        hoaDon = booking.HoaDon;
                    }
                    else
                    {
                        hoaDon = new HoaDon
                        {
                            MaDatTour = booking.MaDatTour
                        };
                        _db.HoaDons.Add(hoaDon);
                    }

                    // Update invoice details
                    hoaDon.SoTien = booking.TongTien;
                    hoaDon.NgayXuat = DateTime.Now;
                    hoaDon.TrangThai = "Đã thanh toán";
                    hoaDon.PhuongThucThanhToan = methodToInvoice;

                    // First save: Ensures the invoice exists in the DB and has an ID.
                    await _db.SaveChangesAsync();

                    // Create payload and sign now that hoaDon has a valid ID
                    string dataToSign = InvoiceSignatureHelper.CreatePayload(booking, hoaDon);
                    if (string.IsNullOrEmpty(dataToSign))
                    {
                        throw new Exception("Không thể tạo nội dung ký số (Payload rỗng).");
                    }

                    string signature = _rsaService.Sign(dataToSign);

                    // Update the invoice object with payload and signature
                    hoaDon.Payload = dataToSign;
                    hoaDon.ChuKySo = signature;

                    // Update booking status
                    booking.TrangThaiDat = "Đã xác nhận";
                    if (booking.HoaDon == null)
                    {
                        booking.HoaDon = hoaDon;
                    }

                    // Second save: Updates the invoice with signature/payload and the booking status.
                    await _db.SaveChangesAsync();
                    transaction.Commit();

                    return Ok(new
                    {
                        success = true,
                        message = "Đã xác nhận và ký số thành công!",
                        maHoaDon = hoaDon.MaHoaDon
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine("Error ConfirmBooking: " + ex.ToString());
                    return BadRequest(new { success = false, message = "Lỗi xử lý: " + ex.Message });
                }
            }
        }

        [HttpPost("bookings/cancel")]
        public async Task<IActionResult> CancelBooking([FromForm] int bookingId, [FromForm] string reason)
        {
            var booking = await _db.DatTours
                .Include(b => b.Tour) // Include tour to update its slots
                .Include(b => b.HoaDon)
                .FirstOrDefaultAsync(d => d.MaDatTour == bookingId);

            if (booking == null)
                return NotFound(new { message = "Không tìm thấy đặt tour." });

            if (booking.HoaDon != null && IsInvoicePaid(booking.HoaDon.TrangThai))
            {
                return BadRequest(new { message = "Không thể hủy đặt tour đã thanh toán." });
            }

            // Refund slots if the booking was taking up space
            if (booking.TrangThaiDat == "Chờ xác nhận" || booking.TrangThaiDat == "Đã xác nhận")
            {
                if (booking.Tour != null && booking.Tour.SoLuong.HasValue)
                {
                    var totalGuests = (booking.SoNguoiLon ?? 0) + (booking.SoTreEm ?? 0);
                    booking.Tour.SoLuong += totalGuests;
                }
            }

            booking.TrangThaiDat = "Đã hủy";

            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã hủy đặt tour." });
        }

        private static bool IsInvoicePaid(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return status.Equals("Đã thanh toán", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Hoàn tất", StringComparison.OrdinalIgnoreCase);
        }

        private string GetBadgeClassForInvoice(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                var s when s.Contains("đã thanh toán") || s.Contains("da thanh toan") => "success",
                var s when s.Contains("chờ") || s.Contains("cho") || s.Contains("chưa thanh toán") => "warning",
                var s when s.Contains("hủy") || s.Contains("huy") => "danger",
                var s when s.Contains("hoàn tất") || s.Contains("hoan tat") => "info",
                _ => "secondary"
            };
        }

        [HttpGet("invoices")]
        [HttpGet("/staff/api/invoices")]
        public async Task<IActionResult> GetInvoices()
        {
            // Lọc thêm theo chi nhánh từ claim (phòng trường hợp context chưa set)
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var branchClaim = User.FindFirst("MaChiNhanh")?.Value;

            var query = _db.HoaDons
                .AsNoTracking()
                .Include(h => h.DatTour)
                    .ThenInclude(d => d!.Tour)
                .Include(h => h.DatTour)
                    .ThenInclude(d => d!.KhachHang)
                .AsQueryable();

            if (role == "ROLE_STAFF" && int.TryParse(branchClaim, out var branchId))
            {
                query = query.Where(h => h.DatTour != null &&
                                         h.DatTour.Tour != null &&
                                         h.DatTour.Tour.MaChiNhanh == branchId);
            }

            var data = await query
                .OrderByDescending(h => h.NgayXuat)
                .Select(h => new
                {
                    maHoaDon = h.MaHoaDon,
                    maDatTour = h.MaDatTour,
                    tenTour = h.DatTour != null && h.DatTour.Tour != null ? h.DatTour.Tour.TieuDe : string.Empty,
                    tenKhachHang = h.DatTour != null && h.DatTour.KhachHang != null ? h.DatTour.KhachHang.HoTen : string.Empty,
                    soTien = h.SoTien ?? 0,
                    ngayXuat = h.NgayXuat,
                    trangThai = h.TrangThai ?? "Chưa thanh toán",
                    chuKySo = h.ChuKySo ?? string.Empty,
                    payload = InvoiceSignatureHelper.CreatePayload(h.DatTour, h)
                })
                .ToListAsync();

            var result = data.Select(item => new
            {
                item.maHoaDon,
                item.maDatTour,
                item.tenTour,
                item.tenKhachHang,
                item.soTien,
                item.ngayXuat,
                item.trangThai,
                statusClass = GetBadgeClassForInvoice(item.trangThai), // Add status class
                item.chuKySo,
                chuKySoHopLe = _rsaService.Verify(item.payload, item.chuKySo)
            });

            return Ok(new { data = result });
        }
    }
}
