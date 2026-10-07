using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace MajokoRentals.Mobile.main.Services
{
    public class NotificationService : IPushNotificationService
    {
        public async Task<bool> RequestPermissionsAsync()
        {
            try
            {
                if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
                {
                    await LocalNotificationCenter.Current.RequestNotificationPermission();
                }
                return await LocalNotificationCenter.Current.AreNotificationsEnabled();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting notification permissions: {ex.Message}");
                return false;
            }
        }

        public async Task ShowTestNotificationAsync()
        {
            try
            {
                // Request permissions first
                var hasPermission = await RequestPermissionsAsync();

                if (!hasPermission)
                {
                    System.Diagnostics.Debug.WriteLine("Notification permission not granted");
                    return;
                }

                // Create a test notification
                var notification = new NotificationRequest
                {
                    NotificationId = 1000,
                    Title = "Test Notification",
                    Description = "This is a test push notification from Majoko Rentals!",
                    ReturningData = "Test Data",
                    Schedule = new NotificationRequestSchedule
                    {
                        NotifyTime = DateTime.Now.AddSeconds(1) // Show notification after 1 second
                    },
                    Android = new AndroidOptions
                    {
                        Priority = AndroidPriority.High,
                        AutoCancel = true,
                        IconSmallName = new AndroidIcon("notification_icon")
                    }
                };

                await LocalNotificationCenter.Current.Show(notification);
                System.Diagnostics.Debug.WriteLine("Test notification sent successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing notification: {ex.Message}");
                throw;
            }
        }
    }
}
