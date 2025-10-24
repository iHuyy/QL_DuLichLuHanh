using Microsoft.EntityFrameworkCore;
using DuLich.Models;

namespace DuLich.Models.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Thêm DbSet cho các entity của bạn ở đây
        // Ví dụ: public DbSet<Tour> Tours { get; set; }
        public DbSet<KhachHang> KhachHangs { get; set; } = null!;
        public DbSet<Tour> Tours { get; set; } = null!;
        public DbSet<AnhTour> AnhTours { get; set; } = null!;
    }
}