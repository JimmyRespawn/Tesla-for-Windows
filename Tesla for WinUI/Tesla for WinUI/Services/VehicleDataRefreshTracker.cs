using System;
using System.Collections.Generic;

namespace TeslaMurphy.Services
{
    internal static class VehicleDataRefreshTracker
    {
        private static readonly object sync = new object();
        private static readonly Dictionary<string, long> changedVehicles = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static long revision;

        public static void MarkChanged(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin)) return;
            lock (sync) changedVehicles[vin] = ++revision;
        }

        public static bool RequiresRefresh(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin)) return false;
            lock (sync) return changedVehicles.ContainsKey(vin);
        }

        public static long GetPendingRevision(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin)) return 0;
            lock (sync) return changedVehicles.TryGetValue(vin, out long pending) ? pending : 0;
        }

        public static void MarkRefreshed(string vin, long completedRevision)
        {
            if (string.IsNullOrWhiteSpace(vin) || completedRevision == 0) return;
            lock (sync)
                if (changedVehicles.TryGetValue(vin, out long pending) && pending == completedRevision)
                    changedVehicles.Remove(vin);
        }
    }
}
