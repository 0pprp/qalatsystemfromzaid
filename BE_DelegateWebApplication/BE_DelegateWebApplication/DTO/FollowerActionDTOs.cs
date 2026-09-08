namespace BE_DelegateWebApplication.DTO
{
    public sealed class FollowerNoteCreateDTO
    {
        public string? AsyncId { get; set; }
        public int ListId { get; set; }
        public string? NoteText { get; set; }
        /// <summary>Ignored — server sets identity from authenticated follower.</summary>
        public int? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedByRole { get; set; }
    }

    public sealed class FollowerCustomerNoteDTO
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string NoteText { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string CreatedByRole { get; set; } = "Follower";
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class FollowerEmployeeNoteDTO
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int? ListId { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string CreatedByRole { get; set; } = "Follower";
        public string NoteText { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class FollowerSalesRequestCreateDTO
    {
        public string? AsyncId { get; set; }
        public int ListId { get; set; }
        public int CustomerId { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        /// <summary>Ignored — server sets from authenticated follower.</summary>
        public int? CreatedByUserId { get; set; }
    }

    public sealed class FollowerSalesRequestResultDTO
    {
        public int Id { get; set; }
        public string CustomerSourceType { get; set; } = "Follower";
        public string? CreatedByName { get; set; }
        public string? CreatedByUserType { get; set; }
        public int CreatedByUserId { get; set; }
        public string Status { get; set; } = "New";
        public DateTime CreatedAtUtc { get; set; }
    }
}
