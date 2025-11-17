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
        [HttpGet("/staff/api/bookings")]
        public async Task<IActionResult> GetBookings()
        {
            var data = await _db.DatTours
                .AsNoTracking()
                .Include(d => d.HoaDon)
                .Include(d => d.Tour)
                .Include(d => d.KhachHang)
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
        [HttpPost("/staff/api/bookings/confirm")]
        public async Task<IActionResult> ConfirmBooking([FromForm] int bookingId)
        {
            var booking = await _db.DatTours
                .Include(d => d.HoaDon)
                .Include(d => d.Tour)
                .Include(d => d.KhachHang)
                .FirstOrDefaultAsync(d => d.MaDatTour == bookingId);

            if (booking == null)
                return NotFound(new { message = "Không tìm thấy đặt tour." });

            booking.TrangThaiDat = "Đã xác nhận";
            booking.TrangThaiThanhToan = booking.TrangThaiThanhToan ?? "Chưa thanh toán";

            // Tạo hóa đơn nếu chưa có, đồng thời ký số
            if (booking.HoaDon == null)
            {
                booking.HoaDon = new HoaDon
                {
                    MaDatTour = booking.MaDatTour,
                    SoTien = booking.TongTien ?? 0,
                    NgayXuat = DateTime.Now,
                    TrangThai = "Chưa thanh toán"
                };
                var payload = InvoiceSignatureHelper.CreatePayload(booking, booking.HoaDon);
                booking.HoaDon.ChuKySo = _rsaService.Sign(payload);
                _db.HoaDons.Add(booking.HoaDon);
            }
            else
            {
                booking.HoaDon.NgayXuat = booking.HoaDon.NgayXuat ?? DateTime.Now;
                var payload = InvoiceSignatureHelper.CreatePayload(booking, booking.HoaDon);
                booking.HoaDon.ChuKySo = _rsaService.Sign(payload);
                _db.HoaDons.Update(booking.HoaDon);
            }

            _db.DatTours.Update(booking);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã xác nhận đặt tour và cập nhật hóa đơn." });
        }

        [HttpPost("bookings/cancel")]
        [HttpPost("/staff/api/bookings/cancel")]
        public async Task<IActionResult> CancelBooking([FromForm] int bookingId, [FromForm] string reason)
        {
            var booking = await _db.DatTours.FirstOrDefaultAsync(d => d.MaDatTour == bookingId);
            if (booking == null)
                return NotFound(new { message = "Không tìm thấy đặt tour." });

            booking.TrangThaiDat = "Đã hủy";
            booking.TrangThaiThanhToan = "Đã hủy";
            _db.DatTours.Update(booking);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã hủy đặt tour." });
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
                item.chuKySo,
                chuKySoHopLe = _rsaService.Verify(item.payload, item.chuKySo)
            });

            return Ok(new { data = result });
        }
    }
}
