namespace BE_DelegateWebApplication.Services;

/// <summary>
/// Completion gate for follower legal follow-up: shop GPS + five mandatory ID images.
/// </summary>
public static class LegalFollowupCompletionRules
{
    public static readonly string[] RequiredDocumentTypes =
    [
        "NationalIdFront",
        "NationalIdBack",
        "ResidenceCardFront",
        "ResidenceCardBack",
        "ResidenceCertificate"
    ];

    public static bool HasLocation(double? latitude, double? longitude)
    {
        if (latitude is null || longitude is null) return false;
        if (double.IsNaN(latitude.Value) || double.IsNaN(longitude.Value)) return false;
        return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    }

    public static string TypeLabel(string? documentType) => documentType switch
    {
        "NationalIdFront" => "البطاقة الوطنية - أمامية",
        "NationalIdBack" => "البطاقة الوطنية - خلفية",
        "ResidenceCardFront" => "بطاقة السكن - أمامية",
        "ResidenceCardBack" => "بطاقة السكن - خلفية",
        "ResidenceCertificate" => "مضبطة تأييد سكن",
        "ResidenceCard" => "بطاقة السكن",
        _ => documentType ?? ""
    };

    public static bool IsRequiredType(string? documentType) =>
        !string.IsNullOrWhiteSpace(documentType)
        && RequiredDocumentTypes.Any(t => string.Equals(t, documentType.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsKnownUploadType(string? documentType) =>
        IsRequiredType(documentType)
        || string.Equals(documentType, "ResidenceCard", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns Arabic error message, or null when ready to submit.</summary>
    public static string? ValidateReady(
        double? latitude,
        double? longitude,
        IEnumerable<string?> documentTypes)
    {
        if (!HasLocation(latitude, longitude))
            return "يجب تحديد موقع عنوان المحل أولاً";

        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in documentTypes)
        {
            if (!string.IsNullOrWhiteSpace(t))
                present.Add(t.Trim());
        }

        // Legacy ResidenceCard counts toward either front or back if a side is missing.
        if (present.Contains("ResidenceCard"))
        {
            if (!present.Contains("ResidenceCardFront"))
                present.Add("ResidenceCardFront");
            if (!present.Contains("ResidenceCardBack"))
                present.Add("ResidenceCardBack");
        }

        var missing = RequiredDocumentTypes
            .Where(req => !present.Contains(req))
            .Select(TypeLabel)
            .ToList();

        if (missing.Count > 0)
            return "يجب رفع كل المستمسكات المطلوبة: " + string.Join("، ", missing);

        return null;
    }
}
