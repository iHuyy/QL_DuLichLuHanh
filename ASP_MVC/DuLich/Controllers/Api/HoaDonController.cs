using DuLich.Models;
using DuLich.Models.Data;
using DuLich.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // API lấy chi tiết hóa đơn
        // URL: GET api/hoadon/123
        [HttpGet("{maDatTour}")]
        [Authorize(Policy = "MobileUser")] // <-- QUAN TRỌNG: Dùng Policy MobileUser để nhận JWT
        public async Task<IActionResult> GetInvoice(int maDatTour)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null) return Unauthorized();

            var hoaDon = await _dbContext.HoaDons
                .Include(h => h.DatTour)
                .FirstOrDefaultAsync(h => h.MaDatTour == maDatTour && h.DatTour != null && h.DatTour.MaKhachHang == customer.MaKhachHang);

            if (hoaDon == null) return NotFound(new { message = "Không tìm thấy hóa đơn" });

            // Kiểm tra chữ ký số
            var payload = InvoiceSignatureHelper.CreatePayload(hoaDon.DatTour, hoaDon);
            var valid = _rsaService.Verify(payload, hoaDon.ChuKySo ?? string.Empty);

            return Ok(new
            {
                success = true, // Thêm cờ success để Flutter dễ check
                invoice = new
                {
                    MAHOADON = hoaDon.MaHoaDon, // Trả về key chữ hoa để khớp với map trong Flutter
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
        [Authorize(Policy = "MobileUser")] // <-- QUAN TRỌNG: Dùng Policy MobileUser
        public async Task<IActionResult> Pay(int maDatTour)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null) return Unauthorized();

            var booking = await _dbContext.DatTours
                .Include(d => d.HoaDon)
                .FirstOrDefaultAsync(d => d.MaDatTour == maDatTour && d.MaKhachHang == customer.MaKhachHang);

            if (booking == null || booking.HoaDon == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy đơn đặt tour hoặc hóa đơn" });
            }

            if (booking.HoaDon.TrangThai == "Đã thanh toán" || booking.HoaDon.TrangThai == "Hoàn tất")
            {
                return Ok(new { success = true, message = "Hóa đơn đã được thanh toán trước đó" });
            }

            // Cập nhật trạng thái
            booking.TrangThaiDat = "Đã thanh toán";
            booking.TrangThaiThanhToan = "Đã thanh toán";
            booking.HoaDon.TrangThai = "Đã thanh toán"; // Đồng bộ trạng thái

            // Ký lại hóa đơn (để xác nhận trạng thái mới nếu cần) hoặc giữ nguyên chữ ký cũ
            // Ở đây ta cập nhật DB
            _dbContext.DatTours.Update(booking);
            _dbContext.HoaDons.Update(booking.HoaDon);

            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Thanh toán thành công" });
        }
        // Thêm action này vào HoaDonController class

        // GET: api/hoadon/html/{maDatTour}
        // Lưu ý: Flutter đang gọi 'api/invoice/html/...', bạn nên sửa Flutter thành 'api/hoadon/html/...' 
        // hoặc đổi Route của controller này, hoặc thêm Route phụ như bên dưới.
        // GET: api/hoadon/html/{maDatTour}
        [HttpGet("html/{maDatTour}")]
        [Authorize(Policy = "MobileUser")]
        public async Task<IActionResult> GetInvoiceHtml(int maDatTour)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null) return Unauthorized();

            var booking = await _dbContext.DatTours
                .Include(d => d.Tour)
                .Include(d => d.HoaDon)
                .FirstOrDefaultAsync(d => d.MaDatTour == maDatTour && d.MaKhachHang == customer.MaKhachHang);

            // Kiểm tra null cho HoaDon
            if (booking == null || booking.HoaDon == null || booking.Tour == null)
            {
                return NotFound("Không tìm thấy dữ liệu hóa đơn");
            }

            var signerName = "Hệ thống DuLich";

            // SỬA LỖI: Ép kiểu hoặc dùng ! để khẳng định không null (vì đã check ở trên)
            var htmlContent = GenerateInvoiceHtml(booking.HoaDon!, booking, booking.Tour!, customer, signerName);

            return Content(htmlContent, "text/html");
        }

        // Hàm Helper sinh HTML (Copy logic từ CustomerController sang đây)
        private string GenerateInvoiceHtml(HoaDon hoaDon, DatTour booking, Tour tour, KhachHang customer, string signerName)
        {
            // Tính toán hash để hiển thị (giống logic Verify)
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

            // Trả về chuỗi HTML
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
            <p>Chữ ký số hệ thống:<br/>{hoaDon.ChuKySo}</p>
            <p>Mã kiểm tra (Auth Code): <b>{authCode}</b></p>
            <p><i>Hóa đơn này được ký số bảo mật bởi hệ thống DuLich. Mọi chỉnh sửa sẽ làm mất hiệu lực của chữ ký.</i></p>
        </div>

        <div class='footer'>
            Cảm ơn quý khách đã sử dụng dịch vụ của chúng tôi!
        </div>
    </body>
    </html>";
        }
    }
}