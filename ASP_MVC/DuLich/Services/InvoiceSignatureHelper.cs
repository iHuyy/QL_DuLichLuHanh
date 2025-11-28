using DuLich.Models;
using System.Globalization;

namespace DuLich.Services
{
    public static class InvoiceSignatureHelper
    {
        public static string CreatePayload(DatTour? booking, HoaDon? hoaDon)
        {
            // Kiểm tra dữ liệu
            if (hoaDon == null || !hoaDon.SoTien.HasValue || !hoaDon.NgayXuat.HasValue)
            {
                return string.Empty;
            }

            // 1. Format số tiền: "10000" (không dấu phẩy, lấy theo CultureInfo.InvariantCulture)
            var formattedAmount = hoaDon.SoTien.Value.ToString("0.##", CultureInfo.InvariantCulture);
            
            // 2. Format ngày: "yyyy-MM-dd HH:mm:ss" (Khớp với hiển thị trên PDF)
            var formattedDate = hoaDon.NgayXuat.Value.ToString("yyyy-MM-dd HH:mm:ss");

            // 3. Tạo chuỗi Payload: Dùng MaHoaDon làm định danh chính
            return $"MaHoaDon={hoaDon.MaHoaDon}|SoTien={formattedAmount}|NgayXuat={formattedDate}";
        }
    }
}
