using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace KPSCExamPortal.Security
{
    /// <summary>
    /// Encryption of form submissions in the browser (Scripts/form-encrypt.js).
    ///
    /// The server holds an RSA-2048 key pair; pages carry only the public key.
    /// On every submit the browser creates a random AES-256 key and HMAC-SHA256
    /// key, encrypts all form fields and files with AES-CBC, seals them with the
    /// HMAC, encrypts the two keys with the public key (RSA-OAEP-SHA256) and posts
    /// one opaque value as text/plain: __ENC=key.iv.data.mac (base64).
    ///
    /// Here, before ASP.NET reads the request, the body is decrypted, checked and
    /// rebuilt as the ordinary multipart form ASP.NET expects, so pages, event
    /// validation and every other check work unchanged. A POST that is not
    /// encrypted this way is refused, except SBI ePay's own callbacks.
    ///
    /// The key pair is created when the application starts and kept in memory
    /// only; after a restart, a page opened before it is refused once (the user
    /// reloads it).
    /// </summary>
    public static class EncryptedForms
    {
        public const string FieldName = "__ENC";

        private static RSA rsa;
        private static string publicKey;

        /// <summary>Paths that receive posts from outside (SBI ePay); these stay plain.</summary>
        private static readonly string[] PlainPostPaths =
        {
            "~/Objections/PaymentResponse", "~/Objections/PaymentResponse.aspx", "~/Objections/PaymentPush.ashx"
        };

        /// <summary>Call once from Application_Start.</summary>
        public static void Initialize()
        {
            RSA key = new RSACng(2048);
            RSAParameters p = key.ExportParameters(false);

            rsa = key;
            publicKey = Base64Url(p.Modulus) + "." + Base64Url(p.Exponent);
        }

        /// <summary>The public key for the page: base64url modulus "." exponent (JWK n and e).</summary>
        public static string PublicKey
        {
            get { return publicKey; }
        }

        /// <summary>
        /// Call from Application_BeginRequest. Installs the decryption for an
        /// encrypted post; refuses a plain post. Returns true when the request
        /// was refused (the response is complete).
        /// </summary>
        public static bool OnBeginRequest(HttpContext context)
        {
            HttpRequest request = context.Request;

            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                return false;

            string path = request.AppRelativeCurrentExecutionFilePath ?? "";

            if (PlainPostPaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                return false;

            if ((request.ContentType ?? "").StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
            {
                // Decrypted and rebuilt when ASP.NET first reads the body.
                string boundary = "----KPSCEnc" + Guid.NewGuid().ToString("N");

                request.Filter = new DecryptingFilter(request.Filter, boundary);
                request.ContentType = "multipart/form-data; boundary=" + boundary;
                return false;
            }

            AppLog.Write("WARN", "Unencrypted form post refused from " + RequestThrottle.ClientIp(context) +
                         " " + request.HttpMethod + " " + request.Path);

            context.Response.StatusCode = 400;
            context.Response.TrySkipIisCustomErrors = true;
            context.Response.ContentType = "text/plain";
            context.Response.Write("The request was rejected. Please reload the page and try again.");
            context.ApplicationInstance.CompleteRequest();
            return true;
        }

        /// <summary>Decrypts "key.iv.data.mac" and rebuilds the form as multipart/form-data.</summary>
        private static byte[] DecryptToMultipart(byte[] body, string boundary)
        {
            string text = Encoding.ASCII.GetString(body).Trim();
            string prefix = FieldName + "=";

            if (!text.StartsWith(prefix, StringComparison.Ordinal))
                throw new EncryptedRequestException("missing " + FieldName);

            string[] parts = text.Substring(prefix.Length).Trim().Split('.');

            if (parts.Length != 4)
                throw new EncryptedRequestException("malformed");

            byte[] keys, iv, data, mac;

            try
            {
                keys = rsa.Decrypt(Convert.FromBase64String(parts[0]), RSAEncryptionPadding.OaepSHA256);
                iv = Convert.FromBase64String(parts[1]);
                data = Convert.FromBase64String(parts[2]);
                mac = Convert.FromBase64String(parts[3]);
            }
            catch (Exception ex)
            {
                // A wrong key (e.g. a page opened before a restart) or garbage.
                throw new EncryptedRequestException("cannot decrypt key: " + ex.GetType().Name);
            }

            if (keys.Length != 64 || iv.Length != 16)
                throw new EncryptedRequestException("bad key material");

            byte[] aesKey = keys.Take(32).ToArray();
            byte[] macKey = keys.Skip(32).ToArray();

            using (HMACSHA256 hmac = new HMACSHA256(macKey))
            {
                byte[] expected = hmac.ComputeHash(iv.Concat(data).ToArray());

                if (!PasswordHasher.FixedTimeEquals(expected, mac))
                    throw new EncryptedRequestException("integrity check failed");
            }

            byte[] plain;

            using (Aes aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    plain = decryptor.TransformFinalBlock(data, 0, data.Length);
            }

            JObject payload;

            try
            {
                payload = JObject.Parse(Encoding.UTF8.GetString(plain));
            }
            catch (Exception)
            {
                throw new EncryptedRequestException("bad payload");
            }

            return BuildMultipart(payload, boundary);
        }

        /// <summary>
        /// Payload: { "f": [[name, value], ...], "u": [[name, fileName, type, base64], ...] }.
        /// </summary>
        private static byte[] BuildMultipart(JObject payload, string boundary)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Action<string> write = s => { byte[] b = Encoding.UTF8.GetBytes(s); ms.Write(b, 0, b.Length); };

                foreach (JToken field in (payload["f"] as JArray) ?? new JArray())
                {
                    write("--" + boundary + "\r\n");
                    write("Content-Disposition: form-data; name=\"" + Clean((string)field[0]) + "\"\r\n\r\n");
                    write(((string)field[1] ?? "") + "\r\n");
                }

                foreach (JToken file in (payload["u"] as JArray) ?? new JArray())
                {
                    byte[] content = Convert.FromBase64String((string)file[3] ?? "");
                    string type = (string)file[2];

                    write("--" + boundary + "\r\n");
                    write("Content-Disposition: form-data; name=\"" + Clean((string)file[0]) +
                          "\"; filename=\"" + Clean((string)file[1]) + "\"\r\n");
                    write("Content-Type: " + (string.IsNullOrEmpty(type) ? "application/octet-stream" : Clean(type)) + "\r\n\r\n");
                    ms.Write(content, 0, content.Length);
                    write("\r\n");
                }

                write("--" + boundary + "--\r\n");
                return ms.ToArray();
            }
        }

        /// <summary>Keeps a name or header value on one line and inside its quotes.</summary>
        private static string Clean(string value)
        {
            return new string((value ?? "").Where(c => c != '"' && c != '\r' && c != '\n').ToArray());
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>Request filter: reads the encrypted body once and serves the rebuilt one.</summary>
        private sealed class DecryptingFilter : Stream
        {
            private readonly Stream source;
            private readonly string boundary;
            private MemoryStream output;

            public DecryptingFilter(Stream source, string boundary)
            {
                this.source = source;
                this.boundary = boundary;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (output == null)
                {
                    using (MemoryStream raw = new MemoryStream())
                    {
                        source.CopyTo(raw);
                        output = new MemoryStream(DecryptToMultipart(raw.ToArray(), boundary));
                    }
                }

                return output.Read(buffer, offset, count);
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        }
    }

    /// <summary>An encrypted form post that could not be decrypted or verified (answered with 400).</summary>
    public class EncryptedRequestException : HttpException
    {
        public EncryptedRequestException(string reason) : base(400, "Encrypted request rejected: " + reason)
        {
        }
    }
}
