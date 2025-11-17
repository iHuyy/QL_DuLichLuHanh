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

        [HttpGet("{maDatTour}")]
        [Authorize(Roles = "ROLE_CUSTOMER")]
        public async Task<IActionResult> GetInvoice(int maDatTour)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return Unauthorized();
            }

            var hoaDon = await _dbContext.HoaDons
                .Include(h => h.DatTour)
                .FirstOrDefaultAsync(h => h.MaDatTour == maDatTour && h.DatTour != null && h.DatTour.MaKhachHang == customer.MaKhachHang);

            if (hoaDon == null)
            {
                return NotFound();
            }

            var payload = InvoiceSignatureHelper.CreatePayload(hoaDon.DatTour, hoaDon);
            var valid = _rsaService.Verify(payload, hoaDon.ChuKySo ?? string.Empty);

            return Ok(new
            {
                hoaDon = new
                {
                    hoaDon.MaHoaDon,
                    hoaDon.MaDatTour,
                    hoaDon.SoTien,
                    hoaDon.NgayXuat,
                    hoaDon.TrangThai,
                    hoaDon.ChuKySo
                },
                chuKySoHopLe = valid
            });
        }

        [HttpPost("{maDatTour}/thanh-toan")]
        [Authorize(Roles = "ROLE_CUSTOMER")]
        public async Task<IActionResult> Pay(int maDatTour)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return Unauthorized();
            }

            var booking = await _dbContext.DatTours
                .Include(d => d.HoaDon)
                .FirstOrDefaultAsync(d => d.MaDatTour == maDatTour && d.MaKhachHang == customer.MaKhachHang);

            if (booking == null || booking.HoaDon == null)
            {
                return NotFound();
            }

            booking.TrangThaiDat = "Đã thanh toán";
            booking.TrangThaiThanhToan = "Đã thanh toán";
            booking.HoaDon.TrangThai = "Hoàn tất";
            _dbContext.DatTours.Update(booking);
            _dbContext.HoaDons.Update(booking.HoaDon);

            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
