using System;
using System.Threading;
using System.Threading.Tasks;
using DuLich.Models;
using DuLich.Models.Data;
using DuLich.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuLich.BackgroundServices
{
    /// <summary>
    /// Runs scheduled database backups (full at 02:00, incremental at 17:00).
    /// </summary>
    public class BackupSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<BackupSchedulerService> _logger;

        public BackupSchedulerService(IServiceProvider services, ILogger<BackupSchedulerService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Backup Scheduler Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var (definition, runAt) = BackupScheduleProvider.GetNextEntry(now);
                var delay = runAt - now;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next {Type} backup scheduled at {RunAt} (in {Delay}).", definition.BackupType, runAt, delay);
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await ExecuteBackupAsync(definition, stoppingToken);
            }

            _logger.LogInformation("Backup Scheduler Service stopped.");
        }

        private async Task ExecuteBackupAsync(BackupScheduleDefinition definition, CancellationToken stoppingToken)
        {
            using var scope = _services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var backupService = scope.ServiceProvider.GetRequiredService<BackupSshService>();

            var actionType = definition.DisplayName;
            var scheduleType = definition.BackupType;

            var history = await CreateHistoryRecordAsync(dbContext, actionType, stoppingToken);

            try
            {
                _logger.LogInformation("Starting scheduled {Type} backup.", actionType);
                var exec = backupService.RunBackup(scheduleType);

                history.Target = exec.BackupPath;
                history.CompletedAt = DateTime.Now;
                history.Status = exec.ExitStatus == 0 ? "Hoan tat" : "That bai";
                history.Notes = TruncateNote($"AUTO [{scheduleType}]: {exec.Output}");

                _logger.LogInformation("Scheduled {Type} backup finished with status {Status}.", actionType, history.Status);
            }
            catch (Exception ex)
            {
                history.CompletedAt = DateTime.Now;
                history.Status = "That bai";
                history.Notes = TruncateNote($"AUTO [{scheduleType}] ERROR: {ex}");
                _logger.LogError(ex, "Scheduled {Type} backup failed.", actionType);
            }

            dbContext.BackupHistories.Update(history);
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        private static async Task<BackupHistory> CreateHistoryRecordAsync(ApplicationDbContext dbContext, string actionType, CancellationToken token)
        {
            var nextId = (await dbContext.BackupHistories.MaxAsync(b => (int?)b.Id, token) ?? 0) + 1;
            var history = new BackupHistory
            {
                Id = nextId,
                ActionType = actionType,
                RequestedAt = DateTime.Now,
                Status = "Dang chay",
                Target = null,
                Notes = "Tu dong chay theo lich",
                RequestedBy = "system-scheduler"
            };

            dbContext.BackupHistories.Add(history);
            await dbContext.SaveChangesAsync(token);
            return history;
        }

        private static string TruncateNote(string? text, int maxLength = 490)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            return text.Length <= maxLength ? text : text[..maxLength];
        }
    }
}
