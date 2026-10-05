using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public class LegalFollowupCompletionRulesTests
{
    [Fact]
    public void ValidateReady_Fails_WithoutLocation()
    {
        var err = LegalFollowupCompletionRules.ValidateReady(
            latitude: null,
            longitude: null,
            documentTypes: LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("موقع", err!);
    }

    [Fact]
    public void ValidateReady_Fails_WhenDocMissing()
    {
        var docs = LegalFollowupCompletionRules.RequiredDocumentTypes
            .Where(t => t != "ResidenceCertificate")
            .ToArray();
        var err = LegalFollowupCompletionRules.ValidateReady(32.0, 44.3, docs);
        Assert.Contains("مستمسك", err!);
    }

    [Fact]
    public void ValidateReady_Passes_WithLocationAndAllDocs()
    {
        var err = LegalFollowupCompletionRules.ValidateReady(
            32.0289,
            44.3356,
            LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Null(err);
    }

    [Fact]
    public void RequiredTypes_AreFiveImages()
    {
        Assert.Equal(5, LegalFollowupCompletionRules.RequiredDocumentTypes.Length);
        Assert.Contains("NationalIdFront", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("NationalIdBack", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("ResidenceCardFront", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("ResidenceCardBack", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("ResidenceCertificate", LegalFollowupCompletionRules.RequiredDocumentTypes);
    }
}
