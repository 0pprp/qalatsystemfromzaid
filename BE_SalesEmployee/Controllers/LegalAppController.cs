using BE_SalesEmployee.Legal;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// The legal app (مدير القانونية + المدير): debtors received from the company move along
/// الاسماء المضافة مؤخرا → ارشيف القانونية → الدعوى القضائية → القرارات → التنفيذ,
/// plus lawyers with their wallets and the two registers.
/// </summary>
[Route("api/legal-app")]
[ApiController]
[Authorize(Policy = SalesPolicies.LegalApp)]
[LegalErrors]
public sealed class LegalAppController : ControllerBase
{
    private const long MaxImageBytes = 8 * 1024 * 1024;

    private readonly LegalStore _store;
    private readonly LegalService _service;
    private readonly IWebHostEnvironment _env;

    public LegalAppController(LegalStore store, LegalService service, IWebHostEnvironment env)
    {
        _store = store;
        _service = service;
        _env = env;
    }

    private string Actor => User.FindFirst("UserName")?.Value ?? "";

    // —— المسار ——

    [HttpGet("board")]
    public async Task<IActionResult> Board(CancellationToken ct) =>
        Ok(new { stages = await _store.StageCountsAsync(ct), settings = await _store.SettingsAsync(ct) });

    [HttpGet("cases")]
    public async Task<IActionResult> Cases([FromQuery] string? stage, [FromQuery] string? q, [FromQuery] int? lawyerId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(stage) && !LegalStages.IsKnown(stage))
            return BadRequest(new { message = "مرحلة غير معروفة" });
        return Ok(await _store.CasesAsync(stage, q, lawyerId, ct));
    }

    [HttpGet("cases/{id:int}")]
    public async Task<IActionResult> Case(int id, CancellationToken ct)
    {
        var legalCase = await _store.CaseAsync(id, ct);
        if (legalCase is null)
            return NotFound(new { message = "الملف غير موجود" });
        int personId = legalCase.personId;
        var accounts = await _store.AccountsAsync(personId, ct);
        var (live, stale) = await _service.LiveAccountsAsync(accounts, Actor, ct);
        var extras = await _store.CaseExtrasAsync(id, personId, ct);
        var penaltiesDue = await _store.PenaltiesDueAsync(personId, ct);
        var remaining = live.Sum(a => (long)((dynamic)a).remaining);
        return Ok(new
        {
            legalCase,
            person = await _store.PersonAsync(personId, ct),
            accounts = live,
            staleCities = stale,
            extras,
            totals = new { remaining, penaltiesDue, totalDue = remaining + penaltiesDue },
        });
    }

    public sealed class MoveBody
    {
        public string? To { get; set; }
        public string? Note { get; set; }
        /// <summary>Lawyer to assign in the same move (required for archive → judicial when none is assigned).</summary>
        public int? LawyerId { get; set; }
    }

    [HttpPost("cases/{id:int}/move")]
    public async Task<IActionResult> Move(int id, [FromBody] MoveBody body, CancellationToken ct) =>
        Ok(await _service.MoveAsync(id, body.To ?? "", body.Note, Actor, body.LawyerId, ct));

    /// <summary>كود ارشيف القانونية of each sale: existing, or the one a move to the archive would issue now.</summary>
    [HttpGet("cases/{id:int}/archive-codes")]
    public async Task<IActionResult> ArchiveCodes(int id, CancellationToken ct)
    {
        var legalCase = await _store.CaseAsync(id, ct);
        if (legalCase is null)
            return NotFound(new { message = "الملف غير موجود" });
        var (lines, _) = await _service.ArchiveCodesAsync((int)legalCase.personId, Actor, ct);
        return Ok(lines);
    }

    public sealed class StepBody
    {
        public int StepTypeId { get; set; }
        public string? Note { get; set; }
        public DateTime? Date { get; set; }
        /// <summary>Another lawyer for this step only; the debtor's assigned lawyer does not change.</summary>
        public int? LawyerId { get; set; }
    }

    [HttpPost("cases/{id:int}/steps")]
    public async Task<IActionResult> AddStep(int id, [FromBody] StepBody body, CancellationToken ct) =>
        Ok(new { id = await _store.AddStepAsync(id, body.StepTypeId, body.Note, body.Date, Actor, ct, body.LawyerId) });

    public sealed class DoneBody
    {
        public bool Done { get; set; } = true;
    }

    /// <summary>Marks a scheduled step done (or back to scheduled).</summary>
    [HttpPost("cases/{id:int}/steps/{stepId:int}/done")]
    public async Task<IActionResult> StepDone(int id, int stepId, [FromBody] DoneBody? body, CancellationToken ct)
    {
        await _store.SetStepDoneAsync(id, stepId, body?.Done ?? true, ct);
        return Ok(new { id = stepId });
    }

    /// <summary>الخطوات القادمة: scheduled steps not done yet, overdue included. Filter by lawyer or debtor.</summary>
    [HttpGet("upcoming-steps")]
    public async Task<IActionResult> UpcomingSteps([FromQuery] int? lawyerId, [FromQuery] int? caseId, [FromQuery] int take = 300, CancellationToken ct = default) =>
        Ok(await _store.UpcomingStepsAsync(lawyerId, caseId, take, ct));

    [HttpDelete("cases/{id:int}/steps/{stepId:int}")]
    public async Task<IActionResult> DeleteStep(int id, int stepId, CancellationToken ct)
    {
        await _store.DeleteStepAsync(id, stepId, ct);
        return Ok(new { id = stepId });
    }

    public sealed class LawyerAssignBody
    {
        public int? LawyerId { get; set; }
    }

    [HttpPost("cases/{id:int}/lawyer")]
    public async Task<IActionResult> AssignLawyer(int id, [FromBody] LawyerAssignBody body, CancellationToken ct)
    {
        await _store.SetLawyerAsync(id, body.LawyerId, Actor, ct);
        return Ok(new { id });
    }

    public sealed class AnnulBody
    {
        public bool Annulled { get; set; }
        public string? Note { get; set; }
        /// <summary>مبلغ الابطال — required when asking for ابطال.</summary>
        public long Amount { get; set; }
    }

    /// <summary>
    /// annulled=true: asks محاسب القانونية for ابطال with مبلغ الابطال (the debtor is marked only on acceptance).
    /// annulled=false: removes ابطال, or withdraws a request that is still waiting.
    /// </summary>
    [HttpPost("cases/{id:int}/annul")]
    public async Task<IActionResult> Annul(int id, [FromBody] AnnulBody body, CancellationToken ct)
    {
        if (body.Annulled)
            return Ok(new { id, requestId = await _store.RequestAnnulAsync(id, body.Amount, body.Note, Actor, ct) });
        await _store.SetAnnulledAsync(id, false, body.Note, Actor, ct);
        return Ok(new { id });
    }

    public sealed class DecisionBody
    {
        public string? Text { get; set; }
        public DateTime? Date { get; set; }
    }

    [HttpPost("cases/{id:int}/decision")]
    public async Task<IActionResult> Decision(int id, [FromBody] DecisionBody body, CancellationToken ct)
    {
        await _store.SetDecisionAsync(id, body.Text, body.Date, Actor, ct);
        return Ok(new { id });
    }

    public sealed class NoteBody
    {
        public string? Note { get; set; }
    }

    [HttpPost("cases/{id:int}/note")]
    public async Task<IActionResult> Note(int id, [FromBody] NoteBody body, CancellationToken ct)
    {
        await _store.SetNoteAsync(id, body.Note, ct);
        return Ok(new { id });
    }

    /// <summary>The debtor wants to pay everything: asks محاسب القانونية to approve the penalty and take the تسديد.</summary>
    [HttpPost("cases/{id:int}/settlement-request")]
    public async Task<IActionResult> SettlementRequest(int id, [FromBody] NoteBody? body, CancellationToken ct) =>
        Ok(new { id = await _store.RequestSettlementAsync(id, body?.Note, Actor, ct) });

    [HttpPost("cases/{id:int}/close")]
    public async Task<IActionResult> Close(int id, [FromBody] NoteBody? body, CancellationToken ct)
    {
        await _store.CloseAsync(id, body?.Note, Actor, ct);
        return Ok(new { id });
    }

    // —— خطوات الدعوى والتنفيذ ——

    [HttpGet("step-types")]
    public async Task<IActionResult> StepTypes(CancellationToken ct) => Ok(await _store.StepTypesAsync(ct));

    public sealed class StepTypeBody
    {
        public int? Id { get; set; }
        public string? Flow { get; set; }
        public string? Name { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>A mandatory step must be logged before the debtor can leave that flow.</summary>
        public bool IsMandatory { get; set; }
    }

    [HttpPost("step-types")]
    public async Task<IActionResult> SaveStepType([FromBody] StepTypeBody body, CancellationToken ct) =>
        Ok(new { id = await _store.SaveStepTypeAsync(body.Id, body.Flow ?? "", body.Name ?? "", body.SortOrder, body.IsActive, body.IsMandatory, ct) });

    // —— المحامون ——

    [HttpGet("lawyers")]
    public async Task<IActionResult> Lawyers(CancellationToken ct) => Ok(await _store.LawyersAsync(ct));

    [HttpGet("lawyers/{id:int}")]
    public async Task<IActionResult> Lawyer(int id, CancellationToken ct)
    {
        var profile = await _store.LawyerProfileAsync(id, ct);
        return profile is null ? NotFound(new { message = "المحامي غير موجود" }) : Ok(profile);
    }

    public sealed class LawyerBody
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Branch { get; set; }
        public string? Phone { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
    }

    [HttpPost("lawyers")]
    public async Task<IActionResult> SaveLawyer([FromBody] LawyerBody body, CancellationToken ct) =>
        Ok(new { id = await _store.SaveLawyerAsync(body.Id, body.Name ?? "", body.Branch, body.Phone, body.Notes, body.IsActive, ct) });

    public sealed class AmountBody
    {
        public long Amount { get; set; }
        public string? Note { get; set; }
    }

    [HttpPost("lawyers/{id:int}/transfer")]
    public async Task<IActionResult> Transfer(int id, [FromBody] AmountBody body, CancellationToken ct)
    {
        await _store.TransferToLawyerAsync(id, body.Amount, body.Note, Actor, ct);
        return Ok(new { id });
    }

    [HttpPost("lawyers/{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] AmountBody body, CancellationToken ct)
    {
        await _store.ReturnFromLawyerAsync(id, body.Amount, body.Note, Actor, ct);
        return Ok(new { id });
    }

    /// <summary>Multipart: caseId, amount, note, outsideAssignment, optional image.</summary>
    [HttpPost("lawyers/{id:int}/fees")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> AddFee(
        int id,
        [FromForm] int caseId,
        [FromForm] long amount,
        [FromForm] string? note,
        [FromForm] bool outsideAssignment,
        IFormFile? image,
        CancellationToken ct)
    {
        string? stored = null;
        if (image is { Length: > 0 })
        {
            if (image.Length > MaxImageBytes)
                return BadRequest(new { message = "حجم الصورة أكبر من 8MB" });
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp")
                || !(image.ContentType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "الملف المرفق يجب أن يكون صورة (jpg / png / webp)" });
            stored = $"{Guid.NewGuid():N}{extension}";
            Directory.CreateDirectory(FeeFolder);
            await using var file = System.IO.File.Create(Path.Combine(FeeFolder, stored));
            await image.CopyToAsync(file, ct);
        }

        try
        {
            var txId = await _store.AddFeeAsync(id, caseId, amount, note, outsideAssignment, stored, Actor, ct);
            return Ok(new { id = txId });
        }
        catch
        {
            if (stored != null)
                System.IO.File.Delete(Path.Combine(FeeFolder, stored));
            throw;
        }
    }

    [HttpGet("fees/{txId:int}/image")]
    public async Task<IActionResult> FeeImage(int txId, CancellationToken ct)
    {
        var name = await _store.FeeImagePathAsync(txId, ct);
        if (string.IsNullOrWhiteSpace(name))
            return NotFound(new { message = "لا توجد صورة" });
        var path = Path.Combine(FeeFolder, Path.GetFileName(name));
        if (!System.IO.File.Exists(path))
            return NotFound(new { message = "الصورة غير موجودة على السيرفر" });
        var type = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
        return PhysicalFile(path, type);
    }

    private string FeeFolder => Path.Combine(_env.ContentRootPath, "App_Data", "legal", "fees");

    // —— السجلات ——

    [HttpGet("registers")]
    public async Task<IActionResult> Registers([FromQuery] int take = 500, CancellationToken ct = default) =>
        Ok(await _store.RegistersAsync(take, ct));

    [HttpPost("registers/company-funds")]
    public async Task<IActionResult> CompanyFunds([FromBody] AmountBody body, CancellationToken ct)
    {
        await _store.AddCompanyFundsAsync(body.Amount, body.Note, Actor, ct);
        return Ok(new { ok = true });
    }

    [HttpPost("registers/expenses")]
    public async Task<IActionResult> Expense([FromBody] AmountBody body, CancellationToken ct)
    {
        await _store.AddExpenseAsync(body.Amount, body.Note, Actor, ct);
        return Ok(new { ok = true });
    }

    // —— الإعدادات ——

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await _store.SettingsAsync(ct));

    public sealed class SettingBody
    {
        public string? Key { get; set; }
        public long Value { get; set; }
    }

    [HttpPut("settings")]
    public async Task<IActionResult> SetSetting([FromBody] SettingBody body, CancellationToken ct)
    {
        await _store.SetSettingAsync(body.Key ?? "", body.Value, ct);
        return Ok(await _store.SettingsAsync(ct));
    }
}
