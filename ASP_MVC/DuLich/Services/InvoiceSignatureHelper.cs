using DuLich.Models;
using System.Globalization;

namespace DuLich.Services
{
    public static class InvoiceSignatureHelper
    {
        public static string CreatePayload(DatTour? booking, HoaDon? hoaDon)
        {
            if (booking == null || hoaDon == null || !hoaDon.SoTien.HasValue || !hoaDon.NgayXuat.HasValue)
            {
                return string.Empty;
            }

            var formattedAmount = hoaDon.SoTien.Value.ToString("0.##", CultureInfo.InvariantCulture);
            return $"MaDatTour={booking.MaDatTour}|SoTien={formattedAmount}|NgayXuat={hoaDon.NgayXuat.Value:yyyy-MM-dd}";
        }
    }
}
