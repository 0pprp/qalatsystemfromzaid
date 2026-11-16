using BE_CustomerApp.Domain.Orders;
using BE_CustomerApp.Domain.Users;

namespace BE_CustomerApp.Tests;

public class IraqiPhoneTests
{
    [Theory]
    [InlineData("07701234567", "07701234567")]
    [InlineData("7701234567", "07701234567")]
    [InlineData("+9647701234567", "07701234567")]
    [InlineData("009647801234567", "07801234567")]
    [InlineData("964 750 123 4567", "07501234567")]
    [InlineData("٠٧٧٠١٢٣٤٥٦٧", "07701234567")]
    [InlineData("0790-123-4567", "07901234567")]
    public void Normalizes_valid_iraqi_mobiles(string input, string expected) => Assert.Equal(expected, IraqiPhone.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0770123456")]      // too short
    [InlineData("077012345678")]    // too long
    [InlineData("07101234567")]     // not a mobile prefix
    [InlineData("+9617701234567")]  // Lebanon
    [InlineData("0121234567")]      // landline
    public void Rejects_non_iraqi_or_invalid(string? input) => Assert.Null(IraqiPhone.Normalize(input));

    [Fact]
    public void Masks_for_logs() => Assert.Equal("0770****567", IraqiPhone.Mask("07701234567"));
}

public class NameValidationTests
{
    [Fact]
    public void Requires_three_single_word_arabic_names()
    {
        var errors = UserService.ValidateName(new PersonName("Ali", "", "محمد", null));
        Assert.Contains("firstName", errors.Keys);
        Assert.Contains("fatherName", errors.Keys);
        Assert.DoesNotContain("grandfatherName", errors.Keys);
    }

    [Theory]
    [InlineData("محمد جاسم علي", "محمد", "جاسم", "علي", null)]
    [InlineData(" عبد الله  أبو بكر حسين آل محمد ", "عبدالله", "أبوبكر", "حسين", "آل محمد")]
    public void Full_name_in_one_field_is_split_into_parts(string full, string first, string father, string grandfather, string? surname)
    {
        var n = PersonName.FromFullName(full)!;
        Assert.Equal(new PersonName(first, father, grandfather, surname), n);
    }

    [Fact]
    public void Full_name_needs_three_names() => Assert.Null(PersonName.FromFullName("محمد علي"));

    [Fact]
    public void Compound_first_name_is_joined_into_one_token()
    {
        var n = new PersonName("عبد الله", "حسين", "علي", "آل  محمد");
        Assert.Empty(UserService.ValidateName(n));
        var clean = UserService.Clean(n);
        Assert.Equal("عبدالله", clean.FirstName);
        Assert.Equal("آل محمد", clean.Surname);
    }
}

public class OrderStatusMappingTests
{
    [Theory]
    [InlineData("New", false, OrderStatus.Transferred)]
    [InlineData("Assigned", false, OrderStatus.Transferred)]
    [InlineData("PreparedForSale", false, OrderStatus.Transferred)]
    [InlineData("Inspected", false, OrderStatus.FieldInspection)]
    [InlineData("ConvertedToSale", false, OrderStatus.FieldInspection)]
    [InlineData("Completed", false, OrderStatus.Sold)]
    [InlineData("Rejected", false, OrderStatus.NotSold)]
    [InlineData("Assigned", true, OrderStatus.NotSold)]
    [InlineData("SomethingNew", false, OrderStatus.Transferred)]
    public void Maps_branch_status(string branch, bool deleted, string expected) =>
        Assert.Equal(expected, OrderStatus.FromBranch(true, branch, deleted));

    [Fact]
    public void Not_found_leaves_order_unchanged() => Assert.Null(OrderStatus.FromBranch(false, null, false));

    [Fact]
    public void Every_status_has_an_arabic_label() =>
        Assert.All(OrderStatus.All, s => Assert.NotEqual(s, OrderStatus.Label(s)));
}

public class SalesNotesTests
{
    [Fact]
    public void Sales_notes_are_short_plain_text_without_links()
    {
        var order = new OrderRow
        {
            OrderNumber = "QO-2611-000001", PurchaseType = PurchaseTypes.DailyInstallments,
            ShopName = "أسواق النور", ShopBusinessType = "مواد غذائية", OwnsShop = true, SecondaryPhone = "07801234567",
            Latitude = 32.1m, Longitude = 44.3m, ShopPhotoUrl = "media/shops/2611/a.jpg", Notes = "اتصلوا بعد العصر",
        };

        var notes = OrderService.BuildSalesNotes(order, null);

        // Installments are the norm, so only the shop, a second phone and the agent's notes are written.
        Assert.Equal(string.Join('\n', "المحل: أسواق النور (مواد غذائية)", "هاتف ثانٍ: 07801234567", "اتصلوا بعد العصر"), notes);
        Assert.StartsWith("نوع الشراء: نقد", OrderService.BuildSalesNotes(new OrderRow { PurchaseType = PurchaseTypes.Cash }, null));
        Assert.DoesNotContain("http", notes);
        Assert.DoesNotContain("QO-", notes);

        order.Notes = new string('م', 900);
        Assert.True(OrderService.BuildSalesNotes(order, "ملاحظة").Length <= 750);
    }

    [Theory]
    [InlineData("Ali.Hassan", "ali.hassan")]
    [InlineData(" حَيـدر٧ ", "حيدر7")]
    [InlineData("علي۱۲", "علي12")]
    public void Usernames_ignore_case_diacritics_tatweel_and_digit_style(string input, string expected) =>
        Assert.Equal(expected, BE_CustomerApp.Domain.Users.AuthService.NormalizeUsername(input));

    [Fact]
    public void Password_hash_verifies_only_the_same_password()
    {
        var hash = BE_CustomerApp.Auth.PasswordHasher.Hash("s3cret!");
        Assert.StartsWith("pbkdf2$", hash);
        Assert.True(BE_CustomerApp.Auth.PasswordHasher.Verify("s3cret!", hash));
        Assert.False(BE_CustomerApp.Auth.PasswordHasher.Verify("s3cret", hash));
        Assert.False(BE_CustomerApp.Auth.PasswordHasher.Verify("s3cret!", null));
        Assert.False(BE_CustomerApp.Auth.PasswordHasher.Verify("s3cret!", "garbage"));
        Assert.NotEqual(hash, BE_CustomerApp.Auth.PasswordHasher.Hash("s3cret!")); // salted
        // Any non-empty password is allowed.
        Assert.Null(BE_CustomerApp.Auth.PasswordHasher.Validate("1"));
        Assert.Null(BE_CustomerApp.Auth.PasswordHasher.Validate("كلمة"));
        Assert.NotNull(BE_CustomerApp.Auth.PasswordHasher.Validate(""));
        Assert.NotNull(BE_CustomerApp.Auth.PasswordHasher.Validate(null));
    }

    [Fact]
    public void Work_date_is_the_iraq_calendar_day()
    {
        // 22:30 UTC on Nov 3 is 01:30 on Nov 4 in Baghdad.
        Assert.Equal(new DateOnly(2026, 11, 4), BE_CustomerApp.Domain.Work.IraqTime.DateOf(new DateTime(2026, 11, 3, 22, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 11, 3, 21, 0, 0, DateTimeKind.Utc), BE_CustomerApp.Domain.Work.IraqTime.DayStartUtc(new DateOnly(2026, 11, 4)));
    }

    [Fact]
    public void Stay_starts_at_the_oldest_point_of_the_unbroken_run_near_the_latest_point()
    {
        var t = new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
        (decimal, decimal, DateTime)[] newestFirst =
        [
            (32.02010m, 44.31990m, t.AddMinutes(70)),
            (32.01990m, 44.32030m, t.AddMinutes(40)),
            (32.02000m, 44.32000m, t),
            (32.01000m, 44.31000m, t.AddMinutes(-10)), // ~1.4 km away: the stay began after this
            (32.02000m, 44.32000m, t.AddMinutes(-30)),
        ];
        Assert.Equal(t, BE_CustomerApp.Domain.Work.WorkAlertService.StayStart(newestFirst, 150));
        Assert.InRange(BE_CustomerApp.Domain.Work.WorkAlertService.DistanceMeters(32.0200m, 44.3200m, 32.0209m, 44.3200m), 95, 105);
    }
}
