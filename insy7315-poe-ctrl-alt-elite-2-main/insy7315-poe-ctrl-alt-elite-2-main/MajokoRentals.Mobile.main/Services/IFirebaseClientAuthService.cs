namespace MajokoRentals.Mobile.main.Services
{
    public interface IFirebaseClientAuthService
    {
        Task<string> SignInWithEmailAndPasswordAsync(string email, string password);
        Task<string> CreateUserWithEmailAndPasswordAsync(string email, string password);
        Task<(string idToken, string email, string displayName, bool isNewUser)> SignInWithGoogleAsync();
        Task SignOutAsync();
        Task<string?> GetCurrentUserIdTokenAsync();
        Task<string?> GetCurrentUserEmailAsync();
        Task<string?> GetCurrentUserDisplayNameAsync();
    }
}
