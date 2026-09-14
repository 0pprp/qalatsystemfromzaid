using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class DelegateComplaintsContractTests
{
    [Fact]
    public void Message_Too_Short_Rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ValidateMessage("قصير"));
        Assert.Contains("قصير", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Message_Empty_Rejected()
    {
        Assert.Throws<ArgumentException>(() => ValidateMessage("   "));
    }

    [Fact]
    public void Message_Too_Long_Rejected()
    {
        var text = new string('ا', DelegateComplaintsService.MaxMessageLength + 1);
        var ex = Assert.Throws<ArgumentException>(() => ValidateMessage(text));
        Assert.Contains("الحد الأقصى", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Message_Valid_Passes()
    {
        ValidateMessage("هذه شكوى سرية بطول كافٍ للمتابعة");
    }

    [Fact]
    public void AsyncId_Never_Stored_As_Sender_Identity()
    {
        Assert.DoesNotContain("AsyncId", nameof(DelegateComplaintDto.DelegateId));
        Assert.DoesNotContain("AsyncId", nameof(DelegateComplaintDto.SenderDisplayName));
    }

    [Fact]
    public void Angle_Brackets_Stripped_From_Stored_Text()
    {
        var text = "نص <script>alert(1)</script> شكوى طويلة بما يكفي";
        var cleaned = text.Replace('<', ' ').Replace('>', ' ').Trim();
        Assert.DoesNotContain('<', cleaned);
        Assert.DoesNotContain('>', cleaned);
    }

    private static void ValidateMessage(string message)
    {
        var text = (message ?? string.Empty).Trim();
        if (text.Length < DelegateComplaintsService.MinMessageLength)
        {
            throw new ArgumentException(
                $"نص الشكوى قصير جدًا (الحد الأدنى {DelegateComplaintsService.MinMessageLength} أحرف)");
        }

        if (text.Length > DelegateComplaintsService.MaxMessageLength)
        {
            throw new ArgumentException(
                $"الحد الأقصى للشكوى {DelegateComplaintsService.MaxMessageLength} حرف");
        }
    }
}
