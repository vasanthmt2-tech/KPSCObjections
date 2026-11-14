using System;
using System.Data;
using System.Data.SqlClient;
using KPSCObjections.DLL;

namespace KPSCObjections.Security
{
    /// <summary>Checks admin credentials against dbo.AdminUsers.</summary>
    public static class AdminAuth
    {
        public enum Result
        {
            Ok,
            Invalid,
            LockedOut
        }

        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        // Used to spend the same hashing time when the user name does not exist,
        // so response time does not reveal which user names are real.
        private const int DummyIterations = 600000;
        private static readonly byte[] DummySalt = new byte[16];

        public static Result Verify(string userName, string password)
        {
            SqlHelper sh = new SqlHelper();

            DataTable dt = sh.ExecuteDataTable(
                "SELECT PasswordHash, PasswordSalt, Iterations, " +
                "CASE WHEN LockoutUntil IS NOT NULL AND LockoutUntil > GETDATE() THEN 1 ELSE 0 END AS IsLocked " +
                "FROM dbo.AdminUsers WHERE UserName = @UserName AND IsActive = 1",
                new SqlParameter("@UserName", SqlDbType.VarChar, 50) { Value = userName });

            if (dt.Rows.Count == 0)
            {
                PasswordHasher.Hash(password, DummySalt, DummyIterations);
                return Result.Invalid;
            }

            DataRow row = dt.Rows[0];

            if (Convert.ToInt32(row["IsLocked"]) == 1)
                return Result.LockedOut;

            byte[] expected = (byte[])row["PasswordHash"];
            byte[] actual = PasswordHasher.Hash(
                password,
                (byte[])row["PasswordSalt"],
                Convert.ToInt32(row["Iterations"]));

            if (!PasswordHasher.FixedTimeEquals(expected, actual))
            {
                sh.ExecuteNonQuery(
                    "UPDATE dbo.AdminUsers SET " +
                    "LockoutUntil = CASE WHEN FailedAttempts + 1 >= @Max " +
                    "THEN DATEADD(MINUTE, @Minutes, GETDATE()) ELSE LockoutUntil END, " +
                    "FailedAttempts = CASE WHEN FailedAttempts + 1 >= @Max THEN 0 ELSE FailedAttempts + 1 END " +
                    "WHERE UserName = @UserName",
                    new SqlParameter("@UserName", SqlDbType.VarChar, 50) { Value = userName },
                    new SqlParameter("@Max", MaxFailedAttempts),
                    new SqlParameter("@Minutes", LockoutMinutes));

                return Result.Invalid;
            }

            sh.ExecuteNonQuery(
                "UPDATE dbo.AdminUsers SET FailedAttempts = 0, LockoutUntil = NULL, " +
                "LastLoginAt = GETDATE() WHERE UserName = @UserName",
                new SqlParameter("@UserName", SqlDbType.VarChar, 50) { Value = userName });

            return Result.Ok;
        }
    }
}
