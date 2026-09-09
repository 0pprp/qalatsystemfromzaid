using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.Services
{
    /// <summary>
    /// Labels + URL helpers for follower customer media (classic Images + SalesCustomerDocuments).
    /// </summary>
    public static class FollowerCustomerMedia
    {
        public static string DocumentTypeLabel(string? type) => type switch
        {
            "NationalIdFront" => "البطاقة الوطنية - أمامية",
            "NationalIdBack" => "البطاقة الوطنية - خلفية",
            "ResidenceCardFront" => "بطاقة السكن - أمامية",
            "ResidenceCardBack" => "بطاقة السكن - خلفية",
            "ResidenceCard" => "بطاقة السكن - قديمة",
            "ResidenceCertificate" => "تأييد السكن",
            "Customer" => "صورة الزبون",
            "Shop" => "صورة المحل",
            _ => string.IsNullOrWhiteSpace(type) ? "مستمسك" : type.Trim()
        };

        public static string LeafFileName(string? fileNameOrKey)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrKey))
            {
                return string.Empty;
            }

            var name = fileNameOrKey.Trim().Replace('\\', '/');
            return name.Split('/').LastOrDefault() ?? name;
        }

        /// <summary>
        /// Prefer public /Images/{leaf} only when the file exists under wwwroot/Images.
        /// </summary>
        public static string? TryPublicImagesUrl(string? imagesBaseUrl, string? webRootPath, string? fileNameOrKey)
        {
            var leaf = LeafFileName(fileNameOrKey);
            if (string.IsNullOrWhiteSpace(leaf) || string.IsNullOrWhiteSpace(webRootPath))
            {
                return null;
            }

            var physical = Path.Combine(webRootPath, "Images", leaf);
            if (!File.Exists(physical))
            {
                return null;
            }

            return FollowerAuthorization.BuildImageUrl(imagesBaseUrl, leaf);
        }

        public static string BuildDocumentFileApiUrl(
            string apiRoot,
            int customerId,
            int documentId,
            string asyncId,
            int listId) =>
            $"{apiRoot.TrimEnd('/')}/Followers/Customers/{customerId}/documents/{documentId}/file"
            + $"?asyncId={Uri.EscapeDataString(asyncId ?? string.Empty)}&listId={listId}";

        public static string BuildShopImageApiUrl(string apiRoot, int customerId, string asyncId, int listId) =>
            $"{apiRoot.TrimEnd('/')}/Followers/Customers/{customerId}/shop-image"
            + $"?asyncId={Uri.EscapeDataString(asyncId ?? string.Empty)}&listId={listId}";

        public static string ResolveImageContentType(string? stored, string fileNameOrPath)
        {
            var ext = Path.GetExtension(fileNameOrPath);
            var guessed = ext.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "image/jpeg"
            };
            if (string.IsNullOrWhiteSpace(stored)
                || stored.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                || stored.Equals("binary/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return guessed;
            }

            return stored;
        }
    }
}
