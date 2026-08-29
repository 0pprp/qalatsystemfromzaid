using System.Security.Cryptography;
using System.Text;

namespace BE_Company.Utilities
{
    /// <summary>
    /// Helper for encrypting/decrypting customer portal access tokens.
    /// Token format (plaintext): customerID|expiryTimestamp (Unix seconds)
    /// The encrypted + Base64 token is safe to include in URLs.
    /// </summary>
    public static class EncryptionHelper
    {
        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int IvSize = 16; // AES block size

        /// <summary>
        /// Encrypts a plaintext string using AES-256-CBC with a random IV prepended.
        /// Returns Base64-encoded ciphertext suitable for URLs.
        /// </summary>
        public static string Encrypt(string plainText, string base64Key)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException(nameof(plainText));
            if (string.IsNullOrEmpty(base64Key))
                throw new ArgumentNullException(nameof(base64Key));

            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = GenerateRandomIv();

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Prepend IV to ciphertext for later decryption
            byte[] result = new byte[iv.Length + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, iv.Length, cipherBytes.Length);

            return Convert.ToBase64String(result)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Decrypts a Base64-encoded ciphertext (with prepended IV).
        /// Returns the original plaintext, or null if decryption fails.
        /// </summary>
        public static string? Decrypt(string cipherText, string base64Key)
        {
            if (string.IsNullOrEmpty(cipherText))
                return null;
            if (string.IsNullOrEmpty(base64Key))
                return null;

            try
            {
                // Restore Base64 padding and characters
                string base64 = cipherText
                    .Replace('-', '+')
                    .Replace('_', '/');

                // Add padding if needed
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                byte[] fullCipher = Convert.FromBase64String(base64);
                byte[] key = Convert.FromBase64String(base64Key);

                if (fullCipher.Length < IvSize)
                    return null;

                byte[] iv = new byte[IvSize];
                byte[] cipherBytes = new byte[fullCipher.Length - IvSize];
                Buffer.BlockCopy(fullCipher, 0, iv, 0, IvSize);
                Buffer.BlockCopy(fullCipher, IvSize, cipherBytes, 0, cipherBytes.Length);

                using var aes = Aes.Create();
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return null; // Tampered or invalid token
            }
        }

        /// <summary>
        /// Creates an encrypted token for a customer ID with optional branch ID.
        /// Token format: customerId  or  customerId|branchId
        /// Tokens never expire.
        /// </summary>
        public static string CreateCustomerToken(int customerId, string base64Key, int? branchId = null)
        {
            string plainText = branchId.HasValue
                ? $"{customerId}|{branchId.Value}"
                : $"{customerId}";
            return Encrypt(plainText, base64Key);
        }

        /// <summary>
        /// Validates and extracts customer ID from an encrypted token.
        /// Supports formats: "customerId" or "customerId|branchId".
        /// Tokens never expire.
        /// </summary>
        public static int? ValidateAndGetCustomerId(string token, string base64Key)
        {
            string? plainText = Decrypt(token, base64Key);
            if (string.IsNullOrEmpty(plainText))
                return null;

            string[] parts = plainText.Split('|');
            if (parts.Length < 1 || parts.Length > 2)
                return null;

            if (!int.TryParse(parts[0], out int customerId))
                return null;

            return customerId;
        }

        /// <summary>
        /// Validates token and returns both customer ID and branch ID (if present).
        /// Supports formats: "customerId" or "customerId|branchId".
        /// </summary>
        public static (int customerId, int? branchId)? ValidateAndGetCustomerIdWithBranch(string token, string base64Key)
        {
            string? plainText = Decrypt(token, base64Key);
            if (string.IsNullOrEmpty(plainText))
                return null;

            string[] parts = plainText.Split('|');
            if (parts.Length < 1 || parts.Length > 2)
                return null;

            if (!int.TryParse(parts[0], out int customerId))
                return null;

            int? branchId = null;
            if (parts.Length == 2 && int.TryParse(parts[1], out int bid))
                branchId = bid;

            return (customerId, branchId);
        }

        private static byte[] GenerateRandomIv()
        {
            byte[] iv = new byte[IvSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return iv;
        }
    }
}
