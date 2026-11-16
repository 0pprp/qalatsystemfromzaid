using System.Text.Json.Nodes;
using BE_SalesEmployee.Controllers;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>Every gateway rater writes «تقييم لجنة المراقبة» (slot head).</summary>
public sealed class CommitteeRatingSlotTests
{
    [Theory]
    [InlineData("{\"score\":7,\"note\":null,\"slot\":\"assistant\"}")]
    [InlineData("{\"score\":7,\"note\":null,\"Slot\":\"assistant\"}")]
    [InlineData("{\"score\":7,\"note\":null}")]
    public void ForcesHeadSlot(string json)
    {
        var node = JsonNode.Parse(DirectorController.WithCommitteeSlot(json))!.AsObject();
        Assert.Equal("head", node["slot"]!.GetValue<string>());
        Assert.False(node.ContainsKey("Slot"));
        Assert.Equal(7, node["score"]!.GetValue<int>());
    }
}
