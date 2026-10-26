using System.Text.Json;
using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers;

/// <summary>
/// Gateway-provisioned متابع accounts (X-Sales-Gateway-Key only).
/// Used by المدير / المراقب via director follower-manage proxy.
/// </summary>
[Route("api/internal/follower-users")]
[ApiController]
public sealed class InternalFollowerUsersController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly IUsersRepository _users;
    private readonly IFollowerUserListsRepository _followerLists;
    private readonly IConfiguration _configuration;

    public InternalFollowerUsersController(
        IUsersRepository users,
        IFollowerUserListsRepository followerLists,
        IConfiguration configuration)
    {
        _users = users;
        _followerLists = followerLists;
        _configuration = configuration;
    }

    public sealed class FollowerUpsertBody
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? DisplayName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<int>? ListIds { get; set; }
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

    private static bool IsFollowerRow(UsersGetDTO? u) =>
        u != null && SalesRoles.IsFollower(u.UserType);

    [HttpGet("lists")]
    public async Task<IActionResult> Lists(CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        await _followerLists.EnsureSchemaAsync(ct);
        var lists = await _followerLists.GetAvailableListsAsync(ct);
        return Ok(lists);
    }

    [HttpGet]
    public async Task<IActionResult> ListFollowers(CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        var all = (await _users.Users_GetAll(null) ?? [])
            .Where(IsFollowerRow)
            .OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<object>(all.Count);
        foreach (var u in all)
        {
            var id = u.UserID ?? 0;
            var listIds = id > 0
                ? await _followerLists.GetAssignedListIdsAsync(id, ct)
                : Array.Empty<int>();
            result.Add(new
            {
                userId = id,
                userName = u.UserName,
                displayName = string.IsNullOrWhiteSpace(u.Address) ? u.UserName : u.Address,
                phoneNumber = u.PhoneNumber,
                userType = u.UserType,
                listIds
            });
        }

        return Ok(result);
    }

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetFollower(int userId, CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        if (userId <= 0)
        {
            return BadRequest(new { message = "معرّف المتابع غير صالح" });
        }

        var all = await _users.Users_GetAll(null) ?? [];
        var row = all.FirstOrDefault(u => u.UserID == userId);
        if (!IsFollowerRow(row))
        {
            return NotFound(new { message = "المتابع غير موجود" });
        }

        var listIds = await _followerLists.GetAssignedListIdsAsync(userId, ct);
        return Ok(new
        {
            userId,
            userName = row!.UserName,
            displayName = string.IsNullOrWhiteSpace(row.Address) ? row.UserName : row.Address,
            phoneNumber = row.PhoneNumber,
            userType = row.UserType,
            listIds
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FollowerUpsertBody? body, CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        body ??= new FollowerUpsertBody();
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
                new { message = "لا يوجد مستخدم نظام صالح لإنشاء المتابع على هذا الفرع" });
        }

        if (await _users.UserNameExistsAsync(userName))
        {
            return Conflict(new { message = "اسم المستخدم مستخدم مسبقاً" });
        }

        var display = string.IsNullOrWhiteSpace(body.DisplayName) ? userName : body.DisplayName.Trim();
        var listIds = (body.ListIds ?? []).Where(x => x > 0).Distinct().ToList();
        var created = await _users.Users_Create(new UsersPostDTO
        {
            UserName = userName,
            Password = password,
            UserType = SalesRoles.UserTypeFollower,
            Email = $"{userName}@follower.local",
            Address = display,
            PhoneNumber = body.PhoneNumber?.Trim(),
            UserCreateID = actorId,
            ListIdsJson = JsonSerializer.Serialize(listIds)
        });

        if (created?.UserID is not int newId || newId <= 0)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "تعذر إنشاء المتابع على الفرع" });
        }

        await _followerLists.EnsureSchemaAsync(ct);
        await _followerLists.ReplaceAssignmentsAsync(newId, listIds, ct);

        return Ok(new
        {
            ok = true,
            created = true,
            userId = newId,
            userName,
            displayName = display,
            listIds
        });
    }

    [HttpPut("{userId:int}")]
    public async Task<IActionResult> Update(int userId, [FromBody] FollowerUpsertBody? body, CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        if (userId <= 0)
        {
            return BadRequest(new { message = "معرّف المتابع غير صالح" });
        }

        body ??= new FollowerUpsertBody();
        var all = (await _users.Users_GetAll(null) ?? []).ToList();
        var existing = all.FirstOrDefault(u => u.UserID == userId);
        if (!IsFollowerRow(existing))
        {
            return NotFound(new { message = "المتابع غير موجود" });
        }

        var actorId = await ResolveActorUserIdAsync();
        if (actorId <= 0)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "لا يوجد مستخدم نظام صالح لتحديث المتابع على هذا الفرع" });
        }

        var userName = string.IsNullOrWhiteSpace(body.UserName)
            ? (existing!.UserName ?? "").Trim()
            : body.UserName.Trim();
        if (userName.Length < 3)
        {
            return BadRequest(new { message = "اسم المستخدم مطلوب" });
        }

        var nameTaken = all.Any(u =>
            u.UserID != userId &&
            string.Equals((u.UserName ?? "").Trim(), userName, StringComparison.OrdinalIgnoreCase));
        if (nameTaken)
        {
            return Conflict(new { message = "اسم المستخدم مستخدم مسبقاً" });
        }

        var display = string.IsNullOrWhiteSpace(body.DisplayName)
            ? (string.IsNullOrWhiteSpace(existing!.Address) ? userName : existing.Address!.Trim())
            : body.DisplayName.Trim();
        var password = string.IsNullOrWhiteSpace(body.Password) ? (existing!.Password ?? "") : body.Password;
        var listIds = (body.ListIds ?? []).Where(x => x > 0).Distinct().ToList();

        var updated = await _users.Users_Update(userId, new UsersPutDTO
        {
            UserID = userId,
            UserName = userName,
            Password = password,
            UserType = SalesRoles.UserTypeFollower,
            Email = existing!.Email ?? $"{userName}@follower.local",
            Address = display,
            PhoneNumber = body.PhoneNumber?.Trim() ?? existing.PhoneNumber,
            UserUpdateID = actorId,
            ListIdsJson = JsonSerializer.Serialize(listIds)
        });

        if (updated is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "تعذر تحديث المتابع على الفرع" });
        }

        await _followerLists.EnsureSchemaAsync(ct);
        await _followerLists.ReplaceAssignmentsAsync(userId, listIds, ct);

        return Ok(new
        {
            ok = true,
            updated = true,
            userId,
            userName,
            displayName = display,
            listIds
        });
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Delete(int userId, CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        if (userId <= 0)
        {
            return BadRequest(new { message = "معرّف المتابع غير صالح" });
        }

        var all = await _users.Users_GetAll(null) ?? [];
        var existing = all.FirstOrDefault(u => u.UserID == userId);
        if (!IsFollowerRow(existing))
        {
            return NotFound(new { message = "المتابع غير موجود" });
        }

        var actorId = await ResolveActorUserIdAsync();
        if (actorId <= 0)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "لا يوجد مستخدم نظام صالح لحذف المتابع على هذا الفرع" });
        }

        await _followerLists.ClearAssignmentsAsync(userId, ct);
        var deleted = await _users.Users_Delete(userId, actorId);
        if (deleted != true)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "تعذر حذف المتابع على الفرع" });
        }

        return Ok(new { ok = true, deleted = true, userId });
    }
}
