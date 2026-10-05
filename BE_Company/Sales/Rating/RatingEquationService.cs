using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Rating;

public interface IRatingEquationService
{
    Task<RatingEquationDto?> GetAsync(int customerId, CancellationToken ct = default);
    Task<RatingEquationDto?> SetAsync(int customerId, double factor, int? userId, CancellationToken ct = default);
}

public sealed class RatingEquationDto
{
    public int CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public double RatingEquationFactor { get; init; } = 1;
}

public sealed class RatingEquationService : IRatingEquationService
{
    public const double MinFactor = 0.5;
    public const double MaxFactor = 10;

    private static readonly object ColumnLock = new();
    private static bool _columnEnsured;

    private readonly string _connectionString;

    public RatingEquationService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DataBaseConnection")
            ?? throw new InvalidOperationException("Missing connection string DataBaseConnection.");
    }

    public static double ClampFactor(double factor)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor) || factor <= 0)
            return 1;
        if (factor < MinFactor) return MinFactor;
        if (factor > MaxFactor) return MaxFactor;
        return Math.Round(factor, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<RatingEquationDto?> GetAsync(int customerId, CancellationToken ct = default)
    {
        if (customerId <= 0) return null;
        await using var connection = new SqlConnection(_connectionString);
        await EnsureColumnAsync(connection, ct);
        return await connection.QuerySingleOrDefaultAsync<RatingEquationDto>(new CommandDefinition(@"
SELECT
    CustomerID AS CustomerId,
    CustomerName,
    CAST(ISNULL(RatingEquationFactor, 1) AS float) AS RatingEquationFactor
FROM dbo.Customers
WHERE CustomerID = @CustomerId;",
            new { CustomerId = customerId },
            cancellationToken: ct));
    }

    public async Task<RatingEquationDto?> SetAsync(
        int customerId,
        double factor,
        int? userId,
        CancellationToken ct = default)
    {
        if (customerId <= 0) return null;
        var clamped = ClampFactor(factor);
        await using var connection = new SqlConnection(_connectionString);
        await EnsureColumnAsync(connection, ct);

        var updated = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.Customers
SET RatingEquationFactor = @Factor,
    UpdatedDate = GETDATE()
WHERE CustomerID = @CustomerId;",
            new { CustomerId = customerId, Factor = clamped },
            cancellationToken: ct));
        if (updated <= 0) return null;

        if (userId is > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.Activities (UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID)
VALUES (@UserId, @Desc, GETUTCDATE(), 'false', NEWID());",
                new
                {
                    UserId = userId.Value,
                    Desc = $"تم ضبط معادلة التقييم للعميل #{customerId} إلى ×{clamped:0.##}",
                },
                cancellationToken: ct));
        }

        return await GetAsync(customerId, ct);
    }

    private static async Task EnsureColumnAsync(SqlConnection connection, CancellationToken ct)
    {
        if (_columnEnsured)
            return;

        // Serialize schema ensure so rating queries never race an in-flight ALTER.
        lock (ColumnLock)
        {
            if (_columnEnsured)
                return;
        }

        await connection.ExecuteAsync(new CommandDefinition(@"
IF COL_LENGTH(N'dbo.Customers', N'RatingEquationFactor') IS NULL
BEGIN
    ALTER TABLE dbo.Customers
        ADD RatingEquationFactor FLOAT NOT NULL
            CONSTRAINT DF_Customers_RatingEquationFactor DEFAULT (1);
END",
            cancellationToken: ct));

        lock (ColumnLock)
        {
            _columnEnsured = true;
        }
    }
}
