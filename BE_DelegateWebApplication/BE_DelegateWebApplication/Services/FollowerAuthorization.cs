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

        /// <summary>
        /// List delegate notes: the assigned list's DelegateId equals listId (القائمة = مندوب القائمة).
        /// Client cannot target an unrelated DelegateId.
        /// </summary>
        public static bool CanNoteListDelegate(bool isLinked, int listId, int requestedDelegateId) =>
            isLinked && listId > 0 && requestedDelegateId == listId;

        public static bool CanSubmitExistingCustomerRequest(bool isLinked, int customerDelegateId, int listId) =>
            CanNoteCustomer(isLinked, customerDelegateId, listId);

        public static bool CanSubmitNewCustomerRequest(bool isLinked, int listId) =>
            isLinked && listId > 0;

        public static bool AcceptClientCreatedByUserId(int? clientClaimedId, int authenticatedFollowerId) =>
            false;

        public static int ResolveCreatedByUserId(int authenticatedFollowerId, int? clientClaimedId) =>
            authenticatedFollowerId;

        public static bool ListDelegateCanReadFollowerNotes() => false;

        public static bool SalesEmployeeUsedInDelegateNoteFlow() => false;

        public static bool CanEditOrDeleteNoteAfterSave() => false;

        public static string? BuildImageUrl(string? imagesBaseUrl, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var name = fileName.Trim().Replace('\\', '/');
            if (name.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            var leaf = name.Split('/').LastOrDefault() ?? name;
            if (string.IsNullOrWhiteSpace(imagesBaseUrl))
            {
                return $"/Images/{leaf}";
            }

            var baseUrl = imagesBaseUrl.TrimEnd('/') + "/";
            return baseUrl + leaf;
        }
    }
}
