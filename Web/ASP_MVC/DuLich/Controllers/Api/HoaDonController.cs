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

        public HoaDonController(ApplicationDbContext dbContext, RSAService rsaService)
        {
            _dbContext = dbContext;
            _rsaService = rsaService;
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

        private string GenerateInvoiceHtml(HoaDon hoaDon, DatTour booking, Tour tour, KhachHang customer, string signerName)
        {
            var signatureData = InvoiceSignatureHelper.CreatePayload(booking, hoaDon);
            string hashHex = "";
            string authCode = "";

            try
            {
                // Tạo Auth Code ngắn từ Hash của dữ liệu (để đối chiếu nhanh)
                byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signatureData));
                hashHex = BitConverter.ToString(hashBytes).Replace("-", "");
                authCode = hashHex.Length >= 12 ? hashHex.Substring(0, 12) : hashHex;
            }
            catch { }

            var total = hoaDon.SoTien ?? 0m;
            var ngayXuat = hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A";
            var ngayDi = tour.ThoiGian?.ToString("dd/MM/yyyy") ?? "N/A";

            // [ĐÃ SỬA] Ẩn chữ ký số dài dòng, chỉ hiện trạng thái xác thực
            return $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='UTF-8'>
        <style>
            body {{ font-family: DejaVu Sans, Arial, sans-serif; margin: 20px; font-size: 14px; line-height: 1.5; }}
            .header {{ text-align: center; margin-bottom: 30px; border-bottom: 2px solid #007AFF; padding-bottom: 10px; }}
            .header h1 {{ color: #007AFF; margin: 0; }}
            .section-title {{ color: #007AFF; font-weight: bold; margin-top: 20px; border-bottom: 1px solid #ddd; }}
            table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
            td {{ padding: 8px; vertical-align: top; }}
            .label {{ font-weight: bold; color: #555; width: 140px; }}
            .total-box {{ text-align: right; margin-top: 20px; font-size: 18px; font-weight: bold; color: #d32f2f; }}
            .signature-box {{ margin-top: 40px; background: #f9f9f9; padding: 15px; border-radius: 8px; font-size: 11px; word-break: break-all; }}
            .footer {{ margin-top: 50px; text-align: center; font-size: 12px; color: #888; }}
        </style>
    </head>
    <body>
        <div class='header'>
            <h1>HÓA ĐƠN ĐIỆN TỬ</h1>
            <p>Mã hóa đơn: #{hoaDon.MaHoaDon} | Ngày xuất: {ngayXuat}</p>
        </div>

        <div class='section-title'>THÔNG TIN KHÁCH HÀNG</div>
        <table>
            <tr><td class='label'>Họ tên:</td><td>{customer.HoTen}</td></tr>
            <tr><td class='label'>Email:</td><td>{customer.Email}</td></tr>
            <tr><td class='label'>Số điện thoại:</td><td>{customer.SoDienThoai}</td></tr>
            <tr><td class='label'>Địa chỉ:</td><td>{customer.DiaChi}</td></tr>
        </table>

        <div class='section-title'>CHI TIẾT DỊCH VỤ</div>
        <table>
            <tr><td class='label'>Tên Tour:</td><td><strong>{tour.TieuDe}</strong></td></tr>
            <tr><td class='label'>Khởi hành:</td><td>{ngayDi} tại {tour.NoiKhoiHanh}</td></tr>
            <tr><td class='label'>Số lượng:</td><td>{booking.SoNguoiLon} Người lớn, {booking.SoTreEm} Trẻ em</td></tr>
            <tr><td class='label'>Trạng thái:</td><td>{hoaDon.TrangThai}</td></tr>
        </table>

        <div class='total-box'>
            TỔNG THANH TOÁN: {total:N0} VNĐ
        </div>

        <div class='signature-box'>
            <strong>THÔNG TIN XÁC THỰC (DIGITAL SIGNATURE)</strong><br/>
            <p>Trạng thái: <b style='color:green'>Đã ký số bảo mật</b></p>
            <p>Mã kiểm tra (Auth Code): <b>{authCode}</b></p>
            <p><i>Hóa đơn này được bảo vệ bởi chữ ký số hệ thống DuLich. Bất kỳ thay đổi nào về nội dung sẽ khiến hóa đơn trở nên không hợp lệ khi tra cứu.</i></p>
        </div>

        <div class='footer'>
            Cảm ơn quý khách đã sử dụng dịch vụ của chúng tôi!
        </div>
    </body>
    </html>";
        }
    }
}