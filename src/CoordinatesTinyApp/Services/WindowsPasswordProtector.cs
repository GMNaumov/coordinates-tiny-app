using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace CoordinatesTinyApp.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsPasswordProtector : IPasswordProtector
{
    public string Protect(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);

        try
        {
            // Ключом управляет Windows; файл расшифровывается в контексте текущего пользователя.
            return Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public string Unprotect(string encryptedPassword)
    {
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedPassword), null, DataProtectionScope.CurrentUser);

        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
