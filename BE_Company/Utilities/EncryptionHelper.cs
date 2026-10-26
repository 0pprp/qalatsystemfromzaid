using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BE_Company.Utilities
{
    /// <summary>
    /// Authenticated portal tokens (AES-256-GCM). Legacy CBC tokens are rejected with no fallback.
    /// Payload: customerId, branchId, purpose, exp (unix), jti.
    /// </summary>
    public static class PortalTokenCrypto
    {
        public const string PurposeCustomerPortal = "customer-portal";
        private const byte Version = 1;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        public sealed record PortalTokenPayload(
            int CustomerId,
            int BranchId,
            string Purpose,
            long ExpUnix,
            string Jti);

        public static string Create(
            int customerId,
            int branchId,
            string purpose,
            TimeSpan lifetime,
            string base64Key,
            Func<string>? jtiFactory = null)
        {
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (branchId <= 0) throw new ArgumentOutOfRangeException(nameof(branchId));
            if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("purpose required", nameof(purpose));
            if (lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));

            var payload = new PortalTokenPayload(
                customerId,
                branchId,
                purpose.Trim(),
                DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds(),
                jtiFactory?.Invoke() ?? Guid.NewGuid().ToString("N"));

            var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            var key = DecodeKey(base64Key);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagSize];

            using (var gcm = new AesGcm(key, TagSize))
            {
                gcm.Encrypt(nonce, plain, cipher, tag);
            }

            // v | nonce | tag | cipher
            var packed = new byte[1 + NonceSize + TagSize + cipher.Length];
            packed[0] = Version;
            Buffer.BlockCopy(nonce, 0, packed, 1, NonceSize);
            Buffer.BlockCopy(tag, 0, packed, 1 + NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, packed, 1 + NonceSize + TagSize, cipher.Length);
            return ToUrlSafeBase64(packed);
        }

        public static PortalTokenPayload? TryValidate(string token, string base64Key, DateTimeOffset? nowUtc = null)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(base64Key))
            {
                return null;
            }

            try
            {
                var packed = FromUrlSafeBase64(token);
                if (packed.Length < 1 + NonceSize + TagSize + 1 || packed[0] != Version)
                {
                    return null;
                }

                var key = DecodeKey(base64Key);
                var nonce = packed.AsSpan(1, NonceSize);
                var tag = packed.AsSpan(1 + NonceSize, TagSize);
                var cipher = packed.AsSpan(1 + NonceSize + TagSize);
                var plain = new byte[cipher.Length];

                using (var gcm = new AesGcm(key, TagSize))
                {
                    gcm.Decrypt(nonce, cipher, tag, plain);
                }

                var payload = JsonSerializer.Deserialize<PortalTokenPayload>(Encoding.UTF8.GetString(plain));
                if (payload is null || payload.CustomerId <= 0 || payload.BranchId <= 0)
                {
                    return null;
                }

                if (!string.Equals(payload.Purpose, PurposeCustomerPortal, StringComparison.Ordinal))
                {
                    return null;
                }

                var now = (nowUtc ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
                if (payload.ExpUnix < now)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(payload.Jti))
                {
                    return null;
                }

                return payload;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] DecodeKey(string base64Key)
        {
            var key = Convert.FromBase64String(base64Key);
            if (key.Length != 32)
            {
                throw new ArgumentException("Portal encryption key must be 32 bytes (Base64).", nameof(base64Key));
            }

            return key;
        }

        private static string ToUrlSafeBase64(byte[] data) =>
            Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static byte[] FromUrlSafeBase64(string text)
        {
            var base64 = text.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            return Convert.FromBase64String(base64);
        }
    }

    /// <summary>
    /// Legacy CBC helper kept only for generating/detecting old tokens during migration docs/tests.
    /// Production portal validation must not call Decrypt for authorization.
    /// </summary>
    public static class EncryptionHelper
    {
        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int IvSize = 16;

        [Obsolete("Legacy CBC portal tokens are rejected. Use PortalTokenCrypto (AES-GCM).")]
        public static string Encrypt(string plainText, string base64Key)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException(nameof(plainText));
            if (string.IsNullOrEmpty(base64Key))
                throw new ArgumentNullException(nameof(base64Key));

            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

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

            byte[] result = new byte[iv.Length + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, iv.Length, cipherBytes.Length);

            return Convert.ToBase64String(result)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        [Obsolete("Do not authorize with CBC decrypt. Use PortalTokenCrypto.TryValidate.")]
        public static string? Decrypt(string cipherText, string base64Key)
        {
            if (string.IsNullOrEmpty(cipherText) || string.IsNullOrEmpty(base64Key))
                return null;

            try
            {
                string base64 = cipherText.Replace('-', '+').Replace('_', '/');
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
                return null;
            }
        }

        [Obsolete("Use PortalTokenCrypto.Create.")]
        public static string CreateCustomerToken(int customerId, string base64Key, int? branchId = null)
        {
            string plainText = branchId.HasValue
                ? $"{customerId}|{branchId.Value}"
                : $"{customerId}";
#pragma warning disable CS0618
            return Encrypt(plainText, base64Key);
#pragma warning restore CS0618
        }

        [Obsolete("Legacy tokens are not accepted by PortalController.")]
        public static int? ValidateAndGetCustomerId(string token, string base64Key) => null;

        [Obsolete("Legacy tokens are not accepted by PortalController.")]
        public static (int customerId, int? branchId)? ValidateAndGetCustomerIdWithBranch(string token, string base64Key) => null;
    }
}
