using DuLich.Models;
using System.Globalization;

namespace DuLich.Services
{
    public static class InvoiceSignatureHelper
    {
        public static string CreatePayload(DatTour? booking, HoaDon? hoaDon)
        {
            // Kiểm tra dữ liệu đầu vào
            if (booking == null || hoaDon == null || !hoaDon.SoTien.HasValue || !hoaDon.NgayXuat.HasValue)
            {
                return string.Empty;
            }
            // 1. Format Số tiền: Giống PHP (bỏ số 0 thừa, không dấu phẩy)
            var formattedAmount = hoaDon.SoTien.Value.ToString("0.##", CultureInfo.InvariantCulture);
            // 2. Format Ngày: Phải có cả giờ phút giây để khớp với PHP "Y-m-d H:i:s"
            var formattedDate = hoaDon.NgayXuat.Value.ToString("yyyy-MM-dd HH:mm:ss");
            // 3. Tạo Payload: Dùng MaHoaDon thay vì MaDatTour để khớp logic PHP
            return $"MaHoaDon={hoaDon.MaHoaDon}|SoTien={formattedAmount}|NgayXuat={formattedDate}";
        }
    }
}