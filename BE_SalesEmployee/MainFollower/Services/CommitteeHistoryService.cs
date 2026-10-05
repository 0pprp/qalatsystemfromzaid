using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Stores;

namespace BE_SalesEmployee.MainFollower.Services;

public sealed class MissionHistoryEntry
{
    public required string Kind { get; init; } // sale | staff
    public required DateTime AtUtc { get; init; }
    public required int MissionId { get; init; }
    public string? CommitteeName { get; init; }
    public required string CityValue { get; init; }
    public required int Score { get; init; }
    public string? Title { get; init; }
    public string? Note { get; init; }
    public string? ReviewerUserName { get; init; }
}

public sealed class MissionSummaryDto
{
    public required int MissionId { get; init; }
    public required string CommitteeName { get; init; }
    public required int SaleReviewCount { get; init; }
    public required int StaffReviewCount { get; init; }
    public double? AvgSaleScore { get; init; }
    public double? AvgStaffScore { get; init; }
    public required IReadOnlyList<string> CityValues { get; init; }
}

public sealed class CommitteeHistoryService
{
    private readonly ICommitteeStore _committees;
    private readonly ISaleReviewStore _sales;
    private readonly IStaffReviewStore _staff;

    public CommitteeHistoryService(
        ICommitteeStore committees,
        ISaleReviewStore sales,
        IStaffReviewStore staff)
    {
        _committees = committees;
        _sales = sales;
        _staff = staff;
    }

    public async Task<IReadOnlyList<MissionHistoryEntry>> ListCityHistoryForMemberAsync(
        string memberUserName,
        string cityValue,
        CancellationToken ct)
    {
        var city = (cityValue ?? "").Trim();
        if (city.Length == 0)
        {
            throw new CommitteeException("المحافظة مطلوبة.");
        }

        var missions = await _committees.ListMissionsForMemberAsync(memberUserName, ct);
        var allowed = missions
            .Where(m => m.CityValues.Any(c => string.Equals(c, city, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (allowed.Count == 0)
        {
            throw new CommitteeException("هذه المحافظة ليست ضمن مهامك.");
        }

        // Durable province feed: any prior committee reviews for this city (not only current mission).
        var sales = await _sales.ListForCityAsync(city, ct);
        var staff = await _staff.ListForCityAsync(city, ct);
        var allMissions = await _committees.ListAllMissionsAsync(ct);
        var missionNames = allMissions.ToDictionary(m => m.Id, m => m.CommitteeName);

        var entries = new List<MissionHistoryEntry>();
        foreach (var s in sales)
        {
            entries.Add(new MissionHistoryEntry
            {
                Kind = "sale",
                AtUtc = s.CreatedAtUtc,
                MissionId = s.MissionId,
                CommitteeName = missionNames.GetValueOrDefault(s.MissionId),
                CityValue = s.CityValue,
                Score = s.Score,
                Title = s.CustomerName ?? $"مبيع #{s.SaleId}",
                Note = s.Note,
                ReviewerUserName = s.ReviewerUserName
            });
        }

        foreach (var r in staff)
        {
            entries.Add(new MissionHistoryEntry
            {
                Kind = "staff",
                AtUtc = r.CreatedAtUtc,
                MissionId = r.MissionId,
                CommitteeName = r.CommitteeName.Length > 0
                    ? r.CommitteeName
                    : missionNames.GetValueOrDefault(r.MissionId),
                CityValue = r.CityValue,
                Score = r.Score,
                Title = $"{StaffTargetTypes.DisplayName(r.TargetType)}: {r.TargetName ?? $"#{r.TargetId}"}",
                Note = string.IsNullOrWhiteSpace(r.UserNote) ? r.FeedNote : r.UserNote,
                ReviewerUserName = r.ReviewerUserName
            });
        }

        return entries.OrderByDescending(e => e.AtUtc).ToList();
    }

    public async Task<MissionSummaryDto> SummarizeMissionAsync(int missionId, CancellationToken ct)
    {
        var missions = await _committees.ListAllMissionsAsync(ct);
        var mission = missions.FirstOrDefault(m => m.Id == missionId)
            ?? throw new CommitteeException("المهمة غير موجودة.");

        var sales = await _sales.ListForMissionAsync(missionId, ct);
        var staff = await _staff.ListForMissionAsync(missionId, ct);
        return new MissionSummaryDto
        {
            MissionId = missionId,
            CommitteeName = mission.CommitteeName,
            SaleReviewCount = sales.Count,
            StaffReviewCount = staff.Count,
            AvgSaleScore = sales.Count == 0 ? null : Math.Round(sales.Average(s => s.Score), 1),
            AvgStaffScore = staff.Count == 0 ? null : Math.Round(staff.Average(s => s.Score), 1),
            CityValues = mission.CityValues
        };
    }
}
