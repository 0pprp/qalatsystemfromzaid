using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Stores;

namespace BE_SalesEmployee.MainFollower.Services;

public sealed class StaffReviewService
{
    private readonly ICommitteeStore _committees;
    private readonly IStaffReviewStore _reviews;

    public StaffReviewService(ICommitteeStore committees, IStaffReviewStore reviews)
    {
        _committees = committees;
        _reviews = reviews;
    }

    public async Task<StaffReviewRecord> SubmitAsync(
        string reviewerUserName,
        int missionId,
        string cityValue,
        string targetType,
        int targetId,
        string? targetName,
        int? listId,
        int score,
        string? userNote,
        CancellationToken ct)
    {
        var typeErr = StaffReviewRules.ValidateTargetType(targetType);
        if (typeErr != null)
        {
            throw new CommitteeException(typeErr);
        }

        var scoreErr = StaffReviewRules.ValidateScoreAndNote(score, userNote);
        if (scoreErr != null)
        {
            throw new CommitteeException(scoreErr);
        }

        if (targetId <= 0)
        {
            throw new CommitteeException("معرّف الموظف مطلوب.");
        }

        var missions = await _committees.ListMissionsForMemberAsync(reviewerUserName, ct);
        var mission = missions.FirstOrDefault(m => m.Id == missionId);
        var accessErr = MissionAccessRules.AuthorizeMemberCity(mission, reviewerUserName, cityValue);
        if (accessErr != null)
        {
            throw new CommitteeException(accessErr);
        }

        var normalizedType = StaffReviewRules.NormalizeTargetType(targetType);
        var at = DateTime.UtcNow;
        var feed = StaffReviewRules.BuildFeedNote(
            mission!.CommitteeName,
            normalizedType,
            targetName,
            score,
            userNote,
            at);

        var row = new StaffReviewRecord
        {
            MissionId = missionId,
            CommitteeName = mission.CommitteeName,
            CityValue = (cityValue ?? "").Trim(),
            TargetType = normalizedType,
            TargetId = targetId,
            TargetName = string.IsNullOrWhiteSpace(targetName) ? null : targetName.Trim(),
            ListId = listId is > 0 ? listId : null,
            Score = score,
            UserNote = string.IsNullOrWhiteSpace(userNote) ? null : userNote.Trim(),
            FeedNote = feed,
            ReviewerUserName = (reviewerUserName ?? "").Trim(),
            CreatedAtUtc = at
        };
        return await _reviews.InsertAsync(row, ct);
    }

    public Task<IReadOnlyList<StaffReviewRecord>> ListForMissionAsync(int missionId, CancellationToken ct) =>
        _reviews.ListForMissionAsync(missionId, ct);

    public Task<IReadOnlyList<StaffReviewRecord>> ListForTargetAsync(
        string targetType,
        int targetId,
        string? cityValue,
        CancellationToken ct)
    {
        var typeErr = StaffReviewRules.ValidateTargetType(targetType);
        if (typeErr != null)
        {
            throw new CommitteeException(typeErr);
        }

        return _reviews.ListForTargetAsync(
            StaffReviewRules.NormalizeTargetType(targetType),
            targetId,
            cityValue,
            ct);
    }
}
