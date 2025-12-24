using System;
using System.Collections.Generic;
using System.Linq;

namespace DuLich.Services
{
    public class BackupScheduleDefinition
    {
        public BackupScheduleDefinition(string displayName, string backupType, TimeSpan timeOfDay)
        {
            DisplayName = displayName;
            BackupType = backupType;
            TimeOfDay = timeOfDay;
        }

        public string DisplayName { get; }
        public string BackupType { get; }
        public TimeSpan TimeOfDay { get; }
    }

    public static class BackupScheduleProvider
    {
        private static readonly BackupScheduleDefinition[] Schedule =
        {
            new BackupScheduleDefinition("Sao lưu toàn bộ (Full)", "FULL", new TimeSpan(2, 0, 0)),
            new BackupScheduleDefinition("Sao lưu thay đổi (Incremental)", "INCREMENTAL", new TimeSpan(17, 0, 0))
        };

        public static IReadOnlyList<BackupScheduleDefinition> Definitions => Schedule;

        public static (BackupScheduleDefinition Definition, DateTime RunAt) GetNextEntry(DateTime reference)
        {
            var next = Schedule
                .Select(def => new
                {
                    Definition = def,
                    RunAt = GetNextRun(def.TimeOfDay, reference)
                })
                .OrderBy(x => x.RunAt)
                .First();

            return (next.Definition, next.RunAt);
        }

        public static DateTime GetNextRun(TimeSpan timeOfDay, DateTime reference)
        {
            var candidate = reference.Date.Add(timeOfDay);
            if (candidate <= reference)
            {
                candidate = candidate.AddDays(1);
            }
            return candidate;
        }
    }
}
