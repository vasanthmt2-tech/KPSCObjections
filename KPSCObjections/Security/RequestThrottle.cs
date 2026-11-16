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

        /// <summary>Requests counted for <paramref name="key"/> in the current window (0 if none).</summary>
        public static int Count(string bucket, string key)
        {
            Counter counter = HttpRuntime.Cache["throttle:" + bucket + ":" + key] as Counter;

            return counter == null ? 0 : Volatile.Read(ref counter.Count);
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

        /// <summary>
        /// Per-IP limit for a login action (Get OTP, OTP login, admin login).
        /// Returns false when the client IP has used up <paramref name="settingKey"/>
        /// attempts in the current minute; the response status is then set to
        /// 429 with Retry-After, and the first refusal of the minute is logged.
        /// The caller shows its own message and stops.
        /// </summary>
        public static bool AllowLoginAttempt(HttpContext context, string action, string settingKey, int fallback)
        {
            string ip = ClientIp(context);
            int limit = Setting(settingKey, fallback);
            bool firstRejection;

            if (Allow("login-" + action, ip, limit, TimeSpan.FromMinutes(1), out firstRejection))
                return true;

            if (firstRejection)
            {
                AppLog.Write("WARN", "Too many " + action + " attempts (over " + limit +
                             " a minute) from " + ip + "; further attempts refused for the rest of the minute.");
            }

            context.Response.StatusCode = 429;
            context.Response.TrySkipIisCustomErrors = true;
            context.Response.AppendHeader("Retry-After", "60");

            return false;
        }

        /// <summary>Message shown when <see cref="AllowLoginAttempt"/> refuses.</summary>
        public const string TooManyAttemptsMessage =
            "Too many attempts from your network. Please wait a minute and try again.";

        public static int Setting(string key, int fallback)
        {
            int value;

            return int.TryParse(ConfigurationManager.AppSettings[key], out value) && value > 0
                ? value
                : fallback;
        }
    }
}
