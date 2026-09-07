namespace BE_Company.Sales.DTO
{
    public static class SalesCustomerDocumentTypes
    {
        public const string NationalIdFront = "NationalIdFront";
        public const string NationalIdBack = "NationalIdBack";
        public const string ResidenceCardFront = "ResidenceCardFront";
        public const string ResidenceCardBack = "ResidenceCardBack";
        public const string ResidenceCertificate = "ResidenceCertificate";
        public const string ResidenceCard = "ResidenceCard";

        public static readonly string[] All =
        [
            NationalIdFront,
            NationalIdBack,
            ResidenceCardFront,
            ResidenceCardBack,
            ResidenceCertificate
        ];

        public static readonly string[] Legacy =
        [
            ResidenceCard
        ];

        private static IEnumerable<string> Known => All.Concat(Legacy);

        public static string Label(string? type) => type switch
        {
            NationalIdFront => "البطاقة الوطنية - أمامية",
            NationalIdBack => "البطاقة الوطنية - خلفية",
            ResidenceCardFront => "بطاقة السكن - أمامية",
            ResidenceCardBack => "بطاقة السكن - خلفية",
            ResidenceCard => "بطاقة السكن - قديمة",
            ResidenceCertificate => "تأييد السكن",
            _ => type ?? ""
        };

        public static bool IsKnown(string? type) =>
            !string.IsNullOrWhiteSpace(type) &&
            Known.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

        public static string Normalize(string type) =>
            Known.First(t => string.Equals(t, type.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public sealed class SalesCustomerDocumentDTO
    {
        public int Id { get; set; }
        public int? SaleId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string FileKey { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public string? FileUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
