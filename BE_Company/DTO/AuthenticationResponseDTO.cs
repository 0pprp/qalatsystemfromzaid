namespace BE_Company.DTO
{
    public class AuthenticationResponseDTO
    {
        public string? Token { get; set; } = null!;
        public DateTime? Expiration { get; set; }
    }
}
