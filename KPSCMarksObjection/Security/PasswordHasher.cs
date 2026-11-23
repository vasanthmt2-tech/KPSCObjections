using System.Security.Cryptography;

namespace KPSCMarksObjection.Security
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256 password hashing. Database\New-AdminUser.ps1 uses the
    /// same scheme, so keep the two in step.
    /// </summary>
    public static class PasswordHasher
    {
        public const int HashSize = 32;

        public static byte[] Hash(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(
                password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return kdf.GetBytes(HashSize);
            }
        }

        /// <summary>Compares without leaking where the first difference is.</summary>
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int diff = 0;

            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }
    }
}
