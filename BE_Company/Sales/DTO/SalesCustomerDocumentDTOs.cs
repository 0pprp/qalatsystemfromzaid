namespace BE_Company.Sales.DTO
{
    public static class SalesCustomerDocumentTypes
    {
        public const string NationalIdFront = "NationalIdFront";
        public const string NationalIdBack = "NationalIdBack";
        public const string ResidenceCard = "ResidenceCard";
        public const string ResidenceCertificate = "ResidenceCertificate";

        public static readonly string[] All =
        [
            NationalIdFront,
            NationalIdBack,
            ResidenceCard,
            ResidenceCertificate
        ];

        public static string Label(string? type) => type switch
        {
            NationalIdFront => "البطاقة الوطنية - أمامية",
            NationalIdBack => "البطاقة الوطنية - خلفية",
            ResidenceCard => "بطاقة السكن",
            ResidenceCertificate => "تأييد السكن",
            _ => type ?? ""
        };

        public static bool IsKnown(string? type) =>
            !string.IsNullOrWhiteSpace(type) &&
            All.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

        public static string Normalize(string type) =>
            All.First(t => string.Equals(t, type.Trim(), StringComparison.OrdinalIgnoreCase));
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
