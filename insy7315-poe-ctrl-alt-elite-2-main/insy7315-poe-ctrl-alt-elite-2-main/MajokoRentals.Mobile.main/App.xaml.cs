using MajokoRentals.Mobile.Data;
using MajokoRentals.Mobile.main.Services;

namespace MajokoRentals.Mobile.main;

public partial class App : Application
{
	private readonly LocalDbContext _localDb;
	private readonly IConnectivityService _connectivityService;
	private readonly ILocalizationService _localizationService;

	public App(LocalDbContext localDb, IConnectivityService connectivityService, ILocalizationService localizationService)
	{
		InitializeComponent();

		_localDb = localDb;
		_connectivityService = connectivityService;
		_localizationService = localizationService;

		// Initialize offline support and localization
		InitializeAsync().ConfigureAwait(false);
	}

	private async Task InitializeAsync()
	{
		try
		{
			// Initialize local database
			await LocalDbInitializer.InitializeAsync(_localDb);

			// Start connectivity monitoring for auto-sync
			_connectivityService.StartMonitoring();

			// Load user language preference
			await _localizationService.LoadUserLanguagePreferenceAsync();
		}
		catch (Exception ex)
		{
			// Log error
			System.Diagnostics.Debug.WriteLine($"Error initializing app: {ex.Message}");
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "MajokoRentals.Mobile" };
	}
}
