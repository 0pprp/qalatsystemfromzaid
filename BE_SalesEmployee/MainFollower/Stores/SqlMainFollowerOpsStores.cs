using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.MainFollower.Domain;
using Dapper;

namespace BE_SalesEmployee.MainFollower.Stores;

public sealed class SqlSaleReviewStore : ISaleReviewStore
{
    private readonly SqlGatewayConnectionFactory _factory;
    private readonly SemaphoreSlim _schema = new(1, 1);
    private volatile bool _ready;

    public SqlSaleReviewStore(SqlGatewayConnectionFactory factory) => _factory = factory;

    public async Task<SaleReviewRecord> InsertAsync(SaleReviewRecord row, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.QuerySingleAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.MfSaleReviews
              (MissionId, CityValue, SaleId, CustomerName, Score, Note,
               ShopStockEstimatedValue, EstimatedDailyRevenue, ReviewerUserName, CreatedAtUtc)
            OUTPUT INSERTED.Id
            VALUES (@MissionId, @CityValue, @SaleId, @CustomerName, @Score, @Note,
                    @ShopStockEstimatedValue, @EstimatedDailyRevenue, @ReviewerUserName, @CreatedAtUtc);
            """, row, cancellationToken: ct));
        row.Id = id;
        return Clone(row);
    }

    public async Task<SaleReviewRecord?> FindForMissionSaleAsync(int missionId, int saleId, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<SaleReviewRecord>(new CommandDefinition("""
            SELECT TOP 1 * FROM dbo.MfSaleReviews
            WHERE MissionId = @MissionId AND SaleId = @SaleId
            ORDER BY CreatedAtUtc DESC;
            """, new { MissionId = missionId, SaleId = saleId }, cancellationToken: ct));
        return row is null ? null : Clone(row);
    }

    public async Task<IReadOnlyList<SaleReviewRecord>> ListForMissionAsync(int missionId, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<SaleReviewRecord>(new CommandDefinition("""
            SELECT * FROM dbo.MfSaleReviews WHERE MissionId = @MissionId ORDER BY CreatedAtUtc DESC;
            """, new { MissionId = missionId }, cancellationToken: ct));
        return rows.Select(Clone).ToList();
    }

    public async Task<IReadOnlyList<SaleReviewRecord>> ListForCityAsync(string cityValue, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<SaleReviewRecord>(new CommandDefinition("""
            SELECT * FROM dbo.MfSaleReviews WHERE CityValue = @City ORDER BY CreatedAtUtc DESC;
            """, new { City = (cityValue ?? "").Trim() }, cancellationToken: ct));
        return rows.Select(Clone).ToList();
    }

    private static SaleReviewRecord Clone(SaleReviewRecord row) => new()
    {
        Id = row.Id,
        MissionId = row.MissionId,
        CityValue = row.CityValue,
        SaleId = row.SaleId,
        CustomerName = row.CustomerName,
        Score = row.Score,
        Note = row.Note,
        ShopStockEstimatedValue = row.ShopStockEstimatedValue,
        EstimatedDailyRevenue = row.EstimatedDailyRevenue,
        ReviewerUserName = row.ReviewerUserName,
        CreatedAtUtc = row.CreatedAtUtc
    };

    private async Task EnsureAsync(CancellationToken ct)
    {
        if (_ready) return;
        await _schema.WaitAsync(ct);
        try
        {
            if (_ready) return;
            using var connection = await _factory.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.MfSaleReviews', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.MfSaleReviews (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MissionId INT NOT NULL,
    CityValue NVARCHAR(200) NOT NULL,
    SaleId INT NOT NULL,
    CustomerName NVARCHAR(200) NULL,
    Score INT NOT NULL,
    Note NVARCHAR(MAX) NULL,
    ShopStockEstimatedValue DECIMAL(18,2) NOT NULL,
    EstimatedDailyRevenue DECIMAL(18,2) NOT NULL,
    ReviewerUserName NVARCHAR(200) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL
  );
END
""", cancellationToken: ct));
            _ready = true;
        }
        finally { _schema.Release(); }
    }
}

public sealed class SqlStaffReviewStore : IStaffReviewStore
{
    private readonly SqlGatewayConnectionFactory _factory;
    private readonly SemaphoreSlim _schema = new(1, 1);
    private volatile bool _ready;

    public SqlStaffReviewStore(SqlGatewayConnectionFactory factory) => _factory = factory;

    public async Task<StaffReviewRecord> InsertAsync(StaffReviewRecord row, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.QuerySingleAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.MfStaffReviews
              (MissionId, CommitteeName, CityValue, TargetType, TargetId, TargetName, ListId,
               Score, UserNote, FeedNote, ReviewerUserName, CreatedAtUtc)
            OUTPUT INSERTED.Id
            VALUES (@MissionId, @CommitteeName, @CityValue, @TargetType, @TargetId, @TargetName, @ListId,
                    @Score, @UserNote, @FeedNote, @ReviewerUserName, @CreatedAtUtc);
            """, row, cancellationToken: ct));
        row.Id = id;
        return Clone(row);
    }

    public async Task<IReadOnlyList<StaffReviewRecord>> ListForMissionAsync(int missionId, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<StaffReviewRecord>(new CommandDefinition("""
            SELECT * FROM dbo.MfStaffReviews WHERE MissionId = @MissionId ORDER BY CreatedAtUtc DESC;
            """, new { MissionId = missionId }, cancellationToken: ct));
        return rows.Select(Clone).ToList();
    }

    public async Task<IReadOnlyList<StaffReviewRecord>> ListForCityAsync(string cityValue, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<StaffReviewRecord>(new CommandDefinition("""
            SELECT * FROM dbo.MfStaffReviews WHERE CityValue = @City ORDER BY CreatedAtUtc DESC;
            """, new { City = (cityValue ?? "").Trim() }, cancellationToken: ct));
        return rows.Select(Clone).ToList();
    }

    public async Task<IReadOnlyList<StaffReviewRecord>> ListForTargetAsync(
        string targetType, int targetId, string? cityValue, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<StaffReviewRecord>(new CommandDefinition("""
            SELECT * FROM dbo.MfStaffReviews
            WHERE TargetType = @TargetType AND TargetId = @TargetId
              AND (@City IS NULL OR @City = '' OR CityValue = @City)
            ORDER BY CreatedAtUtc DESC;
            """, new { TargetType = targetType, TargetId = targetId, City = cityValue }, cancellationToken: ct));
        return rows.Select(Clone).ToList();
    }

    private static StaffReviewRecord Clone(StaffReviewRecord row) => new()
    {
        Id = row.Id,
        MissionId = row.MissionId,
        CommitteeName = row.CommitteeName,
        CityValue = row.CityValue,
        TargetType = row.TargetType,
        TargetId = row.TargetId,
        TargetName = row.TargetName,
        ListId = row.ListId,
        Score = row.Score,
        UserNote = row.UserNote,
        FeedNote = row.FeedNote,
        ReviewerUserName = row.ReviewerUserName,
        CreatedAtUtc = row.CreatedAtUtc
    };

    private async Task EnsureAsync(CancellationToken ct)
    {
        if (_ready) return;
        await _schema.WaitAsync(ct);
        try
        {
            if (_ready) return;
            using var connection = await _factory.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.MfStaffReviews', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.MfStaffReviews (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MissionId INT NOT NULL,
    CommitteeName NVARCHAR(200) NOT NULL,
    CityValue NVARCHAR(200) NOT NULL,
    TargetType NVARCHAR(80) NOT NULL,
    TargetId INT NOT NULL,
    TargetName NVARCHAR(200) NULL,
    ListId INT NULL,
    Score INT NOT NULL,
    UserNote NVARCHAR(MAX) NULL,
    FeedNote NVARCHAR(MAX) NOT NULL,
    ReviewerUserName NVARCHAR(200) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL
  );
END
""", cancellationToken: ct));
            _ready = true;
        }
        finally { _schema.Release(); }
    }
}

public sealed class SqlMainFollowerShiftStore : IMainFollowerShiftStore
{
    private readonly SqlGatewayConnectionFactory _factory;
    private readonly SemaphoreSlim _schema = new(1, 1);
    private volatile bool _ready;

    public SqlMainFollowerShiftStore(SqlGatewayConnectionFactory factory) => _factory = factory;

    public async Task<MainFollowerShiftRecord?> GetActiveAsync(string userName, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<MainFollowerShiftRecord>(new CommandDefinition("""
            SELECT TOP 1 * FROM dbo.MfShifts
            WHERE UserName = @UserName AND EndedAtUtc IS NULL
            ORDER BY Id DESC;
            """, new { UserName = (userName ?? "").Trim() }, cancellationToken: ct));
        return row;
    }

    public async Task<MainFollowerShiftRecord> StartAsync(MainFollowerShiftRecord row, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var existing = await GetActiveAsync(row.UserName, ct);
        if (existing != null) throw new CommitteeException("لديك دوام نشط بالفعل.");
        var id = await connection.QuerySingleAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.MfShifts
              (UserName, DisplayName, StartedAtUtc, EndedAtUtc, StartLatitude, StartLongitude, EndLatitude, EndLongitude)
            OUTPUT INSERTED.Id
            VALUES (@UserName, @DisplayName, @StartedAtUtc, NULL, @StartLatitude, @StartLongitude, NULL, NULL);
            """, row, cancellationToken: ct));
        row.Id = id;
        return row;
    }

    public async Task<MainFollowerShiftRecord?> EndAsync(
        string userName, double? endLat, double? endLng, DateTime endedAtUtc, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var key = (userName ?? "").Trim();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.MfShifts
            SET EndedAtUtc = @EndedAtUtc, EndLatitude = @EndLat, EndLongitude = @EndLng
            WHERE UserName = @UserName AND EndedAtUtc IS NULL;
            """, new { UserName = key, EndedAtUtc = endedAtUtc, EndLat = endLat, EndLng = endLng }, cancellationToken: ct));
        return await connection.QueryFirstOrDefaultAsync<MainFollowerShiftRecord>(new CommandDefinition("""
            SELECT TOP 1 * FROM dbo.MfShifts WHERE UserName = @UserName ORDER BY Id DESC;
            """, new { UserName = key }, cancellationToken: ct));
    }

    public async Task<MainFollowerLocationPoint?> LastPointAsync(string userName, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<MainFollowerLocationPoint>(new CommandDefinition("""
            SELECT TOP 1 * FROM dbo.MfLocationPoints WHERE UserName = @UserName ORDER BY Id DESC;
            """, new { UserName = (userName ?? "").Trim() }, cancellationToken: ct));
    }

    public async Task<MainFollowerLocationPoint> AddPointAsync(MainFollowerLocationPoint point, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        point.UserName = (point.UserName ?? "").Trim();
        var id = await connection.QuerySingleAsync<long>(new CommandDefinition("""
            INSERT INTO dbo.MfLocationPoints
              (ShiftId, UserName, Latitude, Longitude, AccuracyMeters, RecordedAtUtc)
            OUTPUT INSERTED.Id
            VALUES (@ShiftId, @UserName, @Latitude, @Longitude, @AccuracyMeters, @RecordedAtUtc);
            """, point, cancellationToken: ct));
        point.Id = id;
        return point;
    }

    public async Task<IReadOnlyList<MainFollowerLocationPoint>> ListTrailAsync(
        string userName, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<MainFollowerLocationPoint>(new CommandDefinition("""
            SELECT * FROM dbo.MfLocationPoints
            WHERE UserName = @UserName
              AND (@FromUtc IS NULL OR RecordedAtUtc >= @FromUtc)
              AND (@ToUtc IS NULL OR RecordedAtUtc <= @ToUtc)
            ORDER BY RecordedAtUtc;
            """, new { UserName = (userName ?? "").Trim(), FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(MainFollowerShiftRecord? Active, MainFollowerLocationPoint? Last)>> SnapshotAsync(
        IEnumerable<string> userNames, CancellationToken ct)
    {
        var result = new List<(MainFollowerShiftRecord? Active, MainFollowerLocationPoint? Last)>();
        foreach (var raw in userNames)
        {
            var active = await GetActiveAsync(raw, ct);
            var last = await LastPointAsync(raw, ct);
            result.Add((active, last));
        }
        return result;
    }

    private async Task EnsureAsync(CancellationToken ct)
    {
        if (_ready) return;
        await _schema.WaitAsync(ct);
        try
        {
            if (_ready) return;
            using var connection = await _factory.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.MfShifts', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.MfShifts (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserName NVARCHAR(200) NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL,
    StartedAtUtc DATETIME2 NOT NULL,
    EndedAtUtc DATETIME2 NULL,
    StartLatitude FLOAT NULL,
    StartLongitude FLOAT NULL,
    EndLatitude FLOAT NULL,
    EndLongitude FLOAT NULL
  );
END
IF OBJECT_ID(N'dbo.MfLocationPoints', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.MfLocationPoints (
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ShiftId INT NOT NULL,
    UserName NVARCHAR(200) NOT NULL,
    Latitude FLOAT NOT NULL,
    Longitude FLOAT NOT NULL,
    AccuracyMeters FLOAT NULL,
    RecordedAtUtc DATETIME2 NOT NULL
  );
END
""", cancellationToken: ct));
            _ready = true;
        }
        finally { _schema.Release(); }
    }
}
