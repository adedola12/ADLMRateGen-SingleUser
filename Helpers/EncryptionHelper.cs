using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ADLMRateGen.Helpers
{
    public static class EncryptionHelper
    {
        private static readonly string PassPhrase = "[REDACTED-ENCRYPTION-PASSPHRASE]"; // any length
        private static readonly string IvPhrase = "ADLM_Init_Vector";     // any length ≥16

        private static byte[] GetKeyBytes()
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(PassPhrase)); // 32 bytes
            }
        }

        private static byte[] GetIvBytes()
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(IvPhrase)); // 16 bytes
            }
        }

        public static string Encrypt(string plainText)
        {
            if (plainText == null) plainText = string.Empty;

            using (var aes = Aes.Create())
            {
                aes.Key = GetKeyBytes();
                aes.IV  = GetIvBytes();
                // aes.Mode = CipherMode.CBC; // default
                // aes.Padding = PaddingMode.PKCS7; // default

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs, Encoding.UTF8))
                {
                    sw.Write(plainText);
                    sw.Flush();
                    cs.FlushFinalBlock();
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            var bytes = Convert.FromBase64String(cipherText);

            using (var aes = Aes.Create())
            {
                aes.Key = GetKeyBytes();
                aes.IV  = GetIvBytes();

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(bytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
