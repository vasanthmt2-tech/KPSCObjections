using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Threading;
using System.Web;
using System.Web.UI;
using KPSCObjections.Security;

namespace KPSCObjections
{
    /// <summary>
    /// Captcha image for the candidate login.
    ///
    /// Each image costs CPU (drawing) and memory (a server session holding the
    /// code), so requests are limited to keep a flood from exhausting the server:
    ///  - per client IP: CaptchaPerIpPerMinute, checked in Application_BeginRequest
    ///    (see <see cref="RejectIfFlooding"/>) before any session is created;
    ///  - server-wide: at most CaptchaMaxConcurrent images are drawn at once.
    /// </summary>
    public partial class Captcha : Page
    {
        private const string SessionKey = "CaptchaCode";

        // The refresh token the current code was drawn for, and how many times
        // the code has been accepted (see Check).
        private const string TokenKey = "CaptchaToken";
        private const string UsesKey = "CaptchaUses";

        private const int Width = 180;
        private const int Height = 40;

        private static readonly SemaphoreSlim Renderers =
            new SemaphoreSlim(RequestThrottle.Setting("CaptchaMaxConcurrent", 8));

        /// <summary>
        /// Called from Application_BeginRequest. Ends the request with 429 when
        /// the client has asked for too many captcha images this minute.
        /// Returns true when the request was rejected.
        /// </summary>
        public static bool RejectIfFlooding(HttpContext context)
        {
            string path = context.Request.AppRelativeCurrentExecutionFilePath ?? "";

            if (!path.Equals("~/Captcha", StringComparison.OrdinalIgnoreCase)
             && !path.Equals("~/Captcha.aspx", StringComparison.OrdinalIgnoreCase))
                return false;

            string ip = RequestThrottle.ClientIp(context);
            bool firstRejection;

            if (RequestThrottle.Allow("captcha", ip,
                    RequestThrottle.Setting("CaptchaPerIpPerMinute", 30),
                    TimeSpan.FromMinutes(1), out firstRejection))
                return false;

            // Log once per IP per minute, so a flood cannot also fill the disk.
            if (firstRejection)
                AppLog.Write("WARN", "Captcha requests throttled for " + ip);

            context.Response.StatusCode = 429;
            context.Response.StatusDescription = "Too Many Requests";
            context.Response.AppendHeader("Retry-After", "60");
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.ApplicationInstance.CompleteRequest();

            return true;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

            // Server busy drawing other images: answer at once rather than
            // queue up threads.
            if (!Renderers.Wait(TimeSpan.FromSeconds(2)))
            {
                Response.StatusCode = 503;
                Response.AppendHeader("Retry-After", "5");
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            try
            {
                // The page asks for a new code by changing the "refresh" token in
                // the image URL. The same URL again (the browser reloading the
                // image after a postback, as images are never cached) redraws the
                // current code identically instead of replacing it, so the captcha
                // the user is reading does not change under them.
                string token = Request.QueryString["refresh"] ?? "";
                string captchaText = Session[SessionKey] as string;

                if (captchaText == null || !string.Equals(Session[TokenKey] as string, token, StringComparison.Ordinal))
                {
                    captchaText = GenerateCaptchaText();

                    Session[SessionKey] = captchaText;
                    Session[TokenKey] = token;
                    Session.Remove(UsesKey);
                }

                Response.ContentType = "image/png";
                Render(captchaText);
            }
            finally
            {
                Renderers.Release();
            }

            Context.ApplicationInstance.CompleteRequest();
        }

        private void Render(string captchaText)
        {
            using (Bitmap bitmap = new Bitmap(Width, Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.White);

                // Same code, same picture: redrawing must not give OCR software
                // several differently-noised copies of one code to compare.
                Random random = new Random(StableSeed(captchaText));

                // Background noise lines
                using (Pen pen = new Pen(Color.LightGray, 1))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        graphics.DrawLine(pen,
                            random.Next(0, Width), random.Next(0, Height),
                            random.Next(0, Width), random.Next(0, Height));
                    }
                }

                using (Brush brush = new SolidBrush(Color.DarkBlue))
                {
                    // Wide letters (W, M, H) can make the code wider than the image;
                    // shrink the font until all six characters fit.
                    float size = 20;
                    Font font = new Font("Georgia", size, FontStyle.Bold);

                    while (size > 12 && graphics.MeasureString(captchaText, font).Width > Width - 12)
                    {
                        font.Dispose();
                        size -= 1;
                        font = new Font("Georgia", size, FontStyle.Bold);
                    }

                    using (font)
                    {
                        SizeF text = graphics.MeasureString(captchaText, font);
                        graphics.DrawString(captchaText, font, brush,
                            (Width - text.Width) / 2, (Height - text.Height) / 2);
                    }
                }

                // Noise dots
                for (int i = 0; i < 25; i++)
                {
                    bitmap.SetPixel(random.Next(0, Width), random.Next(0, Height), Color.LightGray);
                }

                bitmap.Save(Response.OutputStream, ImageFormat.Png);
            }
        }

        /// <summary>
        /// Checks the code the user typed and allows it to be used up to
        /// <paramref name="maxUses"/> times (e.g. Get OTP and Resend OTP with the
        /// same captcha). A wrong code is retired at once. <paramref name="retired"/>
        /// tells the caller to show a new image (wrong, or uses used up).
        /// </summary>
        public static bool Check(HttpSessionStateBase session, string entered, int maxUses, out bool retired)
        {
            string expected = session[SessionKey] as string;

            bool ok = !string.IsNullOrEmpty(expected)
                && string.Equals((entered ?? "").Trim(), expected, StringComparison.OrdinalIgnoreCase);

            int uses = (session[UsesKey] as int?) ?? 0;

            if (!ok || uses + 1 >= maxUses)
            {
                session.Remove(SessionKey);
                session.Remove(UsesKey);
                retired = true;
            }
            else
            {
                session[UsesKey] = uses + 1;
                retired = false;
            }

            return ok;
        }

        /// <summary>
        /// Checks the code the user typed against the one last shown to this
        /// session. The code is single use: it is removed whatever the result,
        /// so each attempt needs a new image.
        /// </summary>
        public static bool Consume(HttpSessionStateBase session, string entered)
        {
            string expected = session[SessionKey] as string;

            session.Remove(SessionKey);

            return !string.IsNullOrEmpty(expected)
                && string.Equals((entered ?? "").Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A fresh image URL, so the browser does not reuse the old picture.</summary>
        public static string NewImageUrl()
        {
            return "~/Captcha?refresh=" + Guid.NewGuid().ToString("N");
        }

        /// <summary>A seed that depends only on the code (string.GetHashCode is not stable across runs).</summary>
        private static int StableSeed(string text)
        {
            unchecked
            {
                int hash = 17;

                foreach (char c in text)
                    hash = hash * 31 + c;

                return hash;
            }
        }

        private static string GenerateCaptchaText()
        {
            const string characters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

            char[] captcha = new char[6];
            byte[] bytes = new byte[6];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            // 32 characters divide 256 evenly, so every character is equally likely.
            for (int i = 0; i < captcha.Length; i++)
                captcha[i] = characters[bytes[i] % characters.Length];

            return new string(captcha);
        }
    }
}
