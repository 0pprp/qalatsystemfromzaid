using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers;

/// <summary>
/// Gateway-provisioned مدير فرع accounts for تطبيق إدارة الفرع (X-Sales-Gateway-Key only).
/// </summary>
[Route("api/internal/branch-manager-users")]
[ApiController]
public sealed class InternalBranchManagerUsersController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly IUsersRepository _users;
    private readonly IConfiguration _configuration;

    public InternalBranchManagerUsersController(IUsersRepository users, IConfiguration configuration)
    {
        _users = users;
        _configuration = configuration;
    }

    public sealed class UpsertBody
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? DisplayName { get; set; }
    }

    private bool HasValidKey()
    {
        if (!Request.Headers.TryGetValue(GatewayKeyHeader, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expected = _configuration["SalesEmployee:GatewayKey"]
                       ?? _configuration["InternalApiKey"];
        return !string.IsNullOrWhiteSpace(expected)
               && string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }

    /// <summary>
    /// Users_Create / Users_Update log Activities with a FK to Users.UserID —
    /// UserCreateID=0 causes SQL 547 and a 500 even after the row may insert.
    /// </summary>
    private async Task<int> ResolveActorUserIdAsync()
    {
        var all = (await _users.Users_GetAll(null) ?? []).ToList();
        foreach (var preferredType in new[]
                 {
                     "محاسب رئيسي", "مدير فرع", "مدير مبيعات", "محاسب فرعي"
                 })
        {
            var hit = all.FirstOrDefault(u =>
                u.UserID is > 0 &&
                string.Equals(u.UserType, preferredType, StringComparison.Ordinal));
            if (hit?.UserID is int id and > 0)
            {
                return id;
            }
        }

        return all.Select(u => u.UserID).OfType<int>().FirstOrDefault(id => id > 0);
    }

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert([FromBody] UpsertBody? body, CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        body ??= new UpsertBody();
        var userName = (body.UserName ?? "").Trim();
        var password = body.Password ?? "";
        if (userName.Length < 3 || password.Length < 4)
        {
            return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
        }

        var actorId = await ResolveActorUserIdAsync();
        if (actorId <= 0)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "لا يوجد مستخدم نظام صالح لإنشاء حساب إدارة الفرع على هذا الفرع" });
        }

        var display = string.IsNullOrWhiteSpace(body.DisplayName) ? userName : body.DisplayName.Trim();

        var all = await _users.Users_GetAll(userName) ?? [];
        var existing = all.FirstOrDefault(u =>
            string.Equals((u.UserName ?? "").Trim(), userName, StringComparison.OrdinalIgnoreCase));

        if (existing?.UserID is int uid and > 0)
        {
            var updated = await _users.Users_Update(uid, new UsersPutDTO
            {
                UserID = uid,
                UserName = userName,
                Password = password,
                UserType = SalesRoles.UserTypeBranchManager,
                Email = existing.Email ?? $"{userName}@branch-manager.local",
                Address = display,
                PhoneNumber = existing.PhoneNumber,
                UserUpdateID = actorId
            });
            return updated is null
                ? StatusCode(StatusCodes.Status500InternalServerError, new { message = "تعذر تحديث الحساب على الفرع" })
                : Ok(new { ok = true, updated = true, userId = updated.UserID, displayName = display });
        }

        var created = await _users.Users_Create(new UsersPostDTO
        {
            UserName = userName,
            Password = password,
            UserType = SalesRoles.UserTypeBranchManager,
            Email = $"{userName}@branch-manager.local",
            Address = display,
            UserCreateID = actorId
        });

        if (created is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "تعذر إنشاء حساب مدير الفرع على الفرع" });
        }

        return Ok(new { ok = true, created = true, userId = created.UserID, displayName = display });
    }
}
