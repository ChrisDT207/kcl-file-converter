using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ZooStepConverter.DataAccess
{
    /// <summary>
    /// Data Access Layer: Handles secure DPAPI persistence for Zoo authentication tokens.
    /// </summary>
    public static class TokenRepository
    {
        public static string TokenPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ZooStepConverter",
                    "token.dat");
            }
        }

        public static void SaveToken(string token)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TokenPath));
                byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(TokenPath, enc);
            }
            catch { }
        }

        public static string LoadToken()
        {
            try
            {
                if (File.Exists(TokenPath))
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(TokenPath), null, DataProtectionScope.CurrentUser));
            }
            catch { }
            return null;
        }
    }
}

