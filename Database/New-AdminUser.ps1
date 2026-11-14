# Creates an admin user, or resets the password of an existing one.
# The password is prompted for, never passed on the command line, and only
# its PBKDF2-HMAC-SHA256 hash is stored (same scheme as Security\PasswordHasher.cs).
param(
    [Parameter(Mandatory = $true)][string]$UserName,
    [string]$Server = $env:COMPUTERNAME,
    [string]$Database = 'KPSC_OBJECTIONS'
)

$ErrorActionPreference = 'Stop'
$iterations = 600000

$secure = Read-Host -AsSecureString "Password for '$UserName'"
$confirm = Read-Host -AsSecureString 'Confirm password'
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
$bstr2 = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($confirm)
try {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    $plain2 = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr2)
    if ($plain -cne $plain2) { throw 'Passwords do not match.' }
    if ($plain.Length -lt 12) { throw 'Password must be at least 12 characters.' }

    $salt = New-Object byte[] 16
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($salt)
    $rng.Dispose()

    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes(
        $plain, $salt, $iterations, [Security.Cryptography.HashAlgorithmName]::SHA256)
    $hash = $kdf.GetBytes(32)
    $kdf.Dispose()
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr2)
    $plain = $null; $plain2 = $null
}

$cn = New-Object Data.SqlClient.SqlConnection "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
$cmd = $cn.CreateCommand()
$cmd.CommandText = @'
MERGE dbo.AdminUsers AS t
USING (SELECT @UserName AS UserName) AS s ON t.UserName = s.UserName
WHEN MATCHED THEN UPDATE SET PasswordHash = @Hash, PasswordSalt = @Salt, Iterations = @Iterations,
                             IsActive = 1, FailedAttempts = 0, LockoutUntil = NULL
WHEN NOT MATCHED THEN INSERT (UserName, PasswordHash, PasswordSalt, Iterations)
                      VALUES (@UserName, @Hash, @Salt, @Iterations);
'@
[void]$cmd.Parameters.AddWithValue('@UserName', $UserName.Trim())
$cmd.Parameters.Add('@Hash', [Data.SqlDbType]::VarBinary, 32).Value = $hash
$cmd.Parameters.Add('@Salt', [Data.SqlDbType]::VarBinary, 16).Value = $salt
[void]$cmd.Parameters.AddWithValue('@Iterations', $iterations)
$cn.Open()
try { [void]$cmd.ExecuteNonQuery() } finally { $cn.Close() }

Write-Output "Admin user '$($UserName.Trim())' saved."
