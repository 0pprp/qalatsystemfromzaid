namespace BE_Company.DTO
{
    public class CustomerNoteGetDTO
    {
        public int? NoteID { get; set; }
        public int? CustomerID { get; set; }
        public int? UserID { get; set; }
        public string? NoteText { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? UserName { get; set; }
        public string? UserType { get; set; }
    }
}
