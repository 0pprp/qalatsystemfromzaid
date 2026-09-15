using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Gateway SQL implementation over dbo.CentralComplaints. All SQL is parameterized.</summary>
public sealed class SqlCentralComplaintsStore : ICentralComplaintsStore
{
    private const string Columns = """
        Id, SourceApp, SourceType, SenderUserId, SenderUserName, SenderDisplayName, SenderRole,
        CityValue, CityName, Subject, Body, Status, CreatedAtUtc, ReadAtUtc, MetadataJson
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlCentralComplaintsStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<CentralComplaint> CreateAsync(CentralComplaint complaint, CancellationToken ct = default)
    {
        var row = complaint.Clone();
        if (row.Id == Guid.Empty)
        {
            row.Id = Guid.NewGuid();
        }
        if (row.CreatedAtUtc == default)
        {
            row.CreatedAtUtc = DateTime.UtcNow;
        }
        row.Status = ComplaintStatuses.Unread;
        row.ReadAtUtc = null;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO dbo.CentralComplaints ({Columns})
            VALUES (@Id, @SourceApp, @SourceType, @SenderUserId, @SenderUserName, @SenderDisplayName, @SenderRole,
                    @CityValue, @CityName, @Subject, @Body, @Status, @CreatedAtUtc, @ReadAtUtc, @MetadataJson);
            """, row, cancellationToken: ct));
        return row;
    }

    public async Task<PagedResult<CentralComplaint>> QueryAsync(ComplaintInboxQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        var parameters = new DynamicParameters();
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Unread", ComplaintStatuses.Unread);
        parameters.Add("UnreadOnly", query.UnreadOnly ? 1 : 0);
        parameters.Add("CityValue", string.IsNullOrWhiteSpace(query.CityValue) ? null : query.CityValue.Trim());
        parameters.Add("SourceApp", string.IsNullOrWhiteSpace(query.SourceApp) ? null : query.SourceApp.Trim());
        parameters.Add("Search", string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search.Trim()}%");

        const string where = """
            WHERE (@UnreadOnly = 0 OR Status = @Unread)
              AND (@CityValue IS NULL OR CityValue = @CityValue)
              AND (@SourceApp IS NULL OR SourceApp = @SourceApp)
              AND (@Search IS NULL OR Subject LIKE @Search OR Body LIKE @Search OR SenderDisplayName LIKE @Search)
            """;

        using var connection = await _factory.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.CentralComplaints
            {where}
            ORDER BY CreatedAtUtc DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM dbo.CentralComplaints {where};
            """, parameters, cancellationToken: ct));

        var items = (await grid.ReadAsync<CentralComplaint>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return new PagedResult<CentralComplaint>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<CentralComplaint?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<CentralComplaint>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.CentralComplaints WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<CentralComplaint?> MarkReadAsync(Guid id, DateTime readAtUtc, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.CentralComplaints
            SET Status = @read, ReadAtUtc = @readAtUtc
            WHERE Id = @id AND Status = @unread;
            """, new { id, readAtUtc, read = ComplaintStatuses.Read, unread = ComplaintStatuses.Unread }, cancellationToken: ct));

        return await connection.QuerySingleOrDefaultAsync<CentralComplaint>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.CentralComplaints WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<int> MarkAllReadAsync(DateTime readAtUtc, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.CentralComplaints
            SET Status = @read, ReadAtUtc = @readAtUtc
            WHERE Status = @unread;
            """, new { readAtUtc, read = ComplaintStatuses.Read, unread = ComplaintStatuses.Unread }, cancellationToken: ct));
    }

    public async Task<int> CountUnreadAsync(CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM dbo.CentralComplaints WHERE Status = @unread;
            """, new { unread = ComplaintStatuses.Unread }, cancellationToken: ct));
    }
}
