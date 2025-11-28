using DuLich.Models;
using DuLich.Models.Data;
using DuLich.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;

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

            return Content(htmlContent, "text/html; charset=utf-8", System.Text.Encoding.UTF8);
        }

        // Hàm Helper sinh HTML (Copy logic từ CustomerController sang đây)
        private string GenerateInvoiceHtml(HoaDon hoaDon, DatTour booking, Tour tour, KhachHang customer, string signerName)
        {
            var total = hoaDon.SoTien ?? 0m;
            var ngayXuat = hoaDon.NgayXuat?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            var paymentMethod = (!string.IsNullOrEmpty(hoaDon.TrangThai) && (hoaDon.TrangThai.Contains("Thanh toán") || hoaDon.TrangThai.Contains("Paid")))
                                ? "Chuyển khoản / Online" : "Chưa thanh toán";

            // Tính toán Subtotal
            decimal priceAdult = tour.GiaNguoiLon ?? 0;
            decimal subAdult = (booking.SoNguoiLon ?? 0) * priceAdult;

            decimal priceChild = tour.GiaTreEm ?? 0;
            decimal subChild = (booking.SoTreEm ?? 0) * priceChild;

            // Tạo HTML
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <style>
                    /* Cập nhật font-family để hỗ trợ tốt nhất trên Mobile */
                    body {{ 
                        font-family: 'Roboto', 'Helvetica Neue', Helvetica, Arial, sans-serif; 
                        margin: 20px; 
                        font-size: 14px; 
                        line-height: 1.4; 
                        color: #000; 
                    }}
            .header {{ text-align: center; margin-bottom: 10px; }}
            .header h2 {{ text-transform: uppercase; margin: 0; font-size: 22px; font-weight: bold; }}
            .divider {{ border-bottom: 2px solid #000; margin-bottom: 20px; }}
            
            /* Bảng thông tin 2 cột */
            .info-grid {{ display: flex; justify-content: space-between; margin-bottom: 5px; }}
            .info-col {{ width: 48%; }}
            .info-row {{ margin-bottom: 5px; }}
            .label {{ font-weight: bold; }}

            /* Bảng sản phẩm */
            table.items {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
            table.items th, table.items td {{ border: 1px solid #ccc; padding: 8px; text-align: left; }}
            table.items th {{ background-color: #f0f0f0; font-weight: bold; text-align: center; }}
            .text-right {{ text-align: right !important; }}
            .text-center {{ text-align: center !important; }}
            
            /* Tổng tiền */
            .total-row td {{ border: none; border-top: 2px solid #000; font-weight: bold; padding-top: 10px; font-size: 16px; }}
            
            /* Footer */
            .footer {{ margin-top: 40px; text-align: right; }}
            .signer-title {{ font-weight: bold; margin-bottom: 50px; }}
            .signer-name {{ font-weight: bold; text-transform: uppercase; }}
            .timestamp {{ font-size: 12px; color: #555; }}
        </style>
    </head>
    <body>
        <div class='header'>
            <h2>HÓA ĐƠN / INVOICE</h2>
        </div>
        <div class='divider'></div>

        <div class='info-grid'>
            <div class='info-col'>
                <div class='info-row'><span class='label'>Mã đơn hàng / Order ID:</span> {hoaDon.MaHoaDon}</div>
                <div class='info-row'><span class='label'>Khách hàng / Customer:</span> {customer.HoTen}</div>
            </div>
            <div class='info-col'>
                <div class='info-row'><span class='label'>Ngày / Date:</span> {ngayXuat}</div>
                <div class='info-row'><span class='label'>Thanh toán / Payment:</span> {paymentMethod}</div>
            </div>
        </div>
        
        <div style='margin-bottom: 20px;'>
            <span class='label'>Địa chỉ / Address:</span> {customer.DiaChi} - <span class='label'>SĐT:</span> {customer.SoDienThoai}
        </div>

        <table class='items'>
            <thead>
                <tr>
                    <th style='width: 45%'>Sản phẩm / Product</th>
                    <th style='width: 10%'>SL / Qty</th>
                    <th style='width: 20%'>Đơn giá / Price</th>
                    <th style='width: 25%'>Thành tiền / Subtotal</th>
                </tr>
            </thead>
            <tbody>
                {(booking.SoNguoiLon > 0 ? $@"
                <tr>
                    <td>Vé người lớn - {tour.TieuDe}</td>
                    <td class='text-center'>{booking.SoNguoiLon}</td>
                    <td class='text-right'>{priceAdult:N0}</td>
                    <td class='text-right'>{subAdult:N0}</td>
                </tr>" : "")}
                
                {(booking.SoTreEm > 0 ? $@"
                <tr>
                    <td>Vé trẻ em - {tour.TieuDe}</td>
                    <td class='text-center'>{booking.SoTreEm}</td>
                    <td class='text-right'>{priceChild:N0}</td>
                    <td class='text-right'>{subChild:N0}</td>
                </tr>" : "")}

                <tr class='total-row'>
                    <td colspan='3' class='text-right'>Tổng cộng / Total</td>
                    <td class='text-right'>{total:N0} VNĐ</td>
                </tr>
            </tbody>
        </table>

        <div class='footer'>
            <div class='signer-title'>Người ký / Signed by:</div>
            <div class='signer-name'>{signerName}</div>
            <div class='timestamp'>{DateTime.Now:yyyy-MM-dd HH:mm:ss K}</div>
        </div>
    </body>
    </html>";
        }

        [HttpPost("verify")]
        [Authorize(Policy = "MobileUser")] // Yêu cầu Token từ Mobile
        public async Task<IActionResult> VerifyInvoice(IFormFile? invoiceFile)
        {
            try
            {
                if (invoiceFile == null || invoiceFile.Length == 0)
                {
                    return Ok(new { success = false, message = "Vui lòng chọn file PDF." });
                }

                // 1. Đọc nội dung text từ file PDF
                string pdfText = string.Empty;
                try 
                {
                    using (var reader = new PdfReader(invoiceFile.OpenReadStream()))
                    using (var pdfDoc = new PdfDocument(reader))
                    {
                        var strategy = new LocationTextExtractionStrategy();
                        pdfText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(1), strategy);
                    }
                }
                catch
                {
                    return Ok(new { success = false, message = "File lỗi hoặc không đọc được nội dung PDF." });
                }

                // 2. Trích xuất thông tin từ text PDF bằng Regex
                var matchId = Regex.Match(pdfText, @"Order ID:\s*(\d+)");
                var matchDate = Regex.Match(pdfText, @"Date:\s*(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})"); 
                var matchTotal = Regex.Match(pdfText, @"Total\s*([\d,.]+)\s*VNĐ");

                if (!matchId.Success)
                {
                    return Ok(new { success = false, message = "Không tìm thấy Mã hóa đơn trên file PDF." });
                }

                string pdfMaHoaDon = matchId.Groups[1].Value;
                // Nếu không bắt được ngày hoặc tiền thì gán mặc định để tránh lỗi null, nhưng verify sẽ fail
                string pdfNgayXuat = matchDate.Success ? matchDate.Groups[1].Value : "";
                string pdfSoTienRaw = matchTotal.Success ? matchTotal.Groups[1].Value.Replace(",", "").Replace(".", "") : "0";

                // Format tiền về dạng chuẩn (ví dụ "35000")
                if (decimal.TryParse(pdfSoTienRaw, out decimal parsedMoney))
                {
                    pdfSoTienRaw = parsedMoney.ToString("0.##", CultureInfo.InvariantCulture);
                }

                // 3. Tái tạo Payload
                string reconstructedPayload = $"MaHoaDon={pdfMaHoaDon}|SoTien={pdfSoTienRaw}|NgayXuat={pdfNgayXuat}";

                // 4. Lấy Chữ ký số từ Database
                if (!int.TryParse(pdfMaHoaDon, out int id)) 
                     return Ok(new { success = false, message = "Mã hóa đơn không hợp lệ." });

                var hoaDonDB = await _dbContext.HoaDons.AsNoTracking().FirstOrDefaultAsync(h => h.MaHoaDon == id);

                if (hoaDonDB == null)
                {
                    return Ok(new { success = false, message = $"Hóa đơn #{id} không tồn tại trên hệ thống." });
                }
                
                if (string.IsNullOrEmpty(hoaDonDB.ChuKySo))
                {
                    // Trả về data để hiển thị dù lỗi
                    return Ok(new { 
                        success = true, 
                        isValid = false, 
                        data = new { maHoaDon = pdfMaHoaDon, ngayXuat = pdfNgayXuat, trangThai = hoaDonDB.TrangThai },
                        message = "Hóa đơn gốc trên hệ thống chưa được ký số." 
                    });
                }

                // 5. Xác thực: Hash(Payload tái tạo) vs Decrypt(Chữ ký DB)
                bool isValid = _rsaService.Verify(reconstructedPayload, hoaDonDB.ChuKySo);

                if (isValid)
                {
                    return Ok(new
                    {
                        success = true,
                        isValid = true,
                        data = new { maHoaDon = pdfMaHoaDon, ngayXuat = pdfNgayXuat, trangThai = hoaDonDB.TrangThai },
                        message = "Hóa đơn HỢP LỆ. Thông tin trên file PDF khớp hoàn toàn với hệ thống."
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = true,
                        isValid = false,
                        data = new { maHoaDon = pdfMaHoaDon, ngayXuat = pdfNgayXuat, trangThai = hoaDonDB.TrangThai },
                        message = "CẢNH BÁO: Nội dung file PDF không khớp với chữ ký số! Có thể số tiền hoặc ngày tháng đã bị sửa đổi."
                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}