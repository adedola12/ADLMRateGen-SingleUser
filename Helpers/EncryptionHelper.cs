using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ADLMRateGen.Helpers
{
    /// <summary>
    /// AES-256 helper used for encrypting small values at rest.
    ///
    /// Security changes vs. the legacy implementation:
    ///   - The passphrase is resolved from ADLM_RATEGEN_ENCRYPTION_KEY (no
    ///     hardcoded fallback — a constant in the DLL can be trivially
    ///     extracted by decompilation and used to read any encrypted value).
    ///   - A fresh random IV is generated per encryption and prepended to
    ///     the ciphertext so identical plaintexts produce different outputs.
    ///   - Decrypt() first tries the new "IV-prepended" format; if that fails
    ///     and ADLM_RATEGEN_LEGACY_IV_PHRASE is configured, it falls back to
    ///     the legacy static-IV scheme so existing stored data still reads.
    ///     Re-encrypting any value will migrate it forward.
    /// </summary>
    public static class EncryptionHelper
    {
        private const int IvSize = 16; // AES block size

        private static byte[] GetKeyBytes()
        {
            var pass = Environment.GetEnvironmentVariable("ADLM_RATEGEN_ENCRYPTION_KEY");
            if (string.IsNullOrWhiteSpace(pass))
            {
                throw new InvalidOperationException(
                    "ADLM_RATEGEN_ENCRYPTION_KEY environment variable is not configured.");
            }

            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(pass));
            }
        }

        private static byte[] GetLegacyIvBytes()
        {
            var iv = Environment.GetEnvironmentVariable("ADLM_RATEGEN_LEGACY_IV_PHRASE");
            if (string.IsNullOrEmpty(iv)) return null;

            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(iv));
            }
        }

        public static string Encrypt(string plainText)
        {
            if (plainText == null) plainText = string.Empty;

            using (var aes = Aes.Create())
            {
                aes.Key = GetKeyBytes();
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                    using (var sw = new StreamWriter(cs, Encoding.UTF8))
                    {
                        sw.Write(plainText);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            var bytes = Convert.FromBase64String(cipherText);

            if (bytes.Length > IvSize)
            {
                try
                {
                    using (var aes = Aes.Create())
                    {
                        aes.Key = GetKeyBytes();
                        var iv = new byte[IvSize];
                        Array.Copy(bytes, 0, iv, 0, IvSize);
                        aes.IV = iv;

                        using (var decryptor = aes.CreateDecryptor())
                        using (var ms = new MemoryStream(bytes, IvSize, bytes.Length - IvSize))
                        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        using (var sr = new StreamReader(cs, Encoding.UTF8))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
                catch (CryptographicException)
                {
                    // Fall through to legacy path.
                }
            }

            var legacyIv = GetLegacyIvBytes();
            if (legacyIv != null)
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = GetKeyBytes();
                    aes.IV = legacyIv;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(bytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs, Encoding.UTF8))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }

            throw new CryptographicException(
                "Unable to decrypt value: ciphertext format is not recognized and no legacy IV is configured.");
        }
    }
}
