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
    public void ValidateReady_Fails_WhenRequiredDocMissing()
    {
        var docs = LegalFollowupCompletionRules.RequiredDocumentTypes
            .Where(t => t != "NationalIdFront")
            .ToArray();
        var err = LegalFollowupCompletionRules.ValidateReady(32.0, 44.3, docs);
        Assert.Contains("مستمسك", err!);
    }

    [Fact]
    public void ValidateReady_Passes_WithoutResidenceCertificate()
    {
        var err = LegalFollowupCompletionRules.ValidateReady(
            32.0289,
            44.3356,
            LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Null(err);
    }

    [Fact]
    public void ValidateReady_Passes_WithLocationAndAllRequiredDocs()
    {
        var docs = LegalFollowupCompletionRules.RequiredDocumentTypes
            .Concat(LegalFollowupCompletionRules.OptionalDocumentTypes)
            .ToArray();
        var err = LegalFollowupCompletionRules.ValidateReady(32.0289, 44.3356, docs);
        Assert.Null(err);
    }

    [Fact]
    public void RequiredTypes_AreFourImages_ResidenceOptional()
    {
        Assert.Equal(4, LegalFollowupCompletionRules.RequiredDocumentTypes.Length);
        Assert.Contains("NationalIdFront", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("NationalIdBack", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("ResidenceCardFront", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("ResidenceCardBack", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.DoesNotContain("ResidenceCertificate", LegalFollowupCompletionRules.RequiredDocumentTypes);
        Assert.Contains("ResidenceCertificate", LegalFollowupCompletionRules.OptionalDocumentTypes);
        Assert.True(LegalFollowupCompletionRules.IsKnownUploadType("ResidenceCertificate"));
    }
}
