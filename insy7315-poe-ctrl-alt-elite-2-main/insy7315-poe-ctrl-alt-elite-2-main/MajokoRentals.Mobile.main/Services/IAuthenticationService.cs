namespace MajokoRentals.Mobile.main.Services
{
    public interface IAuthenticationService
    {
        Task<string?> GetTokenAsync();
        Task SetTokenAsync(string token);
        Task<bool> IsAuthenticatedAsync();
        Task LogoutAsync();
        Task<AuthUserInfo?> GetUserInfoAsync();
    }

    public class AuthUserInfo
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> Roles { get; set; } = new();
    }
}
