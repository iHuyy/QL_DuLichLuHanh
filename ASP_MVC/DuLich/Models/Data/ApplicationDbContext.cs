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
        public DbSet<NhanVien> NhanViens { get; set; } = null!;
        public DbSet<DatTour> DatTours { get; set; } = null!;
        public DbSet<HoaDon> HoaDons { get; set; } = null!;
        public DbSet<DanhGiaTour> DanhGiaTours { get; set; } = null!;
        public DbSet<NhatKyHeThong> NhatKyHeThongs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Explicitly set precision for decimal columns to avoid provider warnings
            // Matches the database DDL which used NUMBER(12,2)
            modelBuilder.Entity<Tour>(entity =>
            {
                entity.Property(e => e.GiaNguoiLon).HasPrecision(12, 2);
                entity.Property(e => e.GiaTreEm).HasPrecision(12, 2);
            });

            modelBuilder.Entity<HoaDon>(entity =>
            {
                entity.Property(e => e.SoTien).HasPrecision(12, 2);
            });

            modelBuilder.Entity<DatTour>(entity =>
            {
                entity.Property(e => e.TongTien).HasPrecision(12, 2);
            });

            // Sửa thêm: map quan hệ ANHTOUR <-> TOUR, dùng MATOUR làm FK (tránh shadow FK TourMaTour)
            modelBuilder.Entity<AnhTour>(eb =>
            {
                eb.ToTable("ANHTOUR", schema: "TADMIN");
                eb.HasKey(a => a.MaAnh);
                eb.Property(a => a.MaAnh).HasColumnName("MAANH");
                eb.Property(a => a.MaTour).HasColumnName("MATOUR");
                eb.Property(a => a.DuongDanAnh).HasColumnName("DUONGDANANH");
                eb.Property(a => a.MoTa).HasColumnName("MOTA");
                eb.Property(a => a.NgayTaiLen).HasColumnName("NGAYTAILEN");

                eb.HasOne(a => a.Tour)
                  .WithMany(t => t.AnhTours)
                  .HasForeignKey(a => a.MaTour)
                  .HasConstraintName("FK_ANHTOUR_TOUR");
            });
        }
    }
}