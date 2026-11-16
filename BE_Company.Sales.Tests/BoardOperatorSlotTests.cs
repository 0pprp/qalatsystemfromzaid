using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>«تقييم لجنة المراقبة» (head, both committee members) vs «تقييم المحاسب الرئيسي» (accountant).</summary>
public sealed class BoardOperatorSlotTests
{
    [Theory]
    [InlineData(null, "head")]
    [InlineData("", "head")]
    [InlineData("head", "head")]
    [InlineData("assistant", "head")] // legacy committee-assistant clients write the shared committee score
    [InlineData("Accountant", "accountant")]
    [InlineData(" accountant ", "accountant")]
    public void Normalize_MapsSlots(string? input, string expected) =>
        Assert.Equal(expected, DirectorDelegateFollowupService.NormalizeBoardOperatorSlot(input));

    [Fact]
    public void CommitteeReads_IncludeLegacyAssistantRows() =>
        Assert.Equal(new[] { "head", "assistant" }, DirectorDelegateFollowupService.BoardOperatorReadSlots("head"));

    [Fact]
    public void AccountantReads_OnlyAccountantRows() =>
        Assert.Equal(new[] { "accountant" }, DirectorDelegateFollowupService.BoardOperatorReadSlots("accountant"));
}
