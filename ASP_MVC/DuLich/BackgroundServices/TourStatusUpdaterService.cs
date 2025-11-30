using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DuLich.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuLich.BackgroundServices
{
    public class TourStatusUpdaterService : IHostedService, IDisposable
    {
        private readonly ILogger<TourStatusUpdaterService> _logger;
        private Timer? _timer;
        private readonly IServiceProvider _services;

        public TourStatusUpdaterService(IServiceProvider services, ILogger<TourStatusUpdaterService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Tour Status Updater Service is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(24));

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("Tour Status Updater Service is running to update expired tours.");

            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                try
                {
                    var today = DateTime.Today;
                    var candidateTours = await context.Tours
                        .Where(t => t.ThoiGian.HasValue && t.TrangThai != "Đã hủy" && t.TrangThai != "Tạm ngưng" && t.TrangThai != "Ẩn")
                        .ToListAsync();

                    var updatedCount = 0;

                    foreach (var tour in candidateTours)
                    {
                        var startDate = tour.ThoiGian!.Value.Date;
                        var desiredStatus = tour.TrangThai;

                        if (startDate > today)
                        {
                            desiredStatus = "Hoạt động";
                        }
                        else if (startDate.AddDays(2) <= today)
                        {
                            desiredStatus = "Hoàn thành";
                        }
                        else
                        {
                            desiredStatus = "Đang diễn ra";
                        }

                        if (tour.TrangThai != desiredStatus)
                        {
                            tour.TrangThai = desiredStatus;
                            updatedCount++;
                        }
                    }

                    if (updatedCount > 0)
                    {
                        await context.SaveChangesAsync();
                        _logger.LogInformation("Updated {Count} tours status based on schedule.", updatedCount);
                    }
                    else
                    {
                        _logger.LogInformation("No tour statuses required updates.");
                    }

                    // Sync DatTour to completed when the tour has finished
                    var bookingsToComplete = await context.DatTours
                        .Include(d => d.Tour)
                        .Where(d => d.TrangThaiDat != "Đã hủy"
                                    && d.TrangThaiDat != "Hoàn thành"
                                    && d.Tour != null
                                    && d.Tour.TrangThai == "Hoàn thành")
                        .ToListAsync();

                    if (bookingsToComplete.Any())
                    {
                        foreach (var booking in bookingsToComplete)
                        {
                            booking.TrangThaiDat = "Hoàn thành";
                        }

                        context.DatTours.UpdateRange(bookingsToComplete);
                        await context.SaveChangesAsync();
                        _logger.LogInformation("Synced {Count} bookings to 'Hoàn thành' based on tour status.", bookingsToComplete.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while updating tour statuses.");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Tour Status Updater Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
