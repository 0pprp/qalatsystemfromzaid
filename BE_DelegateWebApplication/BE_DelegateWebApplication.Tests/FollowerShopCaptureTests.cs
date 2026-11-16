using BE_DelegateWebApplication.Controllers;
using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class FollowerShopCaptureTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 }, null)] // %PDF
    [InlineData(new byte[] { }, null)]
    public void Photo_type_comes_from_magic_bytes(byte[] bytes, string? expected) =>
        Assert.Equal(expected, FollowerShopCaptureController.DetectImageType(bytes));

    [Theory]
    [InlineData(33.3152, 44.3661, true)]   // Baghdad
    [InlineData(30.5085, 47.7804, true)]   // Basra
    [InlineData(0, 0, false)]
    [InlineData(1111, 1111, false)]
    [InlineData(44.3661, 33.3152, false)]  // swapped
    [InlineData(51.5, -0.12, false)]       // outside Iraq
    public void Location_must_be_inside_iraq(double lat, double lng, bool ok) =>
        Assert.Equal(ok, FollowerShopCaptureService.IsValidIraqPoint(lat, lng));
}
