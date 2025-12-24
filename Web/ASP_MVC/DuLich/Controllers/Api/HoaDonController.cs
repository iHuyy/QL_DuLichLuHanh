using DuLich.Models;
using DuLich.Models.Data;
using DuLich.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DuLich.Controllers.Api
{
    [ApiController]
    [Route("api/hoadon")]
    public class HoaDonController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly RSAService _rsaService;
        private readonly EmailService _emailService;

        public HoaDonController(ApplicationDbContext dbContext, RSAService rsaService, EmailService emailService)
        {
            _dbContext = dbContext;
            _rsaService = rsaService;
            _emailService = emailService;
        }

        [HttpPost("verify")]
        [Authorize(Policy = "MobileUser")]
        public async Task<IActionResult> Verify([FromForm] IFormFile? invoiceFile)
        {
            if (invoiceFile == null || invoiceFile.Length == 0)
            {
                return Ok(new { success = false, message = "Vui lòng chọn file PDF hóa đơn." });
            }

            var fileName = invoiceFile.FileName;
            var match = Regex.Match(fileName, @"(\d+)");
            if (!match.Success)
            {
                return Ok(new { success = false, message = "Tên file không hợp lệ. Phải chứa mã hóa đơn (VD: HoaDon_123.pdf)." });
            }

            if (!int.TryParse(match.Value, out int maHoaDon))
            {
                return Ok(new { success = false, message = "Mã hóa đơn không hợp lệ." });
            }

            var hoaDon = await _dbContext.HoaDons
                .Include(h => h.DatTour)
                .FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);

            if (hoaDon == null)
            {
                return Ok(new { success = false, message = $"Không tìm thấy hóa đơn #{maHoaDon} trên hệ thống." });
            }

            if (string.IsNullOrEmpty(hoaDon.ChuKySo))
            {
                return Ok(new { success = false, isValid = false, message = "Hóa đơn này chưa được ký số." });
            }

            // [LOGIC XÁC THỰC CHUẨN]
            // Tái tạo payload từ dữ liệu hiện tại trong DB để đối chiếu với chữ ký đã lưu
            var currentDataPayload = InvoiceSignatureHelper.CreatePayload(hoaDon.DatTour, hoaDon);
            bool isValid = _rsaService.Verify(currentDataPayload, hoaDon.ChuKySo);

            if (isValid)
            {
                return Ok(new
                {
                    success = true,
                    isValid = true,
                    message = "Hóa đơn HỢP LỆ.\nDữ liệu khớp hoàn toàn với chữ ký bảo mật.",
                    data = new
                    {
                        maHoaDon = hoaDon.MaHoaDon,
                        ngayXuat = hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm"),
                        trangThai = hoaDon.TrangThai
                    }
                });
            }
            else
            {
                return Ok(new
                {
                    success = true,
                    isValid = false,
                    message = "CẢNH BÁO: Dữ liệu KHÔNG HỢP LỆ!\nThông tin (số tiền, ngày lập...) đã bị thay đổi so với chữ ký gốc.",
                    data = new
                    {
                        maHoaDon = hoaDon.MaHoaDon,
                        ngayXuat = hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm"),
                        trangThai = hoaDon.TrangThai
                    }
                });
            }
        }

        [HttpGet("{maDatTour}")]
        [Authorize(Policy = "MobileUser")]
        public async Task<IActionResult> GetInvoice(int maDatTour)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null) return Unauthorized();

            var hoaDon = await _dbContext.HoaDons
                .Include(h => h.DatTour)
                .FirstOrDefaultAsync(h => h.MaDatTour == maDatTour && h.DatTour != null && h.DatTour.MaKhachHang == customer.MaKhachHang);

            if (hoaDon == null) return NotFound(new { message = "Không tìm thấy hóa đơn" });

            var payload = InvoiceSignatureHelper.CreatePayload(hoaDon.DatTour, hoaDon);
            var valid = _rsaService.Verify(payload, hoaDon.ChuKySo ?? string.Empty);

            return Ok(new
            {
                success = true,
                invoice = new
                {
                    MAHOADON = hoaDon.MaHoaDon,
                    MADATTOUR = hoaDon.MaDatTour,
                    SOTIEN = hoaDon.SoTien,
                    NGAYXUAT = hoaDon.NgayXuat?.ToString("yyyy-MM-dd HH:mm:ss"),
                    TRANGTHAI = hoaDon.TrangThai,
                    CHUKYSO = hoaDon.ChuKySo
                },
                chuKySoHopLe = valid
            });
        }

        [HttpPost("{maDatTour}/thanh-toan")]
        [Authorize(Policy = "MobileUser")]
        public async Task<IActionResult> Pay(int maDatTour)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null) return Unauthorized();

            var booking = await _dbContext.DatTours
                .Include(d => d.HoaDon)
                .FirstOrDefaultAsync(d => d.MaDatTour == maDatTour && d.MaKhachHang == customer.MaKhachHang);

            if (booking == null || booking.HoaDon == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy đơn đặt tour hoặc hóa đơn" });
            }
            if (string.Equals(booking.TrangThaiDat, "Đã hủy", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Đơn đặt đã hủy không thể thanh toán." });
            }

            if (booking.HoaDon.TrangThai == "Đã thanh toán" || booking.HoaDon.TrangThai == "Hoàn tất")
            {
                return Ok(new { success = true, message = "Hóa đơn đã được thanh toán trước đó" });
            }

            if (booking.TrangThaiDat != "Đã xác nhận" && booking.TrangThaiDat != "Hoàn thành" && booking.TrangThaiDat != "Đã hủy")
            {
                booking.TrangThaiDat = "Chờ xác nhận";
            }
            booking.HoaDon.TrangThai = "Đã thanh toán";

            _dbContext.DatTours.Update(booking);
            _dbContext.HoaDons.Update(booking.HoaDon);

            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Thanh toán thành công" });
        }

        [HttpGet("html/{maDatTour}")]
        [Authorize(Policy = "MobileUser")]
        public async Task<IActionResult> GetInvoiceHtml(int maDatTour)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null) return Unauthorized();

            var booking = await _dbContext.DatTours
                .Include(d => d.Tour)
                .Include(d => d.HoaDon)
                .FirstOrDefaultAsync(d => d.MaDatTour == maDatTour && d.MaKhachHang == customer.MaKhachHang);

            if (booking == null || booking.HoaDon == null || booking.Tour == null)
            {
                return NotFound("Không tìm thấy dữ liệu hóa đơn");
            }

            var signerName = "Hệ thống DuLich";
            var htmlContent = GenerateInvoiceHtml(booking.HoaDon!, booking, booking.Tour!, customer, signerName);

            return Content(htmlContent, "text/html; charset=utf-8", System.Text.Encoding.UTF8);
        }

        [HttpPost("send-invoice/{hoaDonId}")]
        [Authorize(Roles = "ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> SendInvoiceEmail(int hoaDonId)
        {
            var hoaDon = await _dbContext.HoaDons
                .Include(h => h.DatTour.Tour)
                .Include(h => h.DatTour.KhachHang)
                .FirstOrDefaultAsync(h => h.MaHoaDon == hoaDonId);

            if (hoaDon?.DatTour?.KhachHang == null || hoaDon.DatTour.Tour == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy dữ liệu đầy đủ cho hóa đơn." });
            }

            var customer = hoaDon.DatTour.KhachHang;
            if (string.IsNullOrEmpty(customer.Email))
            {
                return BadRequest(new { success = false, message = "Khách hàng không có địa chỉ email." });
            }

            try
            {
                var htmlBody = GenerateInvoiceHtml(hoaDon, hoaDon.DatTour, hoaDon.DatTour.Tour, customer, "Hệ thống DuLich");
                var subject = $"Hóa đơn điện tử cho tour: {hoaDon.DatTour.Tour.TieuDe}";

                await _emailService.SendEmailAsync(customer.Email, subject, htmlBody);

                return Ok(new { success = true, message = $"Đã gửi hóa đơn đến email: {customer.Email}" });
            }
            catch (Exception ex)
            {
                // Log the exception properly in a real application
                Console.WriteLine("EMAIL SENDING FAILED: " + ex.ToString());
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ khi gửi email. Vui lòng kiểm tra lại cấu hình SMTP và thử lại." });
            }
        }

        private string GenerateInvoiceHtml(HoaDon hoaDon, DatTour booking, Tour tour, KhachHang customer, string signerName)
        {
            var signatureData = InvoiceSignatureHelper.CreatePayload(booking, hoaDon);
            string hashHex = "";
            string authCode = "";

            try
            {
                byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signatureData));
                hashHex = BitConverter.ToString(hashBytes).Replace("-", "");
                authCode = hashHex.Length >= 12 ? hashHex.Substring(0, 12) : hashHex;
            }
            catch { }

            var total = hoaDon.SoTien ?? 0m;
            var ngayXuat = hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A";
            var ngayDi = tour.ThoiGian?.ToString("dd/MM/yyyy") ?? "N/A";

            return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""utf-8"" />
    <title>Hóa đơn điện tử</title>
    <style>
        body {{ font-family: Arial, Helvetica, sans-serif; color: #333; background: #f7f7f7; padding: 20px; }}
        .invoice {{ max-width: 800px; margin: 0 auto; background: #fff; padding: 20px; border-radius: 6px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }}
        h2 {{ margin: 0 0 8px; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 12px; }}
        th, td {{ padding: 8px; border: 1px solid #e6e6e6; text-align: left; }}
    </style>
</head>
<body>
    <div class=""invoice"">
        <div style=""text-align:center"">
            <h2>HÓA ĐƠN ĐIỆN TỬ</h2>
            <div>Ngày xuất: {ngayXuat}</div>
        </div>

        <section style=""margin-top:14px"">
            <strong>Khách hàng:</strong> {customer.HoTen} {(!string.IsNullOrEmpty(customer.Email) ? $"- {customer.Email}" : string.Empty)}<br />
            <strong>Tour:</strong> {tour.TieuDe} <span style=""margin-left:8px""><strong>Ngày đi:</strong> {ngayDi}</span>
        </section>

        <table>
            <tr><th>Mã hóa đơn</th><td>{hoaDon.MaHoaDon}</td></tr>
            <tr><th>Mã đặt tour</th><td>{booking.MaDatTour}</td></tr>
            <tr><th>Tổng tiền</th><td>{total:C}</td></tr>
            <tr><th>Trạng thái</th><td>{hoaDon.TrangThai}</td></tr>
            <tr><th>Mã xác thực</th><td>{authCode}</td></tr>
        </table>

        <div style=""margin-top:18px;"">Người ký: {signerName}</div>
    </div>
</body>
</html>";
        }

    }

}