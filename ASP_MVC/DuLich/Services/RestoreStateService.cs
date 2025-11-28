using System;

namespace DuLich.Services
{
    /// <summary>
    /// Tracks whether a restore is currently running so the web app can short-circuit
    /// requests and avoid ORA-01109 errors while the database is offline.
    /// </summary>
    public class RestoreStateService
    {
        private readonly object _lock = new();

        public bool IsRestoring { get; private set; }
        public string? CurrentTarget { get; private set; }
        public DateTime? StartedAt { get; private set; }

        public void Start(string target)
        {
            lock (_lock)
            {
                IsRestoring = true;
                CurrentTarget = target;
                StartedAt = DateTime.UtcNow;
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                IsRestoring = false;
                CurrentTarget = null;
                StartedAt = null;
            }
        }
    }
}
