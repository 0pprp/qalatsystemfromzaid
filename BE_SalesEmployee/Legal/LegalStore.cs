using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_SalesEmployee.Legal;

public sealed class LegalAccountRow
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string CityValue { get; set; } = "";
    public string? CityName { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? ListName { get; set; }
    public long LastTotal { get; set; }
    public long LastPaid { get; set; }
    public long LastRemaining { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    /// <summary>كود ارشيف القانونية of this sale; null until the debtor enters the legal archive.</summary>
    public string? ArchiveCode { get; set; }
}

/// <summary>What a sale's archive code is built from: branch letters, 2-digit sale year, floor(total sale / 1,000,000).</summary>
public sealed record ArchiveCodeRequest(int AccountId, string Branch, int Year2, int Slice);

public sealed class LegalCaseRow
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string Stage { get; set; } = "";
    public int? LawyerId { get; set; }
    public bool IsAnnulled { get; set; }
    public DateTime? JudicialStartedAtUtc { get; set; }
    public DateTime? ExecutionStartedAtUtc { get; set; }
}

/// <summary>
/// All legal-module SQL. Money is whole Iraqi dinars. Every state change that touches money
/// runs in one transaction with its register / charge rows, so the books cannot drift.
/// Queries alias columns in camelCase because rows are returned to the API as-is.
/// </summary>
public sealed class LegalStore
{
    private readonly LegalDb _db;

    public LegalStore(LegalDb db)
    {
        _db = db;
    }

    public Task<SqlConnection> OpenAsync(CancellationToken ct) => _db.OpenAsync(ct);

    // ───────────────────────── settings ─────────────────────────

    public async Task<Dictionary<string, long>> SettingsAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var rows = await c.QueryAsync<(string Key, long Value)>(new CommandDefinition(
            "SELECT [Key], [Value] FROM dbo.LegalSettings", cancellationToken: ct));
        return rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);
    }

    public async Task SetSettingAsync(string key, long value, CancellationToken ct)
    {
        if (key is not (LegalSettingKeys.JudicialFee or LegalSettingKeys.ExecutionFee or LegalSettingKeys.EarlySettlementPenalty))
            throw new LegalRuleException("إعداد غير معروف", 400);
        if (value < 0)
            throw new LegalRuleException("المبلغ غير صحيح", 400);
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.LegalSettings SET [Value] = @Value WHERE [Key] = @Key;
IF @@ROWCOUNT = 0 INSERT INTO dbo.LegalSettings ([Key], [Value]) VALUES (@Key, @Value);
""", new { Key = key, Value = value }, cancellationToken: ct));
    }

    private static async Task<long> SettingAsync(IDbConnection c, IDbTransaction tx, string key, long fallback) =>
        await c.ExecuteScalarAsync<long?>("SELECT [Value] FROM dbo.LegalSettings WHERE [Key] = @Key", new { Key = key }, tx)
        ?? fallback;

    // ───────────────────────── persons & accounts ─────────────────────────

    private const string PersonListSql = """
SELECT p.Id AS id, p.FullName AS fullName, p.Phone AS phone, p.Address AS address, p.Notes AS notes,
       p.CreatedBy AS createdBy, p.CreatedAtUtc AS createdAtUtc,
       (SELECT COUNT(*) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id) AS accountCount,
       ISNULL((SELECT SUM(a.LastRemaining) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id), 0) AS remainingSnapshot,
       (SELECT STRING_AGG(a.ArchiveCode, N' · ') WITHIN GROUP (ORDER BY a.Id) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode IS NOT NULL) AS archiveCodes,
       (SELECT STRING_AGG(x.CityName, N'، ') FROM (SELECT DISTINCT a.CityName FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id) x) AS cities,
       ISNULL((SELECT SUM(ch.Amount) FROM dbo.LegalCharges ch WHERE ch.PersonId = p.Id), 0) AS penaltiesCharged,
       ISNULL((SELECT SUM(py.PenaltyAmount) FROM dbo.LegalPayments py WHERE py.PersonId = p.Id), 0) AS penaltiesPaid,
       ISNULL((SELECT SUM(py.DebtAmount) FROM dbo.LegalPayments py WHERE py.PersonId = p.Id), 0) AS debtPaid,
       cs.Id AS caseId, cs.Stage AS stage, cs.IsAnnulled AS isAnnulled, cs.SentAtUtc AS sentAtUtc,
       cs.StageEnteredAtUtc AS stageEnteredAtUtc, cs.LawyerId AS lawyerId, l.Name AS lawyerName,
       cs.CloseReason AS closeReason
FROM dbo.LegalPersons p
OUTER APPLY (SELECT TOP (1) * FROM dbo.LegalCases k WHERE k.PersonId = p.Id ORDER BY CASE WHEN k.Stage = N'Closed' THEN 1 ELSE 0 END, k.Id DESC) cs
LEFT JOIN dbo.LegalLawyers l ON l.Id = cs.LawyerId

""";

    /// <summary>stage: a stage name, "NotSent" (grouped but not sent yet) or empty for everyone.</summary>
    public async Task<IEnumerable<dynamic>> PersonsAsync(string? stage, string? q, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition(PersonListSql + """
WHERE (@Q IS NULL OR p.FullName LIKE N'%' + @Q + N'%' OR p.Phone LIKE N'%' + @Q + N'%'
       OR EXISTS (SELECT 1 FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode LIKE N'%' + @Q + N'%'))
  AND (@Stage IS NULL
       OR (@Stage = N'NotSent' AND cs.Id IS NULL)
       OR cs.Stage = @Stage)
ORDER BY p.Id DESC;
""", new { Stage = Blank(stage), Q = Blank(q) }, cancellationToken: ct));
    }

    public async Task<dynamic?> PersonAsync(int personId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryFirstOrDefaultAsync(new CommandDefinition(
            PersonListSql + "WHERE p.Id = @Id;", new { Id = personId }, cancellationToken: ct));
    }

    public async Task<List<LegalAccountRow>> AccountsAsync(int personId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return (await c.QueryAsync<LegalAccountRow>(new CommandDefinition(
            "SELECT * FROM dbo.LegalPersonAccounts WHERE PersonId = @Id ORDER BY Id",
            new { Id = personId }, cancellationToken: ct))).ToList();
    }

    public async Task<List<LegalAccountRow>> AllAccountsAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return (await c.QueryAsync<LegalAccountRow>(new CommandDefinition(
            "SELECT * FROM dbo.LegalPersonAccounts", cancellationToken: ct))).ToList();
    }

    /// <summary>Which branch customers of one city already belong to a grouped debtor.</summary>
    public async Task<Dictionary<int, (int PersonId, string PersonName, string? Stage, string? ArchiveCode)>> LinkedCustomersAsync(string cityValue, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var rows = await c.QueryAsync<(int CustomerId, int PersonId, string FullName, string? Stage, string? ArchiveCode)>(new CommandDefinition("""
SELECT a.CustomerId, a.PersonId, p.FullName,
       (SELECT TOP (1) k.Stage FROM dbo.LegalCases k WHERE k.PersonId = p.Id ORDER BY CASE WHEN k.Stage = N'Closed' THEN 1 ELSE 0 END, k.Id DESC) AS Stage,
       a.ArchiveCode
FROM dbo.LegalPersonAccounts a
JOIN dbo.LegalPersons p ON p.Id = a.PersonId
WHERE a.CityValue = @City;
""", new { City = cityValue }, cancellationToken: ct));
        return rows.ToDictionary(r => r.CustomerId, r => (r.PersonId, r.FullName, r.Stage, r.ArchiveCode));
    }

    public sealed record NewAccount(string CityValue, string? CityName, int CustomerId, string? CustomerName, string? ListName, long Total, long Paid, long Remaining);

    public async Task<int> CreatePersonAsync(string fullName, string? phone, string? address, string? notes,
        IReadOnlyList<NewAccount> accounts, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new LegalRuleException("اسم المدين مطلوب", 400);
        if (accounts.Count == 0)
            throw new LegalRuleException("اختر حساباً واحداً على الأقل", 400);

        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var id = await c.ExecuteScalarAsync<int>("""
INSERT INTO dbo.LegalPersons (FullName, Phone, Address, Notes, CreatedBy)
OUTPUT INSERTED.Id VALUES (@FullName, @Phone, @Address, @Notes, @Actor);
""", new { FullName = fullName.Trim(), Phone = Blank(phone), Address = Blank(address), Notes = Blank(notes), Actor = actor }, tx);
        foreach (var account in accounts)
            await InsertAccountAsync(c, tx, id, account, actor);
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task AddAccountsAsync(int personId, IReadOnlyList<NewAccount> accounts, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var exists = await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.LegalPersons WHERE Id = @Id", new { Id = personId }, tx);
        if (exists == 0)
            throw new LegalRuleException("المدين غير موجود", 404);
        foreach (var account in accounts)
            await InsertAccountAsync(c, tx, personId, account, actor);
        await tx.CommitAsync(ct);
    }

    private static async Task InsertAccountAsync(SqlConnection c, SqlTransaction tx, int personId, NewAccount a, string actor)
    {
        var owner = await c.QueryFirstOrDefaultAsync<string>("""
SELECT p.FullName FROM dbo.LegalPersonAccounts x JOIN dbo.LegalPersons p ON p.Id = x.PersonId
WHERE x.CityValue = @CityValue AND x.CustomerId = @CustomerId;
""", new { a.CityValue, a.CustomerId }, tx);
        if (owner != null)
            throw new LegalRuleException($"الحساب «{a.CustomerName}» مضاف مسبقاً إلى المدين «{owner}»");
        await c.ExecuteAsync("""
INSERT INTO dbo.LegalPersonAccounts
    (PersonId, CityValue, CityName, CustomerId, CustomerName, ListName, LastTotal, LastPaid, LastRemaining, LastSyncedAtUtc, AddedBy)
VALUES (@PersonId, @CityValue, @CityName, @CustomerId, @CustomerName, @ListName, @Total, @Paid, @Remaining, SYSUTCDATETIME(), @Actor);
""", new { PersonId = personId, a.CityValue, a.CityName, a.CustomerId, a.CustomerName, a.ListName, a.Total, a.Paid, a.Remaining, Actor = actor }, tx);
    }

    public async Task UpdatePersonAsync(int personId, string fullName, string? phone, string? address, string? notes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new LegalRuleException("اسم المدين مطلوب", 400);
        await using var c = await _db.OpenAsync(ct);
        var n = await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.LegalPersons SET FullName = @FullName, Phone = @Phone, Address = @Address, Notes = @Notes WHERE Id = @Id",
            new { Id = personId, FullName = fullName.Trim(), Phone = Blank(phone), Address = Blank(address), Notes = Blank(notes) },
            cancellationToken: ct));
        if (n == 0)
            throw new LegalRuleException("المدين غير موجود", 404);
    }

    /// <summary>An account that already received a legal payment stays, so the payment history keeps its meaning.</summary>
    public async Task RemoveAccountAsync(int personId, int accountId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var account = await c.QueryFirstOrDefaultAsync<LegalAccountRow>(new CommandDefinition(
            "SELECT * FROM dbo.LegalPersonAccounts WHERE Id = @Id AND PersonId = @PersonId",
            new { Id = accountId, PersonId = personId }, cancellationToken: ct));
        if (account is null)
            throw new LegalRuleException("الحساب غير موجود", 404);
        var paid = await c.ExecuteScalarAsync<int>(new CommandDefinition("""
SELECT COUNT(1) FROM dbo.LegalPaymentLines l JOIN dbo.LegalPayments p ON p.Id = l.PaymentId
WHERE p.PersonId = @PersonId AND l.CityValue = @City AND l.CustomerId = @CustomerId AND l.Status = N'Posted';
""", new { PersonId = personId, City = account.CityValue, account.CustomerId }, cancellationToken: ct));
        if (paid > 0)
            throw new LegalRuleException("لا يمكن إزالة حساب سُجّل عليه تسديد من القانونية");
        var count = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.LegalPersonAccounts WHERE PersonId = @PersonId", new { PersonId = personId }, cancellationToken: ct));
        if (count <= 1)
            throw new LegalRuleException("يجب أن يبقى للمدين حساب واحد على الأقل");
        await c.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.LegalPersonAccounts WHERE Id = @Id", new { Id = accountId }, cancellationToken: ct));
    }

    /// <summary>Only a debtor that was never sent to the legal app can be ungrouped.</summary>
    public async Task DeletePersonAsync(int personId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var used = await c.ExecuteScalarAsync<int>("""
SELECT (SELECT COUNT(1) FROM dbo.LegalCases WHERE PersonId = @Id)
     + (SELECT COUNT(1) FROM dbo.LegalPayments WHERE PersonId = @Id)
     + (SELECT COUNT(1) FROM dbo.LegalCharges WHERE PersonId = @Id);
""", new { Id = personId }, tx);
        if (used > 0)
            throw new LegalRuleException("لا يمكن حذف مدين أُرسل إلى القانونية أو عليه حركات");
        await c.ExecuteAsync("DELETE FROM dbo.LegalPersonAccounts WHERE PersonId = @Id; DELETE FROM dbo.LegalPersons WHERE Id = @Id;", new { Id = personId }, tx);
        await tx.CommitAsync(ct);
    }

    public async Task SaveSnapshotsAsync(IEnumerable<(int AccountId, long Total, long Paid, long Remaining, string? Name, string? ListName)> rows, CancellationToken ct)
    {
        var list = rows.ToList();
        if (list.Count == 0)
            return;
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.LegalPersonAccounts
SET LastTotal = @Total, LastPaid = @Paid, LastRemaining = @Remaining, LastSyncedAtUtc = SYSUTCDATETIME(),
    CustomerName = COALESCE(@Name, CustomerName), ListName = COALESCE(@ListName, ListName)
WHERE Id = @AccountId;
""", list.Select(r => new { r.AccountId, r.Total, r.Paid, r.Remaining, r.Name, r.ListName }), cancellationToken: ct));
    }

    // ───────────────────────── cases ─────────────────────────

    public async Task<int> SendPersonAsync(int personId, string? note, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var accounts = await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.LegalPersonAccounts WHERE PersonId = @Id", new { Id = personId }, tx);
        if (accounts == 0)
            throw new LegalRuleException("المدين بلا حسابات", 400);
        var open = await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.LegalCases WHERE PersonId = @Id AND Stage <> N'Closed'", new { Id = personId }, tx);
        if (open > 0)
            throw new LegalRuleException("المدين موجود مسبقاً في تطبيق القانونية");
        var caseId = await c.ExecuteScalarAsync<int>("""
INSERT INTO dbo.LegalCases (PersonId, Stage, Note, SentBy) OUTPUT INSERTED.Id
VALUES (@PersonId, N'Recent', @Note, @Actor);
""", new { PersonId = personId, Note = Blank(note), Actor = actor }, tx);
        await EventAsync(c, tx, caseId, "Sent", null, LegalStages.Recent, note, actor);
        await tx.CommitAsync(ct);
        return caseId;
    }

    private const string CaseListSql = """
SELECT k.Id AS id, k.PersonId AS personId, p.FullName AS fullName, p.Phone AS phone,
       k.Stage AS stage, k.IsAnnulled AS isAnnulled, k.LawyerId AS lawyerId, l.Name AS lawyerName,
       k.SentAtUtc AS sentAtUtc, k.StageEnteredAtUtc AS stageEnteredAtUtc,
       k.JudicialStartedAtUtc AS judicialStartedAtUtc, k.ExecutionStartedAtUtc AS executionStartedAtUtc,
       k.DecisionText AS decisionText, k.DecisionDate AS decisionDate, k.Note AS note,
       k.AnnulNote AS annulNote, k.AnnulledAtUtc AS annulledAtUtc, k.ClosedAtUtc AS closedAtUtc, k.CloseReason AS closeReason,
       (SELECT COUNT(*) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id) AS accountCount,
       ISNULL((SELECT SUM(a.LastRemaining) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id), 0) AS remainingSnapshot,
       (SELECT STRING_AGG(a.ArchiveCode, N' · ') WITHIN GROUP (ORDER BY a.Id) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode IS NOT NULL) AS archiveCodes,
       (SELECT STRING_AGG(x.CityName, N'، ') FROM (SELECT DISTINCT a.CityName FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id) x) AS cities,
       ISNULL((SELECT SUM(t.Amount) FROM dbo.LegalLawyerTx t WHERE t.CaseId = k.Id AND t.Kind = N'Fee'), 0) AS feesTotal,
       (SELECT COUNT(*) FROM dbo.LegalStepTypes t
        WHERE t.IsActive = 1 AND t.IsMandatory = 1
          AND t.Flow = CASE k.Stage WHEN N'Judicial' THEN N'Judicial' WHEN N'Execution' THEN N'Execution' END
          AND NOT EXISTS (SELECT 1 FROM dbo.LegalCaseSteps s WHERE s.CaseId = k.Id AND s.StepTypeId = t.Id AND s.IsDone = 1)) AS missingMandatory,
       ls.StepTypeId AS lastStepTypeId, ls.StepName AS lastStepName, ls.StepDate AS lastStepDate,
       (SELECT COUNT(*) FROM dbo.LegalCaseSteps s WHERE s.CaseId = k.Id) AS stepCount,
       (SELECT COUNT(*) FROM dbo.LegalSettlementRequests r WHERE r.CaseId = k.Id AND r.Status = N'Pending') AS pendingSettlement,
       (SELECT TOP (1) r.Amount FROM dbo.LegalAnnulRequests r WHERE r.CaseId = k.Id AND r.Status = N'Pending' ORDER BY r.Id DESC) AS pendingAnnulAmount
FROM dbo.LegalCases k
JOIN dbo.LegalPersons p ON p.Id = k.PersonId
LEFT JOIN dbo.LegalLawyers l ON l.Id = k.LawyerId
OUTER APPLY (SELECT TOP (1) s.StepTypeId, s.StepName, s.StepDate FROM dbo.LegalCaseSteps s
             WHERE s.CaseId = k.Id AND s.Flow = CASE WHEN k.Stage = N'Execution' THEN N'Execution' ELSE N'Judicial' END
             ORDER BY s.StepDate DESC, s.Id DESC) ls

""";

    public async Task<IEnumerable<dynamic>> CasesAsync(string? stage, string? q, int? lawyerId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition(CaseListSql + """
WHERE (@Stage IS NULL OR k.Stage = @Stage)
  AND (@Stage IS NOT NULL OR k.Stage <> N'Closed')
  AND (@LawyerId IS NULL OR k.LawyerId = @LawyerId)
  AND (@Q IS NULL OR p.FullName LIKE N'%' + @Q + N'%' OR p.Phone LIKE N'%' + @Q + N'%'
       OR EXISTS (SELECT 1 FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode LIKE N'%' + @Q + N'%'))
ORDER BY k.StageEnteredAtUtc DESC, k.Id DESC;
""", new { Stage = Blank(stage), Q = Blank(q), LawyerId = lawyerId }, cancellationToken: ct));
    }

    public async Task<dynamic?> CaseAsync(int caseId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryFirstOrDefaultAsync(new CommandDefinition(
            CaseListSql + "WHERE k.Id = @Id;", new { Id = caseId }, cancellationToken: ct));
    }

    public async Task<IEnumerable<dynamic>> StageCountsAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition("""
SELECT k.Stage AS stage, COUNT(*) AS [count],
       SUM(CASE WHEN k.IsAnnulled = 1 THEN 1 ELSE 0 END) AS annulled,
       ISNULL(SUM(r.Remaining), 0) AS remainingSnapshot
FROM dbo.LegalCases k
OUTER APPLY (SELECT SUM(a.LastRemaining) AS Remaining FROM dbo.LegalPersonAccounts a WHERE a.PersonId = k.PersonId) r
GROUP BY k.Stage;
""", cancellationToken: ct));
    }

    public async Task<object> CaseExtrasAsync(int caseId, int personId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var steps = await c.QueryAsync(new CommandDefinition("""
SELECT s.Id AS id, s.Flow AS flow, s.StepTypeId AS stepTypeId, s.StepName AS stepName, s.Note AS note,
       s.StepDate AS stepDate, s.CreatedBy AS createdBy, s.CreatedAtUtc AS createdAtUtc,
       s.LawyerId AS lawyerId, sl.Name AS lawyerName, s.IsSubstitute AS isSubstitute,
       s.IsDone AS isDone,
       CAST(CASE WHEN s.IsDone = 0 AND s.StepDate < CAST(DATEADD(HOUR, 3, SYSUTCDATETIME()) AS date) THEN 1 ELSE 0 END AS bit) AS isOverdue
FROM dbo.LegalCaseSteps s
LEFT JOIN dbo.LegalLawyers sl ON sl.Id = s.LawyerId
WHERE s.CaseId = @Id ORDER BY s.StepDate, s.Id;
""", new { Id = caseId }, cancellationToken: ct));
        var events = await c.QueryAsync(new CommandDefinition("""
SELECT Id AS id, Kind AS kind, FromStage AS fromStage, ToStage AS toStage, Note AS note, Actor AS actor, AtUtc AS atUtc
FROM dbo.LegalCaseEvents WHERE CaseId = @Id ORDER BY Id DESC;
""", new { Id = caseId }, cancellationToken: ct));
        var fees = await c.QueryAsync(new CommandDefinition("""
SELECT t.Id AS id, t.LawyerId AS lawyerId, l.Name AS lawyerName, t.Amount AS amount, t.Note AS note,
       t.OutsideAssignment AS outsideAssignment, CAST(CASE WHEN t.ImagePath IS NULL THEN 0 ELSE 1 END AS bit) AS hasImage,
       t.Actor AS actor, t.AtUtc AS atUtc
FROM dbo.LegalLawyerTx t JOIN dbo.LegalLawyers l ON l.Id = t.LawyerId
WHERE t.CaseId = @Id AND t.Kind = N'Fee' ORDER BY t.Id DESC;
""", new { Id = caseId }, cancellationToken: ct));
        var money = await MoneyAsync(c, personId, ct);
        return new { steps, events, fees, money.charges, money.payments, money.requests };
    }

    public async Task<(IEnumerable<dynamic> charges, IEnumerable<dynamic> payments, IEnumerable<dynamic> requests)> MoneyAsync(int personId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await MoneyAsync(c, personId, ct);
    }

    private static async Task<(IEnumerable<dynamic> charges, IEnumerable<dynamic> payments, IEnumerable<dynamic> requests)> MoneyAsync(
        SqlConnection c, int personId, CancellationToken ct)
    {
        var charges = await c.QueryAsync(new CommandDefinition("""
SELECT Id AS id, CaseId AS caseId, Kind AS kind, Amount AS amount, Note AS note, Actor AS actor, AtUtc AS atUtc
FROM dbo.LegalCharges WHERE PersonId = @Id ORDER BY Id;
""", new { Id = personId }, cancellationToken: ct));
        var payments = (await c.QueryAsync(new CommandDefinition("""
SELECT Id AS id, DebtAmount AS debtAmount, PenaltyAmount AS penaltyAmount, Note AS note, Actor AS actor, AtUtc AS atUtc
FROM dbo.LegalPayments WHERE PersonId = @Id ORDER BY Id DESC;
""", new { Id = personId }, cancellationToken: ct))).ToList();
        var lines = (await c.QueryAsync(new CommandDefinition("""
SELECT l.PaymentId AS paymentId, l.CityValue AS cityValue, l.CustomerId AS customerId, l.CustomerName AS customerName,
       l.Amount AS amount, l.BranchPaymentId AS branchPaymentId, l.Status AS status, l.Error AS error
FROM dbo.LegalPaymentLines l JOIN dbo.LegalPayments p ON p.Id = l.PaymentId
WHERE p.PersonId = @Id ORDER BY l.Id;
""", new { Id = personId }, cancellationToken: ct))).ToList();
        foreach (var payment in payments)
        {
            int id = payment.id;
            ((IDictionary<string, object>)payment)["lines"] = lines.Where(l => (int)l.paymentId == id).ToList();
        }

        var requests = await c.QueryAsync(new CommandDefinition("""
SELECT Id AS id, CaseId AS caseId, SuggestedPenalty AS suggestedPenalty, Note AS note, RequestedBy AS requestedBy,
       RequestedAtUtc AS requestedAtUtc, Status AS status, ApprovedPenalty AS approvedPenalty,
       DecidedBy AS decidedBy, DecidedAtUtc AS decidedAtUtc, DecisionNote AS decisionNote
FROM dbo.LegalSettlementRequests WHERE PersonId = @Id ORDER BY Id DESC;
""", new { Id = personId }, cancellationToken: ct));
        return (charges, payments, requests);
    }

    private static async Task<LegalCaseRow> LockCaseAsync(SqlConnection c, SqlTransaction tx, int caseId)
    {
        var row = await c.QueryFirstOrDefaultAsync<LegalCaseRow>("""
SELECT Id, PersonId, Stage, LawyerId, IsAnnulled, JudicialStartedAtUtc, ExecutionStartedAtUtc
FROM dbo.LegalCases WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id;
""", new { Id = caseId }, tx);
        return row ?? throw new LegalRuleException("الملف غير موجود", 404);
    }

    private static Task EventAsync(SqlConnection c, SqlTransaction tx, int caseId, string kind, string? from, string? to, string? note, string actor) =>
        c.ExecuteAsync("""
INSERT INTO dbo.LegalCaseEvents (CaseId, Kind, FromStage, ToStage, Note, Actor)
VALUES (@CaseId, @Kind, @From, @To, @Note, @Actor);
""", new { CaseId = caseId, Kind = kind, From = from, To = to, Note = Blank(note), Actor = actor }, tx);

    /// <summary>
    /// Moves a debtor along the flow. Entering the judicial flow for the first time charges the
    /// judicial fee, entering التنفيذ for the first time charges the execution fee: the debtor gets
    /// a penalty of that amount and دين قلعة الضمان goes down by it (the company now owes it to the team).
    /// </summary>
    public async Task<object> MoveAsync(int caseId, string to, string? note, string actor, CancellationToken ct, int? lawyerId = null,
        IReadOnlyList<ArchiveCodeRequest>? archiveCodes = null)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        if (!LegalStages.CanMove(row.Stage, to))
            throw new LegalRuleException($"لا يمكن النقل من «{LegalStages.Label(row.Stage)}» إلى «{LegalStages.Label(to)}»");

        // Entering the legal archive gives every sale that has no كود ارشيف القانونية one. Existing codes are kept.
        Dictionary<int, string> issuedCodes = new();
        if (row.Stage == LegalStages.Recent && to == LegalStages.Archive)
        {
            var without = (await c.QueryAsync<int>(
                "SELECT Id FROM dbo.LegalPersonAccounts WHERE PersonId = @Id AND ArchiveCode IS NULL", new { Id = row.PersonId }, tx)).ToList();
            var requests = (archiveCodes ?? Array.Empty<ArchiveCodeRequest>()).Where(r => without.Contains(r.AccountId)).ToList();
            if (requests.Count != without.Count)
                throw new LegalRuleException("تعذر تجهيز كود ارشيف القانونية لكل حسابات المدين");
            issuedCodes = await AllocateArchiveCodesAsync(c, tx, requests);
        }

        // A judicial case is never started without a lawyer. He can be chosen in the same move.
        if (row.Stage == LegalStages.Archive && to == LegalStages.Judicial)
        {
            if (lawyerId is int chosen && chosen != row.LawyerId)
            {
                var lawyerName = await c.ExecuteScalarAsync<string>(
                    "SELECT Name FROM dbo.LegalLawyers WHERE Id = @Id AND IsActive = 1", new { Id = chosen }, tx)
                    ?? throw new LegalRuleException("المحامي غير موجود أو غير فعّال", 404);
                await c.ExecuteAsync("UPDATE dbo.LegalCases SET LawyerId = @LawyerId WHERE Id = @Id", new { Id = caseId, LawyerId = chosen }, tx);
                await EventAsync(c, tx, caseId, "Lawyer", row.Stage, row.Stage, $"إسناد إلى المحامي {lawyerName}", actor);
                row.LawyerId = chosen;
            }

            if (row.LawyerId is null)
                throw new LegalRuleException("يجب إسناد محامٍ قبل اقامة الدعوى القضائية", 409, "LawyerRequired");
        }

        // Leaving the judicial flow forward needs every mandatory step logged at least once.
        // (التنفيذ is the last station, so its mandatory steps are a checklist, not a gate.)
        if (row.Stage == LegalStages.Judicial && to == LegalStages.Decisions)
        {
            var missing = (await c.QueryAsync<string>("""
SELECT t.Name FROM dbo.LegalStepTypes t
WHERE t.Flow = N'Judicial' AND t.IsActive = 1 AND t.IsMandatory = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.LegalCaseSteps s WHERE s.CaseId = @Id AND s.StepTypeId = t.Id AND s.IsDone = 1)
ORDER BY t.SortOrder, t.Id;
""", new { Id = caseId }, tx)).ToList();
            if (missing.Count > 0)
                throw new LegalRuleException("لا يمكن إنهاء الدعوى قبل إكمال الخطوات الإلزامية: " + string.Join("، ", missing), 409, "MandatorySteps");
        }

        long charged = 0;
        string? chargeKind = null;
        if (to == LegalStages.Judicial && row.JudicialStartedAtUtc is null)
        {
            charged = await SettingAsync(c, tx, LegalSettingKeys.JudicialFee, 200_000);
            chargeKind = "Judicial";
        }
        else if (to == LegalStages.Execution && row.ExecutionStartedAtUtc is null)
        {
            charged = await SettingAsync(c, tx, LegalSettingKeys.ExecutionFee, 150_000);
            chargeKind = "Execution";
        }

        await c.ExecuteAsync("""
UPDATE dbo.LegalCases
SET Stage = @To, StageEnteredAtUtc = SYSUTCDATETIME(),
    JudicialStartedAtUtc = CASE WHEN @To = N'Judicial' AND JudicialStartedAtUtc IS NULL THEN SYSUTCDATETIME() ELSE JudicialStartedAtUtc END,
    ExecutionStartedAtUtc = CASE WHEN @To = N'Execution' AND ExecutionStartedAtUtc IS NULL THEN SYSUTCDATETIME() ELSE ExecutionStartedAtUtc END
WHERE Id = @Id;
""", new { Id = caseId, To = to }, tx);
        await EventAsync(c, tx, caseId, "Move", row.Stage, to, note, actor);

        if (chargeKind != null && charged > 0)
        {
            var label = chargeKind == "Judicial" ? "اقامة دعوى قضائية" : "الدخول في التنفيذ";
            await c.ExecuteAsync("""
INSERT INTO dbo.LegalCharges (PersonId, CaseId, Kind, Amount, Note, Actor) VALUES (@PersonId, @CaseId, @Kind, @Amount, @Note, @Actor);
INSERT INTO dbo.LegalRegisterTx (Register, Kind, Amount, CaseId, Note, Actor) VALUES (N'CompanyDebt', @RegisterKind, -@Amount, @CaseId, @Note, @Actor);
""", new
            {
                row.PersonId,
                CaseId = caseId,
                Kind = chargeKind,
                RegisterKind = chargeKind + "Fee",
                Amount = charged,
                Note = "أجور " + label,
                Actor = actor,
            }, tx);
        }

        await tx.CommitAsync(ct);
        return new { caseId, from = row.Stage, to, charged, chargeKind, archiveCodes = issuedCodes.Values.ToList() };
    }

    /// <summary>{BRANCH}{YY}-{BOX}-{TAIL}. Tail = slice * 1000 + sequence; slice 0 is padded to three digits.</summary>
    public static string FormatArchiveCode(string branch, int year2, int slice, int box, int seq) =>
        $"{branch}{year2:D2}-{box}-{(slice == 0 ? seq.ToString("D3") : (slice * 1000 + seq).ToString())}";

    /// <summary>
    /// Takes the next code of each sale's series (branch + sale year + million slice): highest box, then
    /// highest sequence in it, plus one. Gaps are never filled; a box holds 999 sequences. Any string
    /// already used, by the old system or by this one, is skipped.
    /// </summary>
    private static async Task<Dictionary<int, string>> AllocateArchiveCodesAsync(
        SqlConnection c, SqlTransaction tx, IReadOnlyList<ArchiveCodeRequest> requests)
    {
        var issued = new Dictionary<int, string>();
        foreach (var request in requests)
        {
            var key = new { request.Branch, request.Year2, request.Slice };
            var series = await c.QueryFirstOrDefaultAsync<SeriesRow>("""
SELECT MaxBox, MaxSeq FROM dbo.LegalArchiveSeries WITH (UPDLOCK, HOLDLOCK)
WHERE BranchCode = @Branch AND Year2 = @Year2 AND Slice = @Slice;
""", key, tx);
            var (box, seq) = series is null ? (1, 0) : (series.MaxBox, series.MaxSeq);
            string code;
            do
            {
                seq++;
                if (seq > 999)
                {
                    box++;
                    seq = 1;
                }

                code = FormatArchiveCode(request.Branch, request.Year2, request.Slice, box, seq);
            }
            while (await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.LegalArchiveTaken WHERE Code = @Code", new { Code = code }, tx) > 0);

            await c.ExecuteAsync("""
UPDATE dbo.LegalArchiveSeries SET MaxBox = @Box, MaxSeq = @Seq WHERE BranchCode = @Branch AND Year2 = @Year2 AND Slice = @Slice;
IF @@ROWCOUNT = 0
    INSERT INTO dbo.LegalArchiveSeries (BranchCode, Year2, Slice, MaxBox, MaxSeq) VALUES (@Branch, @Year2, @Slice, @Box, @Seq);
INSERT INTO dbo.LegalArchiveTaken (Code, Source) VALUES (@Code, N'legal-app');
UPDATE dbo.LegalPersonAccounts SET ArchiveCode = @Code WHERE Id = @AccountId AND ArchiveCode IS NULL;
""", new { request.Branch, request.Year2, request.Slice, Box = box, Seq = seq, Code = code, request.AccountId }, tx);
            issued[request.AccountId] = code;
        }

        return issued;
    }

    /// <summary>The codes a move to the archive would issue right now, without issuing them.</summary>
    public async Task<Dictionary<int, string>> PreviewArchiveCodesAsync(IReadOnlyList<ArchiveCodeRequest> requests, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var codes = await AllocateArchiveCodesAsync(c, tx, requests);
        await tx.RollbackAsync(ct);
        return codes;
    }

    public async Task CloseAsync(int caseId, string? reason, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        if (row.Stage == LegalStages.Closed)
            throw new LegalRuleException("الملف مغلق مسبقاً");
        await c.ExecuteAsync("""
UPDATE dbo.LegalCases SET Stage = N'Closed', StageEnteredAtUtc = SYSUTCDATETIME(), ClosedAtUtc = SYSUTCDATETIME(), CloseReason = @Reason
WHERE Id = @Id;
""", new { Id = caseId, Reason = Blank(reason) ?? "أُغلق يدوياً" }, tx);
        await EventAsync(c, tx, caseId, "Close", row.Stage, LegalStages.Closed, reason, actor);
        await tx.CommitAsync(ct);
    }

    /// <summary>ابطال: an agreement to pay without further action. Pointless once the debtor is in التنفيذ.</summary>
    public async Task SetAnnulledAsync(int caseId, bool annulled, string? note, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        if (row.Stage == LegalStages.Closed)
            throw new LegalRuleException("الملف مغلق");
        if (annulled && row.Stage == LegalStages.Execution)
            throw new LegalRuleException("لا يمكن الابطال بعد وصول المدين إلى التنفيذ");
        await c.ExecuteAsync("""
UPDATE dbo.LegalCases
SET IsAnnulled = @Annulled, AnnulledAtUtc = CASE WHEN @Annulled = 1 THEN SYSUTCDATETIME() ELSE NULL END,
    AnnulNote = CASE WHEN @Annulled = 1 THEN @Note ELSE NULL END
WHERE Id = @Id;
""", new { Id = caseId, Annulled = annulled, Note = Blank(note) }, tx);
        if (!annulled)
        {
            await c.ExecuteAsync("""
UPDATE dbo.LegalAnnulRequests SET Status = N'Withdrawn', DecidedBy = @Actor, DecidedAtUtc = SYSUTCDATETIME()
WHERE CaseId = @Id AND Status = N'Pending';
""", new { Id = caseId, Actor = actor }, tx);
        }

        await EventAsync(c, tx, caseId, annulled ? "Annul" : "Unannul", row.Stage, row.Stage, note, actor);
        await tx.CommitAsync(ct);
    }

    /// <summary>The dashboard returns an ابطال debtor who stopped paying. Only the ابطال mark is lifted.</summary>
    public async Task ReturnFromAnnulAsync(int caseId, string note, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        if (row.Stage == LegalStages.Closed)
            throw new LegalRuleException("الملف مغلق");
        if (!row.IsAnnulled)
            throw new LegalRuleException("المدين ليس في الابطال");
        await c.ExecuteAsync(
            "UPDATE dbo.LegalCases SET IsAnnulled = 0, AnnulledAtUtc = NULL, AnnulNote = NULL WHERE Id = @Id;",
            new { Id = caseId }, tx);
        await EventAsync(c, tx, caseId, "Unannul", row.Stage, row.Stage, note, actor);
        await tx.CommitAsync(ct);
    }

    // ───────────────────────── طلبات الابطال ─────────────────────────

    /// <summary>
    /// The legal app asks for ابطال and states مبلغ الابطال. Nothing changes on the debtor until
    /// محاسب القانونية accepts it on the company dashboard.
    /// </summary>
    public async Task<int> RequestAnnulAsync(int caseId, long amount, string? note, string actor, CancellationToken ct)
    {
        if (amount <= 0)
            throw new LegalRuleException("أدخل مبلغ الابطال", 400);
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        if (row.Stage == LegalStages.Closed)
            throw new LegalRuleException("الملف مغلق");
        if (row.Stage == LegalStages.Execution)
            throw new LegalRuleException("لا يمكن الابطال بعد وصول المدين إلى التنفيذ");
        if (row.IsAnnulled)
            throw new LegalRuleException("المدين مؤشَّر ابطال مسبقاً");
        var pending = await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.LegalAnnulRequests WHERE CaseId = @Id AND Status = N'Pending'", new { Id = caseId }, tx);
        if (pending > 0)
            throw new LegalRuleException("يوجد طلب ابطال بانتظار محاسب القانونية");
        var id = await c.ExecuteScalarAsync<int>("""
INSERT INTO dbo.LegalAnnulRequests (PersonId, CaseId, Amount, Note, RequestedBy) OUTPUT INSERTED.Id
VALUES (@PersonId, @CaseId, @Amount, @Note, @Actor);
""", new { row.PersonId, CaseId = caseId, Amount = amount, Note = Blank(note), Actor = actor }, tx);
        await EventAsync(c, tx, caseId, "AnnulRequest", row.Stage, row.Stage,
            $"مبلغ الابطال {amount:N0}" + (string.IsNullOrWhiteSpace(note) ? "" : $" — {note!.Trim()}"), actor);
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<IEnumerable<dynamic>> AnnulRequestsAsync(string? status, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition("""
SELECT r.Id AS id, r.PersonId AS personId, p.FullName AS fullName, p.Phone AS phone, r.CaseId AS caseId, k.Stage AS stage,
       r.Amount AS amount, r.Note AS note, r.RequestedBy AS requestedBy, r.RequestedAtUtc AS requestedAtUtc,
       r.Status AS status, r.DecidedBy AS decidedBy, r.DecidedAtUtc AS decidedAtUtc, r.DecisionNote AS decisionNote,
       ISNULL((SELECT SUM(a.LastRemaining) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id), 0) AS remainingSnapshot,
       ISNULL((SELECT SUM(Amount) FROM dbo.LegalCharges WHERE PersonId = p.Id), 0)
         - ISNULL((SELECT SUM(PenaltyAmount) FROM dbo.LegalPayments WHERE PersonId = p.Id), 0) AS penaltiesDue,
       (SELECT STRING_AGG(a.ArchiveCode, N' · ') WITHIN GROUP (ORDER BY a.Id) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode IS NOT NULL) AS archiveCodes
FROM dbo.LegalAnnulRequests r
JOIN dbo.LegalPersons p ON p.Id = r.PersonId
JOIN dbo.LegalCases k ON k.Id = r.CaseId
WHERE (@Status IS NULL OR r.Status = @Status)
ORDER BY CASE WHEN r.Status = N'Pending' THEN 0 ELSE 1 END, r.Id DESC;
""", new { Status = Blank(status) }, cancellationToken: ct));
    }

    public sealed class AnnulRequestRow
    {
        public int Id { get; set; }
        public int PersonId { get; set; }
        public int CaseId { get; set; }
        public long Amount { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = "";
    }

    public async Task<AnnulRequestRow?> AnnulRequestAsync(int requestId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryFirstOrDefaultAsync<AnnulRequestRow>(new CommandDefinition(
            "SELECT Id, PersonId, CaseId, Amount, Note, Status FROM dbo.LegalAnnulRequests WHERE Id = @Id",
            new { Id = requestId }, cancellationToken: ct));
    }

    /// <summary>Closes a request. On acceptance the debtor becomes ابطال; the تسديد was recorded just before by the caller.</summary>
    public async Task FinishAnnulRequestAsync(int requestId, bool approve, int? paymentId, string? note, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var request = await c.QueryFirstOrDefaultAsync<AnnulRequestRow>(
            "SELECT Id, PersonId, CaseId, Amount, Note, Status FROM dbo.LegalAnnulRequests WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id", new { Id = requestId }, tx)
            ?? throw new LegalRuleException("الطلب غير موجود", 404);
        if (request.Status != "Pending")
            throw new LegalRuleException("تم البت في هذا الطلب مسبقاً");
        var row = await LockCaseAsync(c, tx, request.CaseId);
        await c.ExecuteAsync("""
UPDATE dbo.LegalAnnulRequests
SET Status = @Status, PaymentId = @PaymentId, DecidedBy = @Actor, DecidedAtUtc = SYSUTCDATETIME(), DecisionNote = @Note
WHERE Id = @Id;
""", new { Id = requestId, Status = approve ? "Approved" : "Rejected", PaymentId = paymentId, Actor = actor, Note = Blank(note) }, tx);
        if (approve && row.Stage != LegalStages.Closed)
        {
            await c.ExecuteAsync("""
UPDATE dbo.LegalCases SET IsAnnulled = 1, AnnulledAtUtc = SYSUTCDATETIME(), AnnulNote = @Note WHERE Id = @Id;
""", new { Id = request.CaseId, Note = Blank(request.Note) ?? $"مبلغ الابطال {request.Amount:N0}" }, tx);
        }

        await EventAsync(c, tx, request.CaseId, approve ? "Annul" : "AnnulRejected", row.Stage, row.Stage,
            approve ? $"قُبل الابطال — مبلغ {request.Amount:N0} سُجّل تسديداً" : note, actor);
        await tx.CommitAsync(ct);
    }

    public async Task SetLawyerAsync(int caseId, int? lawyerId, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        if (lawyerId is null && row.Stage is LegalStages.Judicial or LegalStages.Decisions or LegalStages.Execution)
            throw new LegalRuleException("لا يمكن ترك المدين بلا محامٍ بعد اقامة الدعوى — اختر محامياً آخر بدلاً من الإلغاء");
        string? name = null;
        if (lawyerId is int id)
        {
            name = await c.ExecuteScalarAsync<string>("SELECT Name FROM dbo.LegalLawyers WHERE Id = @Id", new { Id = id }, tx)
                ?? throw new LegalRuleException("المحامي غير موجود", 404);
        }

        await c.ExecuteAsync("UPDATE dbo.LegalCases SET LawyerId = @LawyerId WHERE Id = @Id", new { Id = caseId, LawyerId = lawyerId }, tx);
        await EventAsync(c, tx, caseId, "Lawyer", row.Stage, row.Stage, name is null ? "إلغاء إسناد المحامي" : $"إسناد إلى المحامي {name}", actor);
        await tx.CommitAsync(ct);
    }

    public async Task SetDecisionAsync(int caseId, string? text, DateTime? date, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        await c.ExecuteAsync("UPDATE dbo.LegalCases SET DecisionText = @Text, DecisionDate = @Date WHERE Id = @Id",
            new { Id = caseId, Text = Blank(text), Date = date?.Date }, tx);
        await EventAsync(c, tx, caseId, "Decision", row.Stage, row.Stage, text, actor);
        await tx.CommitAsync(ct);
    }

    public async Task SetNoteAsync(int caseId, string? note, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE dbo.LegalCases SET Note = @Note WHERE Id = @Id",
            new { Id = caseId, Note = Blank(note) }, cancellationToken: ct));
    }

    // ───────────────────────── steps ─────────────────────────

    public async Task<IEnumerable<dynamic>> StepTypesAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition("""
SELECT t.Id AS id, t.Flow AS flow, t.Name AS name, t.SortOrder AS sortOrder, t.IsActive AS isActive,
       t.IsMandatory AS isMandatory,
       (SELECT COUNT(*) FROM dbo.LegalCaseSteps s WHERE s.StepTypeId = t.Id) AS usedCount
FROM dbo.LegalStepTypes t ORDER BY t.Flow DESC, t.SortOrder, t.Id;
""", cancellationToken: ct));
    }

    public async Task<int> SaveStepTypeAsync(int? id, string flow, string name, int sortOrder, bool isActive, bool isMandatory, CancellationToken ct)
    {
        if (flow is not ("Judicial" or "Execution"))
            throw new LegalRuleException("نوع المسار غير صحيح", 400);
        if (string.IsNullOrWhiteSpace(name))
            throw new LegalRuleException("اسم الخطوة مطلوب", 400);
        await using var c = await _db.OpenAsync(ct);
        if (id is int existing)
        {
            var n = await c.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.LegalStepTypes SET Name = @Name, SortOrder = @SortOrder, IsActive = @IsActive, IsMandatory = @IsMandatory WHERE Id = @Id",
                new { Id = existing, Name = name.Trim(), SortOrder = sortOrder, IsActive = isActive, IsMandatory = isMandatory }, cancellationToken: ct));
            if (n == 0)
                throw new LegalRuleException("الخطوة غير موجودة", 404);
            return existing;
        }

        return await c.ExecuteScalarAsync<int>(new CommandDefinition("""
INSERT INTO dbo.LegalStepTypes (Flow, Name, SortOrder, IsActive, IsMandatory) OUTPUT INSERTED.Id
VALUES (@Flow, @Name, CASE WHEN @SortOrder > 0 THEN @SortOrder ELSE ISNULL((SELECT MAX(SortOrder) FROM dbo.LegalStepTypes WHERE Flow = @Flow), 0) + 1 END, @IsActive, @IsMandatory);
""", new { Flow = flow, Name = name.Trim(), SortOrder = sortOrder, IsActive = isActive, IsMandatory = isMandatory }, cancellationToken: ct));
    }

    /// <summary>
    /// Removes a step from the list. Steps already logged on debtors stay on their files under the
    /// same name; they just no longer point at a list entry.
    /// </summary>
    public async Task DeleteStepTypeAsync(int id, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        await c.ExecuteAsync("UPDATE dbo.LegalCaseSteps SET StepTypeId = NULL WHERE StepTypeId = @Id", new { Id = id }, tx);
        var n = await c.ExecuteAsync("DELETE FROM dbo.LegalStepTypes WHERE Id = @Id", new { Id = id }, tx);
        if (n == 0)
            throw new LegalRuleException("الخطوة غير موجودة", 404);
        await tx.CommitAsync(ct);
    }

    /// <summary>A step is logged against the flow the debtor is currently in; the same step may repeat.</summary>
    /// <param name="lawyerId">
    /// Another lawyer for this step only. The debtor stays assigned to his own lawyer, so the next
    /// step defaults back to him. Null (or the assigned lawyer) means the assigned lawyer did it.
    /// </param>
    public async Task<int> AddStepAsync(int caseId, int stepTypeId, string? note, DateTime? date, string actor, CancellationToken ct, int? lawyerId = null)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        var flow = row.Stage switch
        {
            LegalStages.Judicial => "Judicial",
            LegalStages.Execution => "Execution",
            _ => throw new LegalRuleException("الخطوات تُضاف فقط داخل الدعوى القضائية أو التنفيذ"),
        };
        var type = await c.QueryFirstOrDefaultAsync<StepTypeRow>(
            "SELECT Flow, Name FROM dbo.LegalStepTypes WHERE Id = @Id", new { Id = stepTypeId }, tx);
        if (type is null || type.Flow != flow)
            throw new LegalRuleException("هذه الخطوة لا تخص المرحلة الحالية", 400);
        var stepDate = (date ?? IraqToday).Date;
        var substitute = lawyerId is int other && other != row.LawyerId;
        if (substitute)
        {
            var exists = await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.LegalLawyers WHERE Id = @Id", new { Id = lawyerId }, tx);
            if (exists == 0)
                throw new LegalRuleException("المحامي غير موجود", 404);
        }

        var id = await c.ExecuteScalarAsync<int>("""
INSERT INTO dbo.LegalCaseSteps (CaseId, Flow, StepTypeId, StepName, Note, StepDate, CreatedBy, LawyerId, IsSubstitute, IsDone, DoneAtUtc) OUTPUT INSERTED.Id
VALUES (@CaseId, @Flow, @StepTypeId, @Name, @Note, @Date, @Actor, @LawyerId, @Substitute, @Done, CASE WHEN @Done = 1 THEN SYSUTCDATETIME() END);
""", new
        {
            CaseId = caseId,
            Flow = flow,
            StepTypeId = stepTypeId,
            type.Name,
            Note = Blank(note),
            Date = stepDate,
            Actor = actor,
            LawyerId = substitute ? lawyerId : row.LawyerId,
            Substitute = substitute,
            // A date after today means the step is scheduled, not done yet.
            Done = stepDate <= IraqToday,
        }, tx);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>The calendar day in Iraq (UTC+3): what "today", "upcoming" and "overdue" are measured against.</summary>
    private static DateTime IraqToday => DateTime.UtcNow.AddHours(3).Date;

    /// <summary>Marks a scheduled step as done (or puts it back to scheduled).</summary>
    public async Task SetStepDoneAsync(int caseId, int stepId, bool done, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var n = await c.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.LegalCaseSteps SET IsDone = @Done, DoneAtUtc = CASE WHEN @Done = 1 THEN SYSUTCDATETIME() END
WHERE Id = @Id AND CaseId = @CaseId;
""", new { Id = stepId, CaseId = caseId, Done = done }, cancellationToken: ct));
        if (n == 0)
            throw new LegalRuleException("الخطوة غير موجودة", 404);
    }

    /// <summary>
    /// Scheduled steps that are not done yet, soonest first, overdue ones included (they come first).
    /// Optionally for one lawyer (the step's own lawyer, else the debtor's assigned one) or one debtor.
    /// </summary>
    public async Task<IEnumerable<dynamic>> UpcomingStepsAsync(int? lawyerId, int? caseId, int take, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition("""
SELECT TOP (@Take) s.Id AS id, s.CaseId AS caseId, p.FullName AS debtorName, k.Stage AS stage,
       s.Flow AS flow, s.StepName AS stepName, s.StepDate AS stepDate, s.Note AS note,
       COALESCE(s.LawyerId, k.LawyerId) AS lawyerId, l.Name AS lawyerName, s.IsSubstitute AS isSubstitute,
       DATEDIFF(DAY, CAST(DATEADD(HOUR, 3, SYSUTCDATETIME()) AS date), s.StepDate) AS daysLeft,
       (SELECT STRING_AGG(a.ArchiveCode, N' · ') WITHIN GROUP (ORDER BY a.Id) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode IS NOT NULL) AS archiveCodes
FROM dbo.LegalCaseSteps s
JOIN dbo.LegalCases k ON k.Id = s.CaseId
JOIN dbo.LegalPersons p ON p.Id = k.PersonId
LEFT JOIN dbo.LegalLawyers l ON l.Id = COALESCE(s.LawyerId, k.LawyerId)
WHERE s.IsDone = 0 AND k.Stage <> N'Closed'
  AND (@CaseId IS NULL OR s.CaseId = @CaseId)
  AND (@LawyerId IS NULL OR COALESCE(s.LawyerId, k.LawyerId) = @LawyerId)
ORDER BY s.StepDate, s.Id;
""", new { Take = Math.Clamp(take, 1, 1000), LawyerId = lawyerId, CaseId = caseId }, cancellationToken: ct));
    }

    public async Task DeleteStepAsync(int caseId, int stepId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var n = await c.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.LegalCaseSteps WHERE Id = @Id AND CaseId = @CaseId", new { Id = stepId, CaseId = caseId }, cancellationToken: ct));
        if (n == 0)
            throw new LegalRuleException("الخطوة غير موجودة", 404);
    }

    // ───────────────────────── settlement requests ─────────────────────────

    /// <summary>
    /// The debtor wants to pay everything. Before a judicial case exists the request suggests the
    /// early-settlement penalty; afterwards the existing penalties already apply, so it suggests none.
    /// </summary>
    public async Task<int> RequestSettlementAsync(int caseId, string? note, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var row = await LockCaseAsync(c, tx, caseId);
        if (row.Stage == LegalStages.Closed)
            throw new LegalRuleException("الملف مغلق");
        var pending = await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.LegalSettlementRequests WHERE CaseId = @Id AND Status = N'Pending'", new { Id = caseId }, tx);
        if (pending > 0)
            throw new LegalRuleException("يوجد طلب تسديد كامل بانتظار محاسب القانونية");
        var suggested = row.JudicialStartedAtUtc is null
            ? await SettingAsync(c, tx, LegalSettingKeys.EarlySettlementPenalty, 35_000)
            : 0;
        var id = await c.ExecuteScalarAsync<int>("""
INSERT INTO dbo.LegalSettlementRequests (PersonId, CaseId, SuggestedPenalty, Note, RequestedBy) OUTPUT INSERTED.Id
VALUES (@PersonId, @CaseId, @Suggested, @Note, @Actor);
""", new { row.PersonId, CaseId = caseId, Suggested = suggested, Note = Blank(note), Actor = actor }, tx);
        await EventAsync(c, tx, caseId, "SettlementRequest", row.Stage, row.Stage, note, actor);
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<IEnumerable<dynamic>> SettlementRequestsAsync(string? status, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition("""
SELECT r.Id AS id, r.PersonId AS personId, p.FullName AS fullName, p.Phone AS phone, r.CaseId AS caseId, k.Stage AS stage,
       r.SuggestedPenalty AS suggestedPenalty, r.Note AS note, r.RequestedBy AS requestedBy, r.RequestedAtUtc AS requestedAtUtc,
       r.Status AS status, r.ApprovedPenalty AS approvedPenalty, r.DecidedBy AS decidedBy, r.DecidedAtUtc AS decidedAtUtc,
       r.DecisionNote AS decisionNote,
       CAST(CASE WHEN k.JudicialStartedAtUtc IS NULL THEN 0 ELSE 1 END AS bit) AS judicialStarted,
       ISNULL((SELECT SUM(a.LastRemaining) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id), 0) AS remainingSnapshot
FROM dbo.LegalSettlementRequests r
JOIN dbo.LegalPersons p ON p.Id = r.PersonId
JOIN dbo.LegalCases k ON k.Id = r.CaseId
WHERE (@Status IS NULL OR r.Status = @Status)
ORDER BY CASE WHEN r.Status = N'Pending' THEN 0 ELSE 1 END, r.Id DESC;
""", new { Status = Blank(status) }, cancellationToken: ct));
    }

    /// <summary>
    /// Approval adds the (possibly edited) early-settlement penalty, but only for a debtor with no
    /// judicial case: once a case exists its own penalty already stands and nothing is added here.
    /// The تسديد itself is a normal payment afterwards.
    /// </summary>
    public async Task<int> DecideSettlementAsync(int requestId, bool approve, long penalty, string? note, string actor, CancellationToken ct)
    {
        if (penalty < 0)
            throw new LegalRuleException("مبلغ الغرامة غير صحيح", 400);
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var request = await c.QueryFirstOrDefaultAsync<RequestRow>(
            "SELECT PersonId, CaseId, Status FROM dbo.LegalSettlementRequests WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id", new { Id = requestId }, tx);
        if (request is null)
            throw new LegalRuleException("الطلب غير موجود", 404);
        if (request.Status != "Pending")
            throw new LegalRuleException("تم البت في هذا الطلب مسبقاً");
        var judicialStarted = await c.ExecuteScalarAsync<DateTime?>(
            "SELECT JudicialStartedAtUtc FROM dbo.LegalCases WHERE Id = @Id", new { Id = request.CaseId }, tx);
        if (judicialStarted != null)
            penalty = 0;
        await c.ExecuteAsync("""
UPDATE dbo.LegalSettlementRequests
SET Status = @Status, ApprovedPenalty = @Penalty, DecidedBy = @Actor, DecidedAtUtc = SYSUTCDATETIME(), DecisionNote = @Note
WHERE Id = @Id;
""", new { Id = requestId, Status = approve ? "Approved" : "Rejected", Penalty = approve ? penalty : (long?)null, Actor = actor, Note = Blank(note) }, tx);
        if (approve && penalty > 0)
        {
            await c.ExecuteAsync("""
INSERT INTO dbo.LegalCharges (PersonId, CaseId, Kind, Amount, Note, Actor)
VALUES (@PersonId, @CaseId, N'EarlySettlement', @Amount, N'غرامة تسديد كامل قبل الدعوى', @Actor);
INSERT INTO dbo.LegalRegisterTx (Register, Kind, Amount, CaseId, Note, Actor)
VALUES (N'CompanyDebt', N'EarlySettlementFee', -@Amount, @CaseId, N'أجور تسديد كامل قبل الدعوى', @Actor);
""", new { request.PersonId, request.CaseId, Amount = penalty, Actor = actor }, tx);
        }

        var stage = await c.ExecuteScalarAsync<string>("SELECT Stage FROM dbo.LegalCases WHERE Id = @Id", new { Id = request.CaseId }, tx);
        await EventAsync(c, tx, request.CaseId, approve ? "SettlementApproved" : "SettlementRejected", stage, stage,
            approve ? $"غرامة {penalty:N0}" + (string.IsNullOrWhiteSpace(note) ? "" : $" — {note}") : note, actor);
        await tx.CommitAsync(ct);
        return request.PersonId;
    }

    // ───────────────────────── payments ─────────────────────────

    public async Task<long> PenaltiesDueAsync(int personId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.ExecuteScalarAsync<long>(new CommandDefinition("""
SELECT ISNULL((SELECT SUM(Amount) FROM dbo.LegalCharges WHERE PersonId = @Id), 0)
     - ISNULL((SELECT SUM(PenaltyAmount) FROM dbo.LegalPayments WHERE PersonId = @Id), 0);
""", new { Id = personId }, cancellationToken: ct));
    }

    public sealed record PayLine(int AccountId, long Amount);

    /// <summary>What القانونية itself has collected on each branch account (the branch never sees these).</summary>
    public async Task<Dictionary<(string City, int CustomerId), long>> LegalPaidAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var rows = await c.QueryAsync<(string CityValue, int CustomerId, long Paid)>(new CommandDefinition("""
SELECT CityValue, CustomerId, SUM(Amount) AS Paid
FROM dbo.LegalPaymentLines WHERE Status = N'Posted' GROUP BY CityValue, CustomerId;
""", cancellationToken: ct));
        return rows.ToDictionary(r => (r.CityValue.ToLowerInvariant(), r.CustomerId), r => r.Paid);
    }

    /// <summary>
    /// A تسديد taken by محاسب القانونية. A legal customer has left his branch, so the money is
    /// recorded here in القانونية only: nothing is posted to the branch or its boxes.
    /// Each line is checked against the account balance (refreshed by the caller just before).
    /// </summary>
    public async Task<int> RecordPaymentAsync(int personId, IReadOnlyList<PayLine> lines, long penaltyAmount, string? note, string actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var accounts = (await c.QueryAsync<LegalAccountRow>(
            "SELECT * FROM dbo.LegalPersonAccounts WITH (UPDLOCK, ROWLOCK) WHERE PersonId = @Id", new { Id = personId }, tx)).ToList();
        if (accounts.Count == 0)
            throw new LegalRuleException("المدين غير موجود", 404);
        foreach (var line in lines)
        {
            var account = accounts.FirstOrDefault(a => a.Id == line.AccountId)
                ?? throw new LegalRuleException("أحد الحسابات لا يخص هذا المدين", 400);
            if (line.Amount > account.LastRemaining)
                throw new LegalRuleException($"المبلغ أكبر من الباقي على حساب «{account.CustomerName}» ({account.LastRemaining:N0})");
        }

        var penaltiesDue = await c.ExecuteScalarAsync<long>("""
SELECT ISNULL((SELECT SUM(Amount) FROM dbo.LegalCharges WHERE PersonId = @Id), 0)
     - ISNULL((SELECT SUM(PenaltyAmount) FROM dbo.LegalPayments WHERE PersonId = @Id), 0);
""", new { Id = personId }, tx);
        if (penaltyAmount > penaltiesDue)
            throw new LegalRuleException($"مبلغ الغرامات أكبر من المستحق ({penaltiesDue:N0})");

        var caseId = await c.ExecuteScalarAsync<int?>(
            "SELECT TOP (1) Id FROM dbo.LegalCases WHERE PersonId = @Id ORDER BY CASE WHEN Stage = N'Closed' THEN 1 ELSE 0 END, Id DESC",
            new { Id = personId }, tx);
        var debt = lines.Sum(l => l.Amount);
        var id = await c.ExecuteScalarAsync<int>("""
INSERT INTO dbo.LegalPayments (PersonId, CaseId, DebtAmount, PenaltyAmount, Note, Actor) OUTPUT INSERTED.Id
VALUES (@PersonId, @CaseId, @Debt, @Penalty, @Note, @Actor);
""", new { PersonId = personId, CaseId = caseId, Debt = debt, Penalty = penaltyAmount, Note = Blank(note), Actor = actor }, tx);
        foreach (var line in lines)
        {
            var account = accounts.First(a => a.Id == line.AccountId);
            await c.ExecuteAsync("""
INSERT INTO dbo.LegalPaymentLines (PaymentId, CityValue, CustomerId, CustomerName, Amount, Status)
VALUES (@PaymentId, @CityValue, @CustomerId, @CustomerName, @Amount, N'Posted');
UPDATE dbo.LegalPersonAccounts SET LastPaid = LastPaid + @Amount, LastRemaining = LastRemaining - @Amount WHERE Id = @AccountId;
""", new { PaymentId = id, account.CityValue, account.CustomerId, account.CustomerName, line.Amount, AccountId = account.Id }, tx);
        }

        if (caseId is int openCase)
        {
            await EventAsync(c, tx, openCase, "Payment", null, null,
                $"تسديد {debt + penaltyAmount:N0} (دين {debt:N0} + غرامات {penaltyAmount:N0})", actor);
        }

        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Everything القانونية collected, newest first: the "branch" legal customers now pay into.</summary>
    public async Task<IEnumerable<dynamic>> RecentPaymentsAsync(int take, CancellationToken ct, string? q = null)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition("""
SELECT TOP (@Take) y.Id AS id, y.PersonId AS personId, p.FullName AS fullName, y.DebtAmount AS debtAmount,
       y.PenaltyAmount AS penaltyAmount, y.Note AS note, y.Actor AS actor, y.AtUtc AS atUtc,
       CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.LegalAnnulRequests r WHERE r.PaymentId = y.Id) THEN 1 ELSE 0 END AS bit) AS isAnnul,
       (SELECT STRING_AGG(a.ArchiveCode, N' · ') WITHIN GROUP (ORDER BY a.Id) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode IS NOT NULL) AS archiveCodes
FROM dbo.LegalPayments y JOIN dbo.LegalPersons p ON p.Id = y.PersonId
WHERE (@Q IS NULL OR p.FullName LIKE N'%' + @Q + N'%' OR p.Phone LIKE N'%' + @Q + N'%'
       OR EXISTS (SELECT 1 FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode LIKE N'%' + @Q + N'%'))
ORDER BY y.Id DESC;
""", new { Take = Math.Clamp(take, 1, 2000), Q = Blank(q) }, cancellationToken: ct));
    }

    public async Task<int?> OpenCaseIdAsync(int personId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP (1) Id FROM dbo.LegalCases WHERE PersonId = @Id AND Stage <> N'Closed' ORDER BY Id DESC",
            new { Id = personId }, cancellationToken: ct));
    }

    // ───────────────────────── lawyers ─────────────────────────

    private const string LawyerSql = """
SELECT l.Id AS id, l.Name AS name, l.Branch AS branch, l.Phone AS phone, l.Notes AS notes, l.IsActive AS isActive,
       ISNULL(w.Received, 0) AS received, ISNULL(w.Fees, 0) AS feesPaid, ISNULL(w.Returned, 0) AS returned,
       ISNULL(w.Received, 0) - ISNULL(w.Fees, 0) - ISNULL(w.Returned, 0) AS balance,
       (SELECT COUNT(*) FROM dbo.LegalCases k WHERE k.LawyerId = l.Id AND k.Stage <> N'Closed') AS openCases,
       (SELECT COUNT(*) FROM dbo.LegalCases k WHERE k.LawyerId = l.Id) AS totalCases
FROM dbo.LegalLawyers l
OUTER APPLY (SELECT SUM(CASE WHEN t.Kind = N'Transfer' THEN t.Amount END) AS Received,
                    SUM(CASE WHEN t.Kind = N'Fee' THEN t.Amount END) AS Fees,
                    SUM(CASE WHEN t.Kind = N'Return' THEN t.Amount END) AS Returned
             FROM dbo.LegalLawyerTx t WHERE t.LawyerId = l.Id) w

""";

    public async Task<IEnumerable<dynamic>> LawyersAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition(LawyerSql + "ORDER BY l.IsActive DESC, l.Name;", cancellationToken: ct));
    }

    public async Task<object?> LawyerProfileAsync(int lawyerId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var lawyer = await c.QueryFirstOrDefaultAsync(new CommandDefinition(LawyerSql + "WHERE l.Id = @Id;", new { Id = lawyerId }, cancellationToken: ct));
        if (lawyer is null)
            return null;
        var cases = await c.QueryAsync(new CommandDefinition(CaseListSql + """
WHERE k.LawyerId = @Id
ORDER BY CASE k.Stage WHEN N'Recent' THEN 1 WHEN N'Archive' THEN 2 WHEN N'Judicial' THEN 3 WHEN N'Decisions' THEN 4 WHEN N'Execution' THEN 5 ELSE 6 END, k.Id DESC;
""", new { Id = lawyerId }, cancellationToken: ct));
        var transactions = await c.QueryAsync(new CommandDefinition("""
SELECT t.Id AS id, t.Kind AS kind, t.Amount AS amount, t.CaseId AS caseId, p.FullName AS debtorName,
       t.OutsideAssignment AS outsideAssignment, t.Note AS note,
       CAST(CASE WHEN t.ImagePath IS NULL THEN 0 ELSE 1 END AS bit) AS hasImage, t.Actor AS actor, t.AtUtc AS atUtc
FROM dbo.LegalLawyerTx t
LEFT JOIN dbo.LegalCases k ON k.Id = t.CaseId
LEFT JOIN dbo.LegalPersons p ON p.Id = k.PersonId
WHERE t.LawyerId = @Id ORDER BY t.Id DESC;
""", new { Id = lawyerId }, cancellationToken: ct));
        // Steps he handled for debtors that are assigned to another lawyer.
        var substituteSteps = await c.QueryAsync(new CommandDefinition("""
SELECT s.Id AS id, s.CaseId AS caseId, p.FullName AS debtorName, s.Flow AS flow, s.StepName AS stepName,
       s.StepDate AS stepDate, s.Note AS note, al.Name AS assignedLawyerName
FROM dbo.LegalCaseSteps s
JOIN dbo.LegalCases k ON k.Id = s.CaseId
JOIN dbo.LegalPersons p ON p.Id = k.PersonId
LEFT JOIN dbo.LegalLawyers al ON al.Id = k.LawyerId
WHERE s.LawyerId = @Id AND s.IsSubstitute = 1
ORDER BY s.StepDate DESC, s.Id DESC;
""", new { Id = lawyerId }, cancellationToken: ct));
        return new { lawyer, cases, transactions, substituteSteps };
    }

    public async Task<int> SaveLawyerAsync(int? id, string name, string? branch, string? phone, string? notes, bool isActive, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new LegalRuleException("اسم المحامي مطلوب", 400);
        await using var c = await _db.OpenAsync(ct);
        if (id is int existing)
        {
            var n = await c.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.LegalLawyers SET Name = @Name, Branch = @Branch, Phone = @Phone, Notes = @Notes, IsActive = @IsActive WHERE Id = @Id",
                new { Id = existing, Name = name.Trim(), Branch = Blank(branch), Phone = Blank(phone), Notes = Blank(notes), IsActive = isActive }, cancellationToken: ct));
            if (n == 0)
                throw new LegalRuleException("المحامي غير موجود", 404);
            return existing;
        }

        return await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "INSERT INTO dbo.LegalLawyers (Name, Branch, Phone, Notes, IsActive) OUTPUT INSERTED.Id VALUES (@Name, @Branch, @Phone, @Notes, @IsActive)",
            new { Name = name.Trim(), Branch = Blank(branch), Phone = Blank(phone), Notes = Blank(notes), IsActive = isActive }, cancellationToken: ct));
    }

    /// <summary>Wallet top-up: cash leaves قاصة القانونية and lands in the lawyer's wallet.</summary>
    public async Task TransferToLawyerAsync(int lawyerId, long amount, string? note, string actor, CancellationToken ct)
    {
        if (amount <= 0)
            throw new LegalRuleException("المبلغ غير صحيح", 400);
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var name = await c.ExecuteScalarAsync<string>("SELECT Name FROM dbo.LegalLawyers WHERE Id = @Id", new { Id = lawyerId }, tx)
            ?? throw new LegalRuleException("المحامي غير موجود", 404);
        await c.ExecuteAsync("""
INSERT INTO dbo.LegalLawyerTx (LawyerId, Kind, Amount, Note, Actor) VALUES (@LawyerId, N'Transfer', @Amount, @Note, @Actor);
INSERT INTO dbo.LegalRegisterTx (Register, Kind, Amount, LawyerId, Note, Actor) VALUES (N'Cash', N'LawyerTransfer', -@Amount, @LawyerId, @RegisterNote, @Actor);
""", new { LawyerId = lawyerId, Amount = amount, Note = Blank(note), RegisterNote = $"تحويل إلى محفظة المحامي {name}" + (string.IsNullOrWhiteSpace(note) ? "" : $" — {note!.Trim()}"), Actor = actor }, tx);
        await tx.CommitAsync(ct);
    }

    /// <summary>The lawyer hands unused wallet money back to قاصة القانونية.</summary>
    public async Task ReturnFromLawyerAsync(int lawyerId, long amount, string? note, string actor, CancellationToken ct)
    {
        if (amount <= 0)
            throw new LegalRuleException("المبلغ غير صحيح", 400);
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var name = await c.ExecuteScalarAsync<string>("SELECT Name FROM dbo.LegalLawyers WHERE Id = @Id", new { Id = lawyerId }, tx)
            ?? throw new LegalRuleException("المحامي غير موجود", 404);
        var balance = await c.ExecuteScalarAsync<long>("""
SELECT ISNULL(SUM(CASE WHEN Kind = N'Transfer' THEN Amount ELSE -Amount END), 0)
FROM dbo.LegalLawyerTx WITH (UPDLOCK, HOLDLOCK) WHERE LawyerId = @Id;
""", new { Id = lawyerId }, tx);
        if (amount > balance)
            throw new LegalRuleException($"رصيد محفظة المحامي ({balance:N0}) أقل من المبلغ المُرجع");
        await c.ExecuteAsync("""
INSERT INTO dbo.LegalLawyerTx (LawyerId, Kind, Amount, Note, Actor) VALUES (@LawyerId, N'Return', @Amount, @Note, @Actor);
INSERT INTO dbo.LegalRegisterTx (Register, Kind, Amount, LawyerId, Note, Actor) VALUES (N'Cash', N'LawyerReturn', @Amount, @LawyerId, @RegisterNote, @Actor);
""", new { LawyerId = lawyerId, Amount = amount, Note = Blank(note), RegisterNote = $"إرجاع من محفظة المحامي {name}", Actor = actor }, tx);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// A fee the lawyer paid for a debtor, taken from the lawyer's wallet. Paying for a debtor that is
    /// not assigned to this lawyer is unusual, so it needs the explicit outsideAssignment switch.
    /// The wallet may go negative: that is the lawyer paying from his own pocket until reimbursed.
    /// </summary>
    public async Task<int> AddFeeAsync(int lawyerId, int caseId, long amount, string? note, bool outsideAssignment, string? imagePath, string actor, CancellationToken ct)
    {
        if (amount <= 0)
            throw new LegalRuleException("المبلغ غير صحيح", 400);
        if (string.IsNullOrWhiteSpace(note))
            throw new LegalRuleException("اكتب ملاحظة توضح سبب الرسم", 400);
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var exists = await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.LegalLawyers WHERE Id = @Id", new { Id = lawyerId }, tx);
        if (exists == 0)
            throw new LegalRuleException("المحامي غير موجود", 404);
        var row = await LockCaseAsync(c, tx, caseId);
        var assigned = row.LawyerId == lawyerId;
        if (!assigned && !outsideAssignment)
            throw new LegalRuleException("هذا المدين غير مسند لهذا المحامي. فعّل خيار «مدين غير مسند للمحامي» للمتابعة.", 409, "NotAssigned");
        var id = await c.ExecuteScalarAsync<int>("""
INSERT INTO dbo.LegalLawyerTx (LawyerId, Kind, Amount, CaseId, OutsideAssignment, Note, ImagePath, Actor) OUTPUT INSERTED.Id
VALUES (@LawyerId, N'Fee', @Amount, @CaseId, @Outside, @Note, @ImagePath, @Actor);
""", new { LawyerId = lawyerId, Amount = amount, CaseId = caseId, Outside = !assigned, Note = note.Trim(), ImagePath = imagePath, Actor = actor }, tx);
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<string?> FeeImagePathAsync(int txId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT ImagePath FROM dbo.LegalLawyerTx WHERE Id = @Id", new { Id = txId }, cancellationToken: ct));
    }

    // ───────────────────────── registers ─────────────────────────

    /// <summary>
    /// القاصة: one cash box. Inside it sits the account with قلعة الضمان: what the team received
    /// from the company, and what it has paid back through its work (200,000 per judicial case,
    /// 150,000 per التنفيذ, and the early-settlement amount when a debtor agrees to pay before a case).
    /// The two are kept as separate ledgers in the table ("Cash" moves money, "CompanyDebt" is the
    /// account with the company) but are shown as one register.
    /// </summary>
    public async Task<object> RegistersAsync(int take, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var balances = (await c.QueryAsync<(string Register, long Balance)>(new CommandDefinition(
            "SELECT Register, ISNULL(SUM(Amount), 0) AS Balance FROM dbo.LegalRegisterTx GROUP BY Register", cancellationToken: ct)))
            .ToDictionary(r => r.Register, r => r.Balance);
        var wallets = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT ISNULL(SUM(CASE WHEN Kind = N'Transfer' THEN Amount ELSE -Amount END), 0) FROM dbo.LegalLawyerTx", cancellationToken: ct));
        var totals = await c.QueryFirstAsync(new CommandDefinition("""
SELECT ISNULL(SUM(CASE WHEN Register = N'CompanyDebt' AND Kind = N'CompanyFunds' THEN Amount END), 0) AS companyFunds,
       ISNULL(-SUM(CASE WHEN Kind = N'JudicialFee' THEN Amount END), 0) AS judicialFees,
       ISNULL(SUM(CASE WHEN Kind = N'JudicialFee' THEN 1 END), 0) AS judicialCount,
       ISNULL(-SUM(CASE WHEN Kind = N'ExecutionFee' THEN Amount END), 0) AS executionFees,
       ISNULL(SUM(CASE WHEN Kind = N'ExecutionFee' THEN 1 END), 0) AS executionCount,
       ISNULL(-SUM(CASE WHEN Kind = N'EarlySettlementFee' THEN Amount END), 0) AS earlyFees,
       ISNULL(SUM(CASE WHEN Kind = N'EarlySettlementFee' THEN 1 END), 0) AS earlyCount,
       ISNULL(-SUM(CASE WHEN Register = N'Cash' AND Kind = N'Expense' THEN Amount END), 0) AS expenses
FROM dbo.LegalRegisterTx;
""", cancellationToken: ct));
        // One row per movement: receiving company money is stored in both ledgers, shown once.
        var transactions = await c.QueryAsync(new CommandDefinition("""
SELECT TOP (@Take) t.Id AS id, t.Kind AS kind,
       CASE WHEN t.Register = N'Cash' THEN t.Amount END AS cashAmount,
       CASE WHEN t.Register = N'CompanyDebt' OR t.Kind = N'CompanyFunds' THEN t.Amount END AS companyAmount,
       t.CaseId AS caseId, p.FullName AS debtorName, t.LawyerId AS lawyerId, l.Name AS lawyerName,
       t.Note AS note, t.Actor AS actor, t.AtUtc AS atUtc
FROM dbo.LegalRegisterTx t
LEFT JOIN dbo.LegalCases k ON k.Id = t.CaseId
LEFT JOIN dbo.LegalPersons p ON p.Id = k.PersonId
LEFT JOIN dbo.LegalLawyers l ON l.Id = t.LawyerId
WHERE NOT (t.Register = N'CompanyDebt' AND t.Kind = N'CompanyFunds')
ORDER BY t.Id DESC;
""", new { Take = Math.Clamp(take, 1, 2000) }, cancellationToken: ct));
        var debt = balances.GetValueOrDefault(LegalRegisters.CompanyDebt);
        long judicial = (long)totals.judicialFees, execution = (long)totals.executionFees, early = (long)totals.earlyFees;
        return new
        {
            cash = balances.GetValueOrDefault(LegalRegisters.Cash),
            lawyerWallets = wallets,
            expenses = (long)totals.expenses,
            // The account with قلعة الضمان.
            companyFunds = (long)totals.companyFunds,
            paidBack = judicial + execution + early,
            judicialFees = judicial,
            judicialCount = (int)totals.judicialCount,
            executionFees = execution,
            executionCount = (int)totals.executionCount,
            earlyFees = early,
            earlyCount = (int)totals.earlyCount,
            // Received minus paid back. Negative means the company owes the team the difference.
            companyDebt = debt,
            dueToLegalTeam = debt < 0 ? -debt : 0,
            feesEarned = judicial + execution + early,
            transactions,
        };
    }

    /// <summary>Money handed over by the company: the cash box grows and so does the debt to the company.</summary>
    public async Task AddCompanyFundsAsync(long amount, string? note, string actor, CancellationToken ct)
    {
        if (amount <= 0)
            throw new LegalRuleException("المبلغ غير صحيح", 400);
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        await c.ExecuteAsync("""
INSERT INTO dbo.LegalRegisterTx (Register, Kind, Amount, Note, Actor) VALUES
    (N'Cash', N'CompanyFunds', @Amount, @Note, @Actor),
    (N'CompanyDebt', N'CompanyFunds', @Amount, @Note, @Actor);
""", new { Amount = amount, Note = Blank(note) ?? "مبلغ مستلم من الشركة", Actor = actor }, tx);
        await tx.CommitAsync(ct);
    }

    public async Task AddExpenseAsync(long amount, string? note, string actor, CancellationToken ct)
    {
        if (amount <= 0)
            throw new LegalRuleException("المبلغ غير صحيح", 400);
        if (string.IsNullOrWhiteSpace(note))
            throw new LegalRuleException("اكتب سبب الصرف", 400);
        await using var c = await _db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        await c.ExecuteAsync(
            "INSERT INTO dbo.LegalRegisterTx (Register, Kind, Amount, Note, Actor) VALUES (N'Cash', N'Expense', -@Amount, @Note, @Actor)",
            new { Amount = amount, Note = note.Trim(), Actor = actor }, tx);
        await tx.CommitAsync(ct);
    }

    // ───────────────────────── overview ─────────────────────────

    /// <summary>
    /// Debtors that were sent to the legal app and have made no progress for <paramref name="days"/> days
    /// or more. Progress = moving to another station, or a step being added or marked done.
    /// Closed files and debtors already in التنفيذ are left out. Longest-idle first.
    /// </summary>
    public async Task<IEnumerable<dynamic>> StalledAsync(int days, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await c.QueryAsync(new CommandDefinition("""
SELECT k.Id AS caseId, p.Id AS personId, p.FullName AS fullName, p.Phone AS phone,
       k.Stage AS stage, k.IsAnnulled AS isAnnulled, l.Name AS lawyerName, k.SentAtUtc AS sentAtUtc,
       lp.LastProgressUtc AS lastProgressUtc,
       DATEDIFF(DAY, lp.LastProgressUtc, SYSUTCDATETIME()) AS idleDays,
       st.LastStepName AS lastStepName,
       ISNULL((SELECT SUM(a.LastRemaining) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id), 0) AS remainingSnapshot,
       (SELECT STRING_AGG(a.ArchiveCode, N' · ') WITHIN GROUP (ORDER BY a.Id) FROM dbo.LegalPersonAccounts a WHERE a.PersonId = p.Id AND a.ArchiveCode IS NOT NULL) AS archiveCodes
FROM dbo.LegalCases k
JOIN dbo.LegalPersons p ON p.Id = k.PersonId
LEFT JOIN dbo.LegalLawyers l ON l.Id = k.LawyerId
OUTER APPLY (SELECT MAX(s.CreatedAtUtc) AS LastAdded, MAX(s.DoneAtUtc) AS LastDone,
                    (SELECT TOP (1) s2.StepName FROM dbo.LegalCaseSteps s2 WHERE s2.CaseId = k.Id ORDER BY s2.CreatedAtUtc DESC, s2.Id DESC) AS LastStepName
             FROM dbo.LegalCaseSteps s WHERE s.CaseId = k.Id) st
CROSS APPLY (SELECT MAX(v) AS LastProgressUtc
             FROM (VALUES (k.SentAtUtc), (k.StageEnteredAtUtc), (st.LastAdded), (st.LastDone)) x(v)) lp
WHERE k.Stage NOT IN (N'Closed', N'Execution')
  AND DATEDIFF(DAY, lp.LastProgressUtc, SYSUTCDATETIME()) >= @Days
ORDER BY lp.LastProgressUtc, k.Id;
""", new { Days = Math.Max(0, days) }, cancellationToken: ct));
    }

    /// <summary>
    /// القاصة on the company dashboard: what محاسب القانونية collected from debtors (with how much of
    /// it was مبالغ الابطال) and what the company put into the legal app's قاصة (دين قلعة الضمان).
    /// </summary>
    public async Task<object> DeskCashboxAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var collected = await c.QueryFirstAsync(new CommandDefinition("""
WITH y AS (
    SELECT p.Id, p.PersonId, p.DebtAmount, p.PenaltyAmount, p.DebtAmount + p.PenaltyAmount AS Total,
           CASE WHEN EXISTS (SELECT 1 FROM dbo.LegalAnnulRequests r WHERE r.PaymentId = p.Id) THEN 1 ELSE 0 END AS IsAnnul
    FROM dbo.LegalPayments p)
SELECT ISNULL(SUM(Total), 0) AS total, COUNT(*) AS payments, COUNT(DISTINCT PersonId) AS customers,
       ISNULL(SUM(DebtAmount), 0) AS debt, ISNULL(SUM(PenaltyAmount), 0) AS penalties,
       ISNULL(SUM(CASE WHEN IsAnnul = 1 THEN Total END), 0) AS annulTotal,
       ISNULL(SUM(IsAnnul), 0) AS annulPayments,
       COUNT(DISTINCT CASE WHEN IsAnnul = 1 THEN PersonId END) AS annulCustomers,
       ISNULL(SUM(CASE WHEN IsAnnul = 0 THEN Total END), 0) AS directTotal,
       ISNULL(SUM(1 - IsAnnul), 0) AS directPayments,
       COUNT(DISTINCT CASE WHEN IsAnnul = 0 THEN PersonId END) AS directCustomers
FROM y;
""", cancellationToken: ct));
        var funds = (await c.QueryAsync(new CommandDefinition("""
SELECT Id AS id, Amount AS amount, Note AS note, Actor AS actor, AtUtc AS atUtc
FROM dbo.LegalRegisterTx WHERE Register = N'CompanyDebt' AND Kind = N'CompanyFunds' ORDER BY Id DESC;
""", cancellationToken: ct))).ToList();
        var paidBack = await c.QueryFirstAsync(new CommandDefinition("""
SELECT ISNULL(-SUM(CASE WHEN Kind = N'JudicialFee' THEN Amount END), 0) AS judicialFees,
       ISNULL(SUM(CASE WHEN Kind = N'JudicialFee' THEN 1 END), 0) AS judicialCount,
       ISNULL(-SUM(CASE WHEN Kind = N'ExecutionFee' THEN Amount END), 0) AS executionFees,
       ISNULL(SUM(CASE WHEN Kind = N'ExecutionFee' THEN 1 END), 0) AS executionCount,
       ISNULL(-SUM(CASE WHEN Kind = N'EarlySettlementFee' THEN Amount END), 0) AS earlyFees,
       ISNULL(SUM(CASE WHEN Kind = N'EarlySettlementFee' THEN 1 END), 0) AS earlyCount
FROM dbo.LegalRegisterTx WHERE Register = N'CompanyDebt';
""", cancellationToken: ct));
        var payments = await RecentPaymentsAsync(2000, ct);
        long fundsTotal = funds.Sum(f => (long)f.amount);
        long back = (long)paidBack.judicialFees + (long)paidBack.executionFees + (long)paidBack.earlyFees;
        return new
        {
            collected,
            payments,
            funds = new { total = fundsTotal, count = funds.Count, records = funds },
            paidBack = new
            {
                total = back,
                paidBack.judicialFees, paidBack.judicialCount,
                paidBack.executionFees, paidBack.executionCount,
                paidBack.earlyFees, paidBack.earlyCount,
            },
            companyDebt = fundsTotal - back,
        };
    }

    public async Task<object> OverviewAsync(CancellationToken ct)
    {
        var stages = (await StageCountsAsync(ct)).ToList();
        var registers = await RegistersAsync(15, ct);
        await using var c = await _db.OpenAsync(ct);
        var money = await c.QueryFirstAsync(new CommandDefinition("""
SELECT (SELECT COUNT(*) FROM dbo.LegalPersons) AS persons,
       (SELECT COUNT(*) FROM dbo.LegalPersons p WHERE NOT EXISTS (SELECT 1 FROM dbo.LegalCases k WHERE k.PersonId = p.Id)) AS notSent,
       (SELECT COUNT(*) FROM dbo.LegalPersonAccounts) AS accounts,
       ISNULL((SELECT SUM(LastRemaining) FROM dbo.LegalPersonAccounts), 0) AS remainingSnapshot,
       ISNULL((SELECT SUM(Amount) FROM dbo.LegalCharges), 0) AS penaltiesCharged,
       ISNULL((SELECT SUM(PenaltyAmount) FROM dbo.LegalPayments), 0) AS penaltiesPaid,
       ISNULL((SELECT SUM(DebtAmount) FROM dbo.LegalPayments), 0) AS debtCollected,
       (SELECT COUNT(*) FROM dbo.LegalCases WHERE JudicialStartedAtUtc IS NOT NULL) AS judicialStarted,
       (SELECT COUNT(*) FROM dbo.LegalCases WHERE ExecutionStartedAtUtc IS NOT NULL) AS executionStarted,
       (SELECT COUNT(*) FROM dbo.LegalCases WHERE IsAnnulled = 1 AND Stage <> N'Closed') AS annulled,
       (SELECT COUNT(*) FROM dbo.LegalSettlementRequests WHERE Status = N'Pending') AS pendingSettlements,
       (SELECT COUNT(*) FROM dbo.LegalAnnulRequests WHERE Status = N'Pending') AS pendingAnnuls,
       ISNULL((SELECT SUM(Amount) FROM dbo.LegalLawyerTx WHERE Kind = N'Fee'), 0) AS lawyerFees;
""", cancellationToken: ct));
        var payments = await RecentPaymentsAsync(15, ct);
        return new { stages, registers, money, payments };
    }

    private sealed class SeriesRow
    {
        public int MaxBox { get; set; }
        public int MaxSeq { get; set; }
    }

    private sealed class StepTypeRow
    {
        public string Flow { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class RequestRow
    {
        public int PersonId { get; set; }
        public int CaseId { get; set; }
        public string Status { get; set; } = "";
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
