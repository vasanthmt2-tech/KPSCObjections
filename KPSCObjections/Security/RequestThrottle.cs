using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;
using System.Web.Caching;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Fixed-window request counters kept in the ASP.NET cache, used to stop
    /// one client from exhausting the server (CPU, memory, sessions) by
    /// hammering an endpoint. Entries expire with their window, so memory use
    /// stays bounded by the number of clients seen in the last window.
    /// </summary>
    public static class RequestThrottle
    {
        private sealed class Counter
        {
            public int Count;
        }

        /// <summary>
        /// Counts one request for <paramref name="key"/> in <paramref name="bucket"/>.
        /// Returns false once more than <paramref name="limit"/> requests were
        /// made in the current window.
        /// </summary>
        public static bool Allow(string bucket, string key, int limit, TimeSpan window, out bool firstRejection)
        {
            string cacheKey = "throttle:" + bucket + ":" + key;

            Counter fresh = new Counter();

            // Add returns the existing entry if there is one, else stores ours.
            Counter counter = HttpRuntime.Cache.Add(
                cacheKey, fresh, null,
                DateTime.UtcNow.Add(window), Cache.NoSlidingExpiration,
                CacheItemPriority.Normal, null) as Counter ?? fresh;

            int count = Interlocked.Increment(ref counter.Count);

            firstRejection = count == limit + 1;

            return count <= limit;
        }

        /// <summary>
        /// The client's IP address. X-Forwarded-For is used only when the
        /// direct peer is listed in the TrustedProxies setting (comma-separated
        /// IPs), so a client cannot dodge the limits by sending that header.
        /// </summary>
        public static string ClientIp(HttpContext context)
        {
            string peer = context.Request.UserHostAddress ?? "";

            string trusted = ConfigurationManager.AppSettings["TrustedProxies"];

            if (string.IsNullOrWhiteSpace(trusted))
                return peer;

            bool fromProxy = trusted.Split(',').Select(s => s.Trim()).Any(s => s == peer);

            if (!fromProxy)
                return peer;

            string forwarded = context.Request.Headers["X-Forwarded-For"];

            if (string.IsNullOrWhiteSpace(forwarded))
                return peer;

            // The proxy appends the address it saw last.
            string last = forwarded.Split(',').Last().Trim();
            IPAddress parsed;

            return IPAddress.TryParse(last, out parsed) ? parsed.ToString() : peer;
        }

        public static int Setting(string key, int fallback)
        {
            int value;

            return int.TryParse(ConfigurationManager.AppSettings[key], out value) && value > 0
                ? value
                : fallback;
        }
    }
}
