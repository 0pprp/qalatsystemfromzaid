namespace BE_DelegateWebApplication.Services
{
    /// <summary>
    /// Pure authorization rules for follower actions (testable without DB).
    /// </summary>
    public static class FollowerAuthorization
    {
        public const string RoleFollower = "Follower";
        public const string SourceFollower = "Follower";

        public static bool CanAccessAssignedList(bool isLinked) => isLinked;

        public static bool CanNoteCustomer(bool isLinked, int customerDelegateId, int listId) =>
            isLinked && listId > 0 && customerDelegateId == listId;

        public static bool CanNoteEmployee(bool isLinked, bool employeeAppearsOnAssignedList) =>
            isLinked && employeeAppearsOnAssignedList;

        public static bool CanSubmitSalesRequest(bool isLinked, int customerDelegateId, int listId) =>
            CanNoteCustomer(isLinked, customerDelegateId, listId);

        public static bool AcceptClientCreatedByUserId(int? clientClaimedId, int authenticatedFollowerId) =>
            // Always reject spoofing: identity comes only from session.
            false;

        public static int ResolveCreatedByUserId(int authenticatedFollowerId, int? clientClaimedId) =>
            authenticatedFollowerId;

        public static bool SalesmanCanReadFollowerEmployeeNotes() => false;

        public static bool CanEditOrDeleteNoteAfterSave() => false;
    }
}
