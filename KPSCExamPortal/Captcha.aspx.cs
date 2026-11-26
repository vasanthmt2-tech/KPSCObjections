using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Threading;
using System.Web;
using System.Web.UI;
using KPSCExamPortal.Security;

namespace KPSCExamPortal
{
    /// <summary>
    /// Captcha for the candidate and admin login pages.
    ///
    /// The code belongs to the page that shows it, not to the image URL:
    ///  - the login page asks for a new code (<see cref="NewToken"/>) and keeps
    ///    the returned token in its own (encrypted, signed) ViewState;
    ///  - the token is the code sealed with a key only the server has
    ///    (AES-256 + HMAC-SHA256), with an expiry and a random id;
    ///  - Captcha?t=token only draws the code sealed in the token. It never
    ///    makes a new code and keeps nothing in the session, so loading the image
    ///    URL again, in another tab, or with an altered token cannot change or
    ///    invalidate the code the login page will check. A token the server did
    ///    not issue gets 404;
    ///  - <see cref="Check"/> compares what was typed with the page's token and
    ///    counts uses per token, so a solved code cannot be replayed.
    ///
    /// Each image costs CPU (drawing), so requests are limited to keep a flood
    /// from exhausting the server:
    ///  - per client IP: CaptchaPerIpPerMinute, checked in Application_BeginRequest
    ///    (see <see cref="RejectIfFlooding"/>);
    ///  - server-wide: at most CaptchaMaxConcurrent images are drawn at once.
    /// </summary>
    public partial class Captcha : Page
    {
        /// <summary>How long a captcha can be used after the page showed it.</summary>
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(20);

        private const int CodeLength = 6;
        private const int IdLength = 16;

        // Created at start-up; a restart makes captchas shown before it invalid
        // (the user just gets a new one).
        private static readonly byte[] EncryptionKey = RandomBytes(32);
        private static readonly byte[] MacKey = RandomBytes(32);

        // Uses of each token, so a code cannot be replayed (see Check).
        private static readonly object UsesLock = new object();
        private const int Retired = int.MaxValue;

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
                // Only draws the code sealed in the token; nothing is created or
                // changed here (see the class comment).
                Payload payload;

                if (!TryOpen(Request.QueryString["t"], out payload))
                {
                    Response.StatusCode = 404;
                }
                else
                {
                    Response.ContentType = "image/png";
                    Render(payload.Code);
                }
            }
            finally
            {
                Renderers.Release();
            }

            Context.ApplicationInstance.CompleteRequest();
        }

        /// <summary>
        /// The response is the PNG (or an empty 404) written in Page_Load. Without
        /// this, Web Forms would still render the page's markup after the image.
        /// </summary>
        protected override void Render(System.Web.UI.HtmlTextWriter writer)
        {
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
                    float size = 18;
                    Font font = new Font("Georgia", size, FontStyle.Bold);

                    while (size > 10 && graphics.MeasureString(captchaText, font).Width > Width - 12)
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
        /// A new captcha for a page: returns the token, which the page keeps (in
        /// ViewState) and passes to <see cref="ImageUrl"/> and <see cref="Check"/>.
        /// </summary>
        public static string NewToken()
        {
            byte[] plain = new byte[IdLength + 8 + CodeLength];

            System.Buffer.BlockCopy(RandomBytes(IdLength), 0, plain, 0, IdLength);
            System.Buffer.BlockCopy(BitConverter.GetBytes(DateTime.UtcNow.Add(Lifetime).Ticks), 0, plain, IdLength, 8);
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(GenerateCaptchaText()), 0, plain, IdLength + 8, CodeLength);

            byte[] iv = RandomBytes(16);
            byte[] cipher;

            using (Aes aes = Aes.Create())
            using (ICryptoTransform encryptor = aes.CreateEncryptor(EncryptionKey, iv))
            {
                cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            }

            byte[] sealedBytes = new byte[iv.Length + cipher.Length + 32];
            System.Buffer.BlockCopy(iv, 0, sealedBytes, 0, iv.Length);
            System.Buffer.BlockCopy(cipher, 0, sealedBytes, iv.Length, cipher.Length);

            using (HMACSHA256 hmac = new HMACSHA256(MacKey))
            {
                byte[] mac = hmac.ComputeHash(sealedBytes, 0, iv.Length + cipher.Length);
                System.Buffer.BlockCopy(mac, 0, sealedBytes, iv.Length + cipher.Length, mac.Length);
            }

            return HttpServerUtility.UrlTokenEncode(sealedBytes);
        }

        /// <summary>The image URL for a token from <see cref="NewToken"/>.</summary>
        public static string ImageUrl(string token)
        {
            return "~/Captcha?t=" + token;
        }

        /// <summary>
        /// Checks the code the user typed against the page's captcha token and
        /// allows a correct code to be used up to <paramref name="maxUses"/> times
        /// (e.g. Get OTP and Resend OTP with the same captcha). A wrong code
        /// retires the token at once. <paramref name="retired"/> tells the caller
        /// to show a new captcha (wrong, expired, or uses used up).
        /// </summary>
        public static bool Check(string token, string entered, int maxUses, out bool retired)
        {
            retired = true;

            Payload payload;

            if (!TryOpen(token, out payload))
                return false;

            bool ok = string.Equals((entered ?? "").Trim(), payload.Code, StringComparison.OrdinalIgnoreCase);

            lock (UsesLock)
            {
                int uses = (HttpRuntime.Cache["captcha-uses:" + payload.Id] as int?) ?? 0;

                if (uses >= maxUses)
                    return false;

                uses = ok ? uses + 1 : Retired;
                retired = uses >= maxUses;

                HttpRuntime.Cache.Insert("captcha-uses:" + payload.Id, uses, null, payload.ExpiresUtc,
                    System.Web.Caching.Cache.NoSlidingExpiration);
            }

            return ok;
        }

        /// <summary>A single-use check: the page shows a new captcha after every attempt.</summary>
        public static bool Consume(string token, string entered)
        {
            bool retired;
            return Check(token, entered, 1, out retired);
        }

        private struct Payload
        {
            public string Id;
            public DateTime ExpiresUtc;
            public string Code;
        }

        /// <summary>Opens a token issued by this server that has not expired.</summary>
        private static bool TryOpen(string token, out Payload payload)
        {
            payload = default(Payload);

            if (string.IsNullOrEmpty(token) || token.Length > 200)
                return false;

            byte[] sealedBytes;

            try
            {
                sealedBytes = HttpServerUtility.UrlTokenDecode(token);
            }
            catch (FormatException)
            {
                return false;
            }

            if (sealedBytes == null || sealedBytes.Length < 16 + 16 + 32)
                return false;

            int cipherLength = sealedBytes.Length - 16 - 32;
            byte[] mac;

            using (HMACSHA256 hmac = new HMACSHA256(MacKey))
            {
                mac = hmac.ComputeHash(sealedBytes, 0, 16 + cipherLength);
            }

            int diff = 0;

            for (int i = 0; i < 32; i++)
                diff |= mac[i] ^ sealedBytes[16 + cipherLength + i];

            if (diff != 0)
                return false;

            byte[] iv = new byte[16];
            System.Buffer.BlockCopy(sealedBytes, 0, iv, 0, 16);

            byte[] plain;

            try
            {
                using (Aes aes = Aes.Create())
                using (ICryptoTransform decryptor = aes.CreateDecryptor(EncryptionKey, iv))
                {
                    plain = decryptor.TransformFinalBlock(sealedBytes, 16, cipherLength);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }

            if (plain.Length != IdLength + 8 + CodeLength)
                return false;

            DateTime expires = new DateTime(BitConverter.ToInt64(plain, IdLength), DateTimeKind.Utc);

            if (expires <= DateTime.UtcNow)
                return false;

            payload.Id = BitConverter.ToString(plain, 0, IdLength);
            payload.ExpiresUtc = expires;
            payload.Code = System.Text.Encoding.ASCII.GetString(plain, IdLength + 8, CodeLength);

            return true;
        }

        private static byte[] RandomBytes(int count)
        {
            byte[] bytes = new byte[count];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return bytes;
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

            char[] captcha = new char[CodeLength];
            byte[] bytes = new byte[CodeLength];

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
