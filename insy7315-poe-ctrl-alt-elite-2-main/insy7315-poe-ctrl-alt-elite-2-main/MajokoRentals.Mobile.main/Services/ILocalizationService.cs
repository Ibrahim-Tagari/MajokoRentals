using MajokoRentals.Core.Models;

namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Service for managing application localization and language preferences
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Gets the current language preference
        /// </summary>
        Language CurrentLanguage { get; }

        /// <summary>
        /// Event raised when the language is changed
        /// </summary>
        event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

        /// <summary>
        /// Sets the current language and saves to user profile if user is logged in
        /// </summary>
        Task SetLanguageAsync(Language language);

        /// <summary>
        /// Loads the language preference from the logged-in user's profile
        /// </summary>
        Task LoadUserLanguagePreferenceAsync();

        /// <summary>
        /// Gets a localized string for the given key
        /// </summary>
        string GetString(string key);
    }

    public class LanguageChangedEventArgs : EventArgs
    {
        public Language NewLanguage { get; set; }
        public Language OldLanguage { get; set; }
    }
}
