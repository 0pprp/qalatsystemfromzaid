namespace BE_Company.DTO
{
    public class AuthenticationResponseDTO
    {
        /// <summary>Deploy marker — login JSON includes UserType for sales APK gate.</summary>
        public const string LoginBodyUserTypeMarker = "LOGIN_BODY_USERTYPE_V1";

        public string? Token { get; set; } = null!;
        public DateTime? Expiration { get; set; }

        /// <summary>
        /// Returned in the login JSON body so mobile apps (sales employee / filter)
        /// can gate by role without decoding the JWT. Also present as a JWT claim.
        /// </summary>
        public string? UserType { get; set; }

        public int? UserID { get; set; }
        public string? UserName { get; set; }
        public string? UserImage { get; set; }
    }
}
