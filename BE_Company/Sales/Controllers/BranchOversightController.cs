using System.Security.Cryptography;
using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using BE_Company.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers
{
    /// <summary>
    /// «ادارة الفرع» on the central dashboard (المدير / مسؤول المراقبين / لجنة المراقبة).
    /// Same data as تطبيق إدارة الفرع, reached only through the trusted sales gateway
    /// (X-Sales-Gateway-Key). Decisions are attributed to a non-login branch user row
    /// that names the oversight actor, so سجل القرارات shows who decided.
    /// </summary>
    [Authorize]
    [Route("api/branch-oversight")]
    [ApiController]
    public sealed class BranchOversightController : ControllerBase
    {
        public const string OversightEmailDomain = "@oversight.local";

        private static readonly HashSet<string> AllowedDecisionTypes = new(StringComparer.Ordinal)
        {
            "متواصل", "قانونية", "وهمي"
        };

        private readonly SalesDevelopmentGuard _guard;
        private readonly IBranchManagerNewSalesService _newSales;
        private readonly IBmCustomerDeclineService _customerDeclines;
        private readonly ICustomerDecisionsRepository _decisions;
        private readonly ILegalConversionRequestService _legalConversion;
        private readonly IUsersRepository _users;

        public BranchOversightController(
            SalesDevelopmentGuard guard,
            IBranchManagerNewSalesService newSales,
            IBmCustomerDeclineService customerDeclines,
            ICustomerDecisionsRepository decisions,
            ILegalConversionRequestService legalConversion,
            IUsersRepository users)
        {
            _guard = guard;
            _newSales = newSales;
            _customerDeclines = customerDeclines;
            _decisions = decisions;
            _legalConversion = legalConversion;
            _users = users;
        }

        /// <summary>Gateway key only (never a branch JWT, never a sales-filter identity).</summary>
        public static bool IsTrustedGatewayCaller(string? authSource, string? userType) =>
            string.Equals(authSource, SalesGatewayKeyHandler.AuthSourceGateway, StringComparison.Ordinal)
            && !string.Equals(userType, SalesRoles.UserTypeSalesFilterEmployee, StringComparison.Ordinal);

        private async Task<IActionResult?> GateAsync(CancellationToken ct)
        {
            var (ok, reason) = await _guard.CanRunSalesModuleAsync(ct);
            if (!ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
            }

            var authSource = User.FindFirst(SalesGatewayKeyHandler.AuthSourceClaim)?.Value;
            var userType = User.FindFirst("UserType")?.Value;
            if (!IsTrustedGatewayCaller(authSource, userType))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه الواجهة لبوابة الإدارة فقط" });
            }

            return null;
        }

        [HttpGet("new-sales")]
        public async Task<IActionResult> NewSales([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _newSales.ListAsync(unreadOnly, ct));
        }

        [HttpGet("new-sales/{saleId:int}")]
        public async Task<IActionResult> NewSale(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var row = await _newSales.GetAsync(saleId, ct);
            return row == null ? NotFound(new { message = "المبيع غير موجود" }) : Ok(row);
        }

        [HttpGet("customers/declining")]
        public async Task<IActionResult> Declining([FromQuery] bool refresh = true, CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            if (refresh)
            {
                await _customerDeclines.RefreshAsync(ct);
            }

            return Ok(await _customerDeclines.ListDecliningAsync(ct));
        }

        public sealed class DecideBody
        {
            public int CustomerId { get; set; }
            public string? DecisionType { get; set; }
            public string? Note { get; set; }
            public string? ActorUserId { get; set; }
            public string? ActorName { get; set; }
            public string? ActorRole { get; set; }
        }

        /// <summary>أسماء قانونية — same decision as مدير فرع (قانونية → pending accountant approval).</summary>
        [HttpPost("decide")]
        public async Task<IActionResult> Decide([FromBody] DecideBody? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            var type = (body?.DecisionType ?? "").Trim();
            if (body is null || body.CustomerId <= 0 || !AllowedDecisionTypes.Contains(type))
            {
                return BadRequest(new { message = "يجب تحديد الزبون ونوع القرار" });
            }

            var actorName = (body.ActorName ?? "").Trim();
            var actorRole = (body.ActorRole ?? "").Trim();
            if (actorName.Length == 0 || actorRole.Length == 0)
            {
                return BadRequest(new { message = "هوية متخذ القرار مطلوبة" });
            }

            var userId = await ResolveOversightUserIdAsync(body.ActorUserId, actorName, actorRole);
            if (userId <= 0)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "تعذر تجهيز حساب متخذ القرار على الفرع" });
            }

            var note = string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim();
            try
            {
                if (string.Equals(type, "قانونية", StringComparison.Ordinal))
                {
                    return Ok(await _legalConversion.SubmitFromBranchManagerAsync(body.CustomerId, userId, note, ct));
                }

                return Ok(await _decisions.Customers_PostWeekDecision(body.CustomerId, userId, type, note));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        public static string OversightEmail(string? actorUserId, string actorName, string actorRole)
        {
            var key = (actorUserId ?? "").Trim();
            if (key.Length == 0 || key == "0" || key.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            {
                var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{actorRole}|{actorName}"));
                key = "n" + Convert.ToHexString(bytes)[..16].ToLowerInvariant();
            }

            return $"oversight-{key.ToLowerInvariant()}{OversightEmailDomain}";
        }

        /// <summary>
        /// CustomerWeekDecisions.UserID has an FK to Users — find or create a non-login row
        /// (random password) whose name/type show the oversight actor in سجل القرارات.
        /// </summary>
        private async Task<int> ResolveOversightUserIdAsync(string? actorUserId, string actorName, string actorRole)
        {
            var email = OversightEmail(actorUserId, actorName, actorRole);
            var all = (await _users.Users_GetAll(null) ?? []).ToList();
            var existing = all.FirstOrDefault(u =>
                string.Equals((u.Email ?? "").Trim(), email, StringComparison.OrdinalIgnoreCase));
            if (existing?.UserID is int existingId and > 0)
            {
                return existingId;
            }

            var creatorId = all
                .Where(u => u.UserID is > 0)
                .OrderBy(u => u.UserType switch
                {
                    "محاسب رئيسي" => 0,
                    "مدير فرع" => 1,
                    "مدير مبيعات" => 2,
                    _ => 3
                })
                .Select(u => u.UserID!.Value)
                .FirstOrDefault();
            if (creatorId <= 0)
            {
                return 0;
            }

            var baseName = $"{actorName} - {actorRole}";
            var userName = baseName;
            for (var i = 2; await _users.UserNameExistsAsync(userName) && i < 50; i++)
            {
                userName = $"{baseName} {i}";
            }

            var created = await _users.Users_Create(new UsersPostDTO
            {
                UserName = userName,
                Email = email,
                Password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                Address = "حساب إدارة مركزية (بدون دخول) — قرارات ادارة الفرع",
                UserType = actorRole,
                UserCreateID = creatorId
            });
            return created?.UserID ?? 0;
        }
    }
}
