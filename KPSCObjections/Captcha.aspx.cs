
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Web;
using System.Web.UI;

namespace KPSCObjections
{
    public partial class Captcha : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentType = "image/png";

            Response.Cache.SetCacheability(
                HttpCacheability.NoCache);

            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

            string captchaText = GenerateCaptchaText();

            // Store captcha in Session
            Session["CaptchaCode"] = captchaText;

            using (Bitmap bitmap = new Bitmap(140, 40))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.White);

                Random random = new Random();

                // Background noise lines
                using (Pen pen = new Pen(Color.LightGray, 1))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        graphics.DrawLine(
                            pen,
                            random.Next(0, 140),
                            random.Next(0, 40),
                            random.Next(0, 140),
                            random.Next(0, 40)
                        );
                    }
                }

                // Draw captcha text
                using (Font font = new Font(
                    "Georgia",
                    20,
                    FontStyle.Bold))
                using (Brush brush = new SolidBrush(
                    Color.DarkBlue))
                {
                    graphics.DrawString(
                        captchaText,
                        font,
                        brush,
                        8,
                        5
                    );
                }

                // Noise dots
                for (int i = 0; i < 25; i++)
                {
                    int x = random.Next(0, 140);
                    int y = random.Next(0, 40);

                    bitmap.SetPixel(x, y, Color.LightGray);
                }

                // Output captcha image
                bitmap.Save(
                    Response.OutputStream,
                    ImageFormat.Png
                );
            }

            Response.Flush();
            Context.ApplicationInstance.CompleteRequest();
        }

        private string GenerateCaptchaText()
        {
            const string characters =
                "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

            char[] captcha = new char[6];

            using (RandomNumberGenerator rng =
                RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[6];

                rng.GetBytes(bytes);

                for (int i = 0; i < captcha.Length; i++)
                {
                    captcha[i] =
                        characters[bytes[i] % characters.Length];
                }
            }

            return new string(captcha);
        }
    }
}
