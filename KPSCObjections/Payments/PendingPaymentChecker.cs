using System;
using System.Configuration;
using System.Threading;
using System.Web.Hosting;
using KPSCObjections.Security;

namespace KPSCObjections.Payments
{
    /// <summary>
    /// Re-checks every payment still awaiting verification with SBI ePay's
    /// double verification (status query) every PendingPaymentCheckMinutes
    /// (default 5; 0 turns it off), so payments resolve even when the candidate
    /// never comes back and SBI's push does not arrive. It does exactly what the
    /// admin "Re-check Pending Payments" button does.
    ///
    /// Runs inside the web application: it starts with the site and stops when
    /// IIS recycles or stops it. A run is skipped if the previous one is still
    /// going. Checks from pages, the push and this timer can overlap safely:
    /// the database only changes a payment that is still pending.
    /// </summary>
    public sealed class PendingPaymentChecker : IRegisteredObject
    {
        private const int DefaultMinutes = 5;
        private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(1);

        private static PendingPaymentChecker instance;

        private readonly Timer timer;
        private int running;

        private PendingPaymentChecker(TimeSpan interval)
        {
            timer = new Timer(Tick, null, FirstRunDelay, interval);
        }

        /// <summary>Call once from Application_Start.</summary>
        public static void Start()
        {
            int minutes;

            if (!int.TryParse(ConfigurationManager.AppSettings["PendingPaymentCheckMinutes"], out minutes))
                minutes = DefaultMinutes;

            if (minutes <= 0 || instance != null)
                return;

            instance = new PendingPaymentChecker(TimeSpan.FromMinutes(minutes));
            HostingEnvironment.RegisterObject(instance);

            AppLog.Write("INFO", "Automatic re-check of pending payments every " + minutes + " minute(s) started.");
        }

        private void Tick(object state)
        {
            if (Interlocked.Exchange(ref running, 1) == 1)
                return;

            try
            {
                int succeeded, failed, stillPending;

                PaymentService.VerifyAllPending(out succeeded, out failed, out stillPending);

                // Quiet unless something changed, so the log is not filled every few minutes.
                if (succeeded + failed > 0)
                {
                    AppLog.Write("INFO", "Automatic payment re-check: " + succeeded + " confirmed, " +
                                 failed + " not completed, " + stillPending + " still pending.");
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Automatic payment re-check failed" + Environment.NewLine + ex);
            }
            finally
            {
                Interlocked.Exchange(ref running, 0);
            }
        }

        /// <summary>Called by ASP.NET when the application shuts down (recycle, stop, deploy).</summary>
        public void Stop(bool immediate)
        {
            timer.Dispose();
            HostingEnvironment.UnregisterObject(this);
            instance = null;
        }
    }
}
