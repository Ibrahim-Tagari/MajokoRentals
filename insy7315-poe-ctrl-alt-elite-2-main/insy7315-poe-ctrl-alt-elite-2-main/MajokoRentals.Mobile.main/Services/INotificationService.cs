namespace MajokoRentals.Mobile.main.Services
{
    public interface IPushNotificationService
    {
        Task<bool> RequestPermissionsAsync();
        Task ShowTestNotificationAsync();
    }
}
