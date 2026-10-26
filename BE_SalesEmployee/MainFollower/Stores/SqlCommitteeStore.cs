using System.Text.Json;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.MainFollower.Domain;
using Dapper;

namespace BE_SalesEmployee.MainFollower.Stores;

/// <summary>SQL-backed committees/missions (gateway DB). Survives restart and multi-instance.</summary>
public sealed class SqlCommitteeStore : ICommitteeStore
{
    private readonly SqlGatewayConnectionFactory _factory;
    private readonly SemaphoreSlim _schema = new(1, 1);
    private volatile bool _ready;

    public SqlCommitteeStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<CommitteeRecord> InsertCommitteeAsync(
        string name,
        string createdByUserName,
        string createdByDisplayName,
        IReadOnlyList<string> memberUserNames,
        DateTime createdAtUtc,
        CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.QuerySingleAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.MfCommittees (Name, CreatedByUserName, CreatedByDisplayName, CreatedAtUtc, MembersJson)
            OUTPUT INSERTED.Id
            VALUES (@Name, @CreatedByUserName, @CreatedByDisplayName, @CreatedAtUtc, @MembersJson);
            """, new
        {
            Name = name,
            CreatedByUserName = createdByUserName,
            CreatedByDisplayName = createdByDisplayName,
            CreatedAtUtc = createdAtUtc,
            MembersJson = JsonSerializer.Serialize(memberUserNames)
        }, cancellationToken: ct));

        return new CommitteeRecord
        {
            Id = id,
            Name = name,
            CreatedByUserName = createdByUserName,
            CreatedByDisplayName = createdByDisplayName,
            CreatedAtUtc = createdAtUtc,
            MemberUserNames = memberUserNames.ToList()
        };
    }

    public async Task<CommitteeRecord?> GetCommitteeAsync(int committeeId, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition("""
            SELECT Id, Name, CreatedByUserName, CreatedByDisplayName, CreatedAtUtc, MembersJson
            FROM dbo.MfCommittees WHERE Id = @Id;
            """, new { Id = committeeId }, cancellationToken: ct));
        return row is null ? null : MapCommittee(row);
    }

    public async Task<MissionRecord> InsertMissionAsync(
        int committeeId,
        string committeeName,
        IReadOnlyList<string> memberUserNames,
        IReadOnlyList<string> cityValues,
        string? note,
        string createdByUserName,
        DateTime createdAtUtc,
        CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.QuerySingleAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.MfMissions
              (CommitteeId, CommitteeName, Status, Note, CreatedByUserName, CreatedAtUtc, MembersJson, CitiesJson)
            OUTPUT INSERTED.Id
            VALUES (@CommitteeId, @CommitteeName, @Status, @Note, @CreatedByUserName, @CreatedAtUtc, @MembersJson, @CitiesJson);
            """, new
        {
            CommitteeId = committeeId,
            CommitteeName = committeeName,
            Status = CommitteeMissionStatuses.Active,
            Note = note,
            CreatedByUserName = createdByUserName,
            CreatedAtUtc = createdAtUtc,
            MembersJson = JsonSerializer.Serialize(memberUserNames),
            CitiesJson = JsonSerializer.Serialize(cityValues)
        }, cancellationToken: ct));

        return new MissionRecord
        {
            Id = id,
            CommitteeId = committeeId,
            CommitteeName = committeeName,
            Status = CommitteeMissionStatuses.Active,
            Note = note,
            CreatedByUserName = createdByUserName,
            CreatedAtUtc = createdAtUtc,
            MemberUserNames = memberUserNames.ToList(),
            CityValues = cityValues.ToList()
        };
    }

    public async Task<IReadOnlyList<MissionRecord>> ListMissionsForMemberAsync(string memberUserName, CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<dynamic>(new CommandDefinition("""
            SELECT Id, CommitteeId, CommitteeName, Status, Note, CreatedByUserName, CreatedAtUtc, MembersJson, CitiesJson
            FROM dbo.MfMissions
            WHERE MembersJson LIKE @Needle
            ORDER BY Id DESC;
            """, new { Needle = "%\"" + memberUserName.Replace("\"", "") + "\"%" }, cancellationToken: ct));
        return rows.Select(MapMission)
            .Where(m => m.MemberUserNames.Any(u =>
                string.Equals(u, memberUserName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<IReadOnlyList<MissionRecord>> ListAllMissionsAsync(CancellationToken ct)
    {
        await EnsureAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<dynamic>(new CommandDefinition("""
            SELECT Id, CommitteeId, CommitteeName, Status, Note, CreatedByUserName, CreatedAtUtc, MembersJson, CitiesJson
            FROM dbo.MfMissions ORDER BY Id DESC;
            """, cancellationToken: ct));
        return rows.Select(MapMission).ToList();
    }

    private static CommitteeRecord MapCommittee(dynamic row) => new()
    {
        Id = (int)row.Id,
        Name = (string)row.Name,
        CreatedByUserName = (string)row.CreatedByUserName,
        CreatedByDisplayName = (string)row.CreatedByDisplayName,
        CreatedAtUtc = (DateTime)row.CreatedAtUtc,
        MemberUserNames = JsonSerializer.Deserialize<List<string>>((string)row.MembersJson) ?? []
    };

    private static MissionRecord MapMission(dynamic row) => new()
    {
        Id = (int)row.Id,
        CommitteeId = (int)row.CommitteeId,
        CommitteeName = (string)row.CommitteeName,
        Status = (string)row.Status,
        Note = row.Note as string,
        CreatedByUserName = (string)row.CreatedByUserName,
        CreatedAtUtc = (DateTime)row.CreatedAtUtc,
        MemberUserNames = JsonSerializer.Deserialize<List<string>>((string)row.MembersJson) ?? [],
        CityValues = JsonSerializer.Deserialize<List<string>>((string)row.CitiesJson) ?? []
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
IF OBJECT_ID(N'dbo.MfCommittees', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.MfCommittees (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    CreatedByUserName NVARCHAR(200) NOT NULL,
    CreatedByDisplayName NVARCHAR(200) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    MembersJson NVARCHAR(MAX) NOT NULL
  );
END
IF OBJECT_ID(N'dbo.MfMissions', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.MfMissions (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CommitteeId INT NOT NULL,
    CommitteeName NVARCHAR(200) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    Note NVARCHAR(MAX) NULL,
    CreatedByUserName NVARCHAR(200) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    MembersJson NVARCHAR(MAX) NOT NULL,
    CitiesJson NVARCHAR(MAX) NOT NULL
  );
END
""", cancellationToken: ct));
            _ready = true;
        }
        finally
        {
            _schema.Release();
        }
    }
}
