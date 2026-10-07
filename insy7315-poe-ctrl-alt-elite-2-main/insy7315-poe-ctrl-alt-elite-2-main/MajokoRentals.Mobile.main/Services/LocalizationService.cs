using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Mobile.main.Resources;
using Microsoft.Extensions.Logging;

namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Implementation of localization service
    /// </summary>
    public class LocalizationService : ILocalizationService
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<LocalizationService> _logger;
        private Language _currentLanguage = Language.English;

        public LocalizationService(
            IAuthenticationService authService,
            ILogger<LocalizationService> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        public Language CurrentLanguage
        {
            get => _currentLanguage;
            private set
            {
                if (_currentLanguage != value)
                {
                    var oldLanguage = _currentLanguage;
                    _currentLanguage = value;
                    OnLanguageChanged(oldLanguage, value);
                }
            }
        }

        public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

        public async Task SetLanguageAsync(Language language)
        {
            try
            {
                CurrentLanguage = language;

                // Store locally for offline access and as source of truth
                await SecureStorage.SetAsync("PreferredLanguage", language.ToString());

                _logger.LogInformation("Language set to {Language}", language);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting language to {Language}", language);
            }
        }

        public async Task LoadUserLanguagePreferenceAsync()
        {
            try
            {
                // Load from locally stored preference
                var storedLanguage = await SecureStorage.GetAsync("PreferredLanguage");
                if (!string.IsNullOrEmpty(storedLanguage) && Enum.TryParse<Language>(storedLanguage, out var language))
                {
                    CurrentLanguage = language;
                    _logger.LogInformation("Loaded language preference: {Language}", language);
                }
                else
                {
                    // Default to English
                    CurrentLanguage = Language.English;
                    _logger.LogInformation("No language preference found, defaulting to English");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading language preference");
                CurrentLanguage = Language.English;
            }
        }

        public string GetString(string key)
        {
            try
            {
                return CurrentLanguage switch
                {
                    Language.Zulu => AppResources_zu.ResourceManager.GetString(key) ?? key,
                    Language.Sesotho => AppResources_st.ResourceManager.GetString(key) ?? key,
                    _ => AppResources.ResourceManager.GetString(key) ?? key
                };
            }
            catch
            {
                return key;
            }
        }

        private void OnLanguageChanged(Language oldLanguage, Language newLanguage)
        {
            LanguageChanged?.Invoke(this, new LanguageChangedEventArgs
            {
                OldLanguage = oldLanguage,
                NewLanguage = newLanguage
            });
        }
    }
}
