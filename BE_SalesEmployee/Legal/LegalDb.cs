using BE_SalesEmployee.Sales.Authorization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_SalesEmployee.Legal;

/// <summary>The linear path every debtor follows inside the legal app.</summary>
public static class LegalStages
{
    public const string Recent = "Recent";        // الاسماء المضافة مؤخرا
    public const string Archive = "Archive";      // ارشيف القانونية
    public const string Judicial = "Judicial";    // الدعوى القضائية
    public const string Decisions = "Decisions";  // القرارات
    public const string Execution = "Execution";  // التنفيذ
    public const string Closed = "Closed";        // مغلق

    public static readonly string[] Open = [Recent, Archive, Judicial, Decisions, Execution];

    public static bool IsKnown(string? stage) => stage == Closed || Open.Contains(stage);

    /// <summary>
    /// Forward: one station at a time. Back: to any earlier station, at any time.
    /// Going back undoes nothing that was already recorded: fees charged, penalties, archive codes
    /// and logged steps all stay, and returning forward does not charge the same fee twice.
    /// </summary>
    public static bool CanMove(string from, string to)
    {
        var fromIndex = Array.IndexOf(Open, from);
        var toIndex = Array.IndexOf(Open, to);
        if (fromIndex < 0 || toIndex < 0)
            return false;
        return toIndex == fromIndex + 1 || toIndex < fromIndex;
    }

    public static string Label(string? stage) => stage switch
    {
        Recent => "الاسماء المضافة مؤخرا",
        Archive => "ارشيف القانونية",
        Judicial => "الدعوى القضائية",
        Decisions => "القرارات",
        Execution => "التنفيذ",
        Closed => "مغلق",
        _ => stage ?? "",
    };
}

public static class LegalRegisters
{
    public const string CompanyDebt = "CompanyDebt"; // دين قلعة الضمان
    public const string Cash = "Cash";               // قاصة القانونية
}

public static class LegalSettingKeys
{
    public const string JudicialFee = "JudicialFee";
    public const string ExecutionFee = "ExecutionFee";
    public const string EarlySettlementPenalty = "EarlySettlementPenalty";
}

/// <summary>A rule of the legal flow was broken; the message is shown to the user as-is.</summary>
public sealed class LegalRuleException : Exception
{
    public LegalRuleException(string message, int status = 409, string? code = null) : base(message)
    {
        Status = status;
        Code = code;
    }

    public int Status { get; }
    public string? Code { get; }
}

/// <summary>
/// Legal tables live in ConnectionStrings:Legal (falls back to the gateway database).
/// The schema is created on first use, like the other gateway stores.
/// </summary>
public sealed class LegalDb
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public LegalDb(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Legal")
            ?? configuration.GetConnectionString("SalesGateway")
            ?? "";
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new LegalRuleException("قاعدة بيانات القانونية غير مهيأة (ConnectionStrings:Legal).", 503);

        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        if (!_ready)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (!_ready)
                {
                    await connection.ExecuteAsync(new CommandDefinition(Schema, cancellationToken: ct));
                    await SeedAsync(connection, ct);
                    _ready = true;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        return connection;
    }

    /// <summary>
    /// First run only: sets each كود ارشيف القانونية series to where the old legal system stopped,
    /// so new codes continue after the old ones, and adds the old system's lawyers.
    /// See <see cref="LegalArchiveSeed"/>.
    /// </summary>
    private static async Task SeedAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        if (await FirstTimeAsync(connection, tx, "Seed.ArchiveSeries"))
        {
            await connection.ExecuteAsync("""
UPDATE dbo.LegalArchiveSeries SET MaxBox = @MaxBox, MaxSeq = @MaxSeq
WHERE BranchCode = @Branch AND Year2 = @Year2 AND Slice = @Slice
  AND (MaxBox < @MaxBox OR (MaxBox = @MaxBox AND MaxSeq < @MaxSeq));
IF @@ROWCOUNT = 0 AND NOT EXISTS (SELECT 1 FROM dbo.LegalArchiveSeries WHERE BranchCode = @Branch AND Year2 = @Year2 AND Slice = @Slice)
    INSERT INTO dbo.LegalArchiveSeries (BranchCode, Year2, Slice, MaxBox, MaxSeq) VALUES (@Branch, @Year2, @Slice, @MaxBox, @MaxSeq);
""", LegalArchiveSeed.Series.Select(s => new { s.Branch, s.Year2, s.Slice, s.MaxBox, s.MaxSeq }), tx);
        }

        if (await FirstTimeAsync(connection, tx, "Seed.Lawyers"))
        {
            await connection.ExecuteAsync("""
IF NOT EXISTS (SELECT 1 FROM dbo.LegalLawyers WHERE Name = @Name)
    INSERT INTO dbo.LegalLawyers (Name, Branch) VALUES (@Name, @Branch);
""", LegalArchiveSeed.Lawyers.Select(l => new { l.Name, l.Branch }), tx);
        }

        await tx.CommitAsync(ct);
    }

    /// <summary>True once per database for a seed marker (kept in its own table, away from the app's settings).</summary>
    private static async Task<bool> FirstTimeAsync(SqlConnection connection, SqlTransaction tx, string marker) =>
        await connection.ExecuteScalarAsync<int>("""
IF EXISTS (SELECT 1 FROM dbo.LegalSeedLog WITH (UPDLOCK, HOLDLOCK) WHERE Marker = @Marker) SELECT 0
ELSE BEGIN INSERT INTO dbo.LegalSeedLog (Marker) VALUES (@Marker); SELECT 1 END
""", new { Marker = marker }, tx) == 1;

    private const string Schema = """
IF OBJECT_ID(N'dbo.LegalPersons', N'U') IS NULL
CREATE TABLE dbo.LegalPersons (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(255) NOT NULL,
    Phone NVARCHAR(100) NULL,
    Address NVARCHAR(500) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedBy NVARCHAR(200) NULL,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalPersons_Created DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.LegalPersonAccounts', N'U') IS NULL
CREATE TABLE dbo.LegalPersonAccounts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PersonId INT NOT NULL REFERENCES dbo.LegalPersons(Id),
    CityValue NVARCHAR(100) NOT NULL,
    CityName NVARCHAR(200) NULL,
    CustomerId INT NOT NULL,
    CustomerName NVARCHAR(255) NULL,
    ListName NVARCHAR(255) NULL,
    LastTotal BIGINT NOT NULL CONSTRAINT DF_LegalPersonAccounts_Total DEFAULT (0),
    LastPaid BIGINT NOT NULL CONSTRAINT DF_LegalPersonAccounts_Paid DEFAULT (0),
    LastRemaining BIGINT NOT NULL CONSTRAINT DF_LegalPersonAccounts_Remaining DEFAULT (0),
    LastSyncedAtUtc DATETIME2 NULL,
    AddedBy NVARCHAR(200) NULL,
    AddedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalPersonAccounts_Added DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_LegalPersonAccounts_Customer UNIQUE (CityValue, CustomerId)
);

IF OBJECT_ID(N'dbo.LegalLawyers', N'U') IS NULL
CREATE TABLE dbo.LegalLawyers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Phone NVARCHAR(100) NULL,
    Notes NVARCHAR(MAX) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_LegalLawyers_Active DEFAULT (1),
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalLawyers_Created DEFAULT (SYSUTCDATETIME())
);

IF COL_LENGTH(N'dbo.LegalLawyers', N'Branch') IS NULL
    ALTER TABLE dbo.LegalLawyers ADD Branch NVARCHAR(200) NULL;

IF OBJECT_ID(N'dbo.LegalSeedLog', N'U') IS NULL
CREATE TABLE dbo.LegalSeedLog (
    Marker NVARCHAR(100) NOT NULL PRIMARY KEY,
    AtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalSeedLog_At DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.LegalCases', N'U') IS NULL
CREATE TABLE dbo.LegalCases (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PersonId INT NOT NULL REFERENCES dbo.LegalPersons(Id),
    Stage NVARCHAR(30) NOT NULL,
    LawyerId INT NULL REFERENCES dbo.LegalLawyers(Id),
    IsAnnulled BIT NOT NULL CONSTRAINT DF_LegalCases_Annulled DEFAULT (0),
    AnnulledAtUtc DATETIME2 NULL,
    AnnulNote NVARCHAR(MAX) NULL,
    DecisionText NVARCHAR(MAX) NULL,
    DecisionDate DATE NULL,
    Note NVARCHAR(MAX) NULL,
    SentBy NVARCHAR(200) NULL,
    SentAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalCases_Sent DEFAULT (SYSUTCDATETIME()),
    StageEnteredAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalCases_StageEntered DEFAULT (SYSUTCDATETIME()),
    JudicialStartedAtUtc DATETIME2 NULL,
    ExecutionStartedAtUtc DATETIME2 NULL,
    ClosedAtUtc DATETIME2 NULL,
    CloseReason NVARCHAR(500) NULL
);

IF OBJECT_ID(N'dbo.LegalStepTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LegalStepTypes (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Flow NVARCHAR(20) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_LegalStepTypes_Sort DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_LegalStepTypes_Active DEFAULT (1)
    );
    INSERT INTO dbo.LegalStepTypes (Flow, Name, SortOrder) VALUES
        (N'Judicial', N'اقامة دعوى', 1), (N'Judicial', N'تبليغ', 2),
        (N'Judicial', N'مرافعة', 3), (N'Judicial', N'تعميم جريدة', 4),
        (N'Execution', N'فتح اضبارة تنفيذية', 1), (N'Execution', N'تبليغ تنفيذي', 2),
        (N'Execution', N'حجز', 3), (N'Execution', N'منع سفر', 4);
END;

IF OBJECT_ID(N'dbo.LegalCaseSteps', N'U') IS NULL
CREATE TABLE dbo.LegalCaseSteps (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CaseId INT NOT NULL REFERENCES dbo.LegalCases(Id),
    Flow NVARCHAR(20) NOT NULL,
    StepTypeId INT NULL,
    StepName NVARCHAR(200) NOT NULL,
    Note NVARCHAR(MAX) NULL,
    StepDate DATE NOT NULL,
    CreatedBy NVARCHAR(200) NULL,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalCaseSteps_Created DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.LegalCaseEvents', N'U') IS NULL
CREATE TABLE dbo.LegalCaseEvents (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CaseId INT NOT NULL REFERENCES dbo.LegalCases(Id),
    Kind NVARCHAR(40) NOT NULL,
    FromStage NVARCHAR(30) NULL,
    ToStage NVARCHAR(30) NULL,
    Note NVARCHAR(MAX) NULL,
    Actor NVARCHAR(200) NULL,
    AtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalCaseEvents_At DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.LegalLawyerTx', N'U') IS NULL
CREATE TABLE dbo.LegalLawyerTx (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LawyerId INT NOT NULL REFERENCES dbo.LegalLawyers(Id),
    Kind NVARCHAR(20) NOT NULL,           -- Transfer (+) | Fee (-) | Return (-)
    Amount BIGINT NOT NULL,               -- always positive
    CaseId INT NULL REFERENCES dbo.LegalCases(Id),
    OutsideAssignment BIT NOT NULL CONSTRAINT DF_LegalLawyerTx_Outside DEFAULT (0),
    Note NVARCHAR(MAX) NULL,
    ImagePath NVARCHAR(400) NULL,
    Actor NVARCHAR(200) NULL,
    AtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalLawyerTx_At DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.LegalRegisterTx', N'U') IS NULL
CREATE TABLE dbo.LegalRegisterTx (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Register NVARCHAR(20) NOT NULL,       -- CompanyDebt | Cash
    Kind NVARCHAR(30) NOT NULL,
    Amount BIGINT NOT NULL,               -- signed
    CaseId INT NULL,
    LawyerId INT NULL,
    Note NVARCHAR(MAX) NULL,
    Actor NVARCHAR(200) NULL,
    AtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalRegisterTx_At DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.LegalCharges', N'U') IS NULL
CREATE TABLE dbo.LegalCharges (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PersonId INT NOT NULL REFERENCES dbo.LegalPersons(Id),
    CaseId INT NULL REFERENCES dbo.LegalCases(Id),
    Kind NVARCHAR(30) NOT NULL,           -- Judicial | Execution | EarlySettlement
    Amount BIGINT NOT NULL,
    Note NVARCHAR(MAX) NULL,
    Actor NVARCHAR(200) NULL,
    AtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalCharges_At DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.LegalPayments', N'U') IS NULL
CREATE TABLE dbo.LegalPayments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PersonId INT NOT NULL REFERENCES dbo.LegalPersons(Id),
    CaseId INT NULL,
    DebtAmount BIGINT NOT NULL,           -- posted to branch accounts
    PenaltyAmount BIGINT NOT NULL,        -- taken against LegalCharges
    Note NVARCHAR(MAX) NULL,
    Actor NVARCHAR(200) NULL,
    AtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalPayments_At DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.LegalPaymentLines', N'U') IS NULL
CREATE TABLE dbo.LegalPaymentLines (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PaymentId INT NOT NULL REFERENCES dbo.LegalPayments(Id),
    CityValue NVARCHAR(100) NOT NULL,
    CustomerId INT NOT NULL,
    CustomerName NVARCHAR(255) NULL,
    Amount BIGINT NOT NULL,
    BranchPaymentId INT NULL,
    Status NVARCHAR(20) NOT NULL,         -- Posted | Failed
    Error NVARCHAR(1000) NULL
);

IF OBJECT_ID(N'dbo.LegalSettlementRequests', N'U') IS NULL
CREATE TABLE dbo.LegalSettlementRequests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PersonId INT NOT NULL REFERENCES dbo.LegalPersons(Id),
    CaseId INT NOT NULL REFERENCES dbo.LegalCases(Id),
    SuggestedPenalty BIGINT NOT NULL,
    Note NVARCHAR(MAX) NULL,
    RequestedBy NVARCHAR(200) NULL,
    RequestedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalSettlementRequests_At DEFAULT (SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_LegalSettlementRequests_Status DEFAULT (N'Pending'),
    ApprovedPenalty BIGINT NULL,
    DecidedBy NVARCHAR(200) NULL,
    DecidedAtUtc DATETIME2 NULL,
    DecisionNote NVARCHAR(MAX) NULL
);

IF OBJECT_ID(N'dbo.LegalSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LegalSettings (
        [Key] NVARCHAR(60) PRIMARY KEY,
        [Value] BIGINT NOT NULL
    );
    INSERT INTO dbo.LegalSettings ([Key], [Value]) VALUES
        (N'JudicialFee', 200000), (N'ExecutionFee', 150000), (N'EarlySettlementPenalty', 35000);
END;

-- ───── Added after the first version. Kept below every CREATE TABLE so a new database gets its
-- tables first: a column can only be added to a table that already exists. ─────

-- كود ارشيف القانونية: one per sale (branch account), issued when the debtor enters the legal archive.
IF COL_LENGTH(N'dbo.LegalPersonAccounts', N'ArchiveCode') IS NULL
BEGIN
    ALTER TABLE dbo.LegalPersonAccounts ADD ArchiveCode NVARCHAR(40) NULL;
    EXEC(N'CREATE UNIQUE INDEX UX_LegalPersonAccounts_ArchiveCode ON dbo.LegalPersonAccounts (ArchiveCode) WHERE ArchiveCode IS NOT NULL');
END;

-- Highest box/sequence issued per branch + sale year + million slice.
IF OBJECT_ID(N'dbo.LegalArchiveSeries', N'U') IS NULL
CREATE TABLE dbo.LegalArchiveSeries (
    BranchCode NVARCHAR(4) NOT NULL,
    Year2 INT NOT NULL,
    Slice INT NOT NULL,
    MaxBox INT NOT NULL,
    MaxSeq INT NOT NULL,
    CONSTRAINT PK_LegalArchiveSeries PRIMARY KEY (BranchCode, Year2, Slice)
);

-- Every code ever used (old system + this one). A code in here is never issued again.
IF OBJECT_ID(N'dbo.LegalArchiveTaken', N'U') IS NULL
CREATE TABLE dbo.LegalArchiveTaken (
    Code NVARCHAR(40) NOT NULL PRIMARY KEY,
    Source NVARCHAR(20) NOT NULL,
    AtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalArchiveTaken_At DEFAULT (SYSUTCDATETIME())
);

-- The lawyer who handled one step. Usually the debtor's assigned lawyer; IsSubstitute marks a step
-- handed to another lawyer for that step only (the assignment itself does not change).
IF COL_LENGTH(N'dbo.LegalCaseSteps', N'LawyerId') IS NULL
    ALTER TABLE dbo.LegalCaseSteps ADD LawyerId INT NULL, IsSubstitute BIT NOT NULL CONSTRAINT DF_LegalCaseSteps_Substitute DEFAULT (0);

-- A step dated in the future is scheduled, not done yet. It shows under «الخطوات القادمة» until it is
-- marked done; once its date passes without that it is overdue. Steps logged before this column are done.
IF COL_LENGTH(N'dbo.LegalCaseSteps', N'IsDone') IS NULL
    ALTER TABLE dbo.LegalCaseSteps ADD IsDone BIT NOT NULL CONSTRAINT DF_LegalCaseSteps_Done DEFAULT (1), DoneAtUtc DATETIME2 NULL;

-- ابطال is asked for by the legal app together with مبلغ الابطال; محاسب القانونية accepts it on the
-- company dashboard, and on acceptance the amount is recorded as a تسديد.
IF OBJECT_ID(N'dbo.LegalAnnulRequests', N'U') IS NULL
CREATE TABLE dbo.LegalAnnulRequests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PersonId INT NOT NULL REFERENCES dbo.LegalPersons(Id),
    CaseId INT NOT NULL REFERENCES dbo.LegalCases(Id),
    Amount BIGINT NOT NULL,
    Note NVARCHAR(MAX) NULL,
    RequestedBy NVARCHAR(200) NULL,
    RequestedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LegalAnnulRequests_At DEFAULT (SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_LegalAnnulRequests_Status DEFAULT (N'Pending'),
    PaymentId INT NULL,
    DecidedBy NVARCHAR(200) NULL,
    DecidedAtUtc DATETIME2 NULL,
    DecisionNote NVARCHAR(MAX) NULL
);

-- A mandatory step must be logged on a debtor before he can leave that flow.
IF COL_LENGTH(N'dbo.LegalStepTypes', N'IsMandatory') IS NULL
    ALTER TABLE dbo.LegalStepTypes ADD IsMandatory BIT NOT NULL CONSTRAINT DF_LegalStepTypes_Mandatory DEFAULT (0);
""";
}

/// <summary>
/// Stamps gateway→branch calls made on behalf of المدير / محاسب القانونية / مدير القانونية,
/// so the branch shows them القانونية customers. Everyone else's calls go out unstamped.
/// </summary>
public sealed class LegalViewerStampHandler : DelegatingHandler
{
    public const string ViewerHeader = "X-Legal-Viewer";
    private readonly IHttpContextAccessor _accessor;

    public LegalViewerStampHandler(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var userType = _accessor.HttpContext?.User.FindFirst("UserType")?.Value;
        if (SalesRoles.IsLegalViewer(userType) && request.Headers.Contains("X-Sales-Gateway-Key"))
            request.Headers.TryAddWithoutValidation(ViewerHeader, "1");
        return base.SendAsync(request, cancellationToken);
    }
}
