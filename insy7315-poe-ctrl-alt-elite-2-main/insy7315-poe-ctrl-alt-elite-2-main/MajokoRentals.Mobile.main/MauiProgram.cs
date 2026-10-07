using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using MajokoRentals.Core.Services;
using MajokoRentals.Mobile.main.Services;
using MajokoRentals.Mobile.Data;
using System.Reflection;
using Plugin.LocalNotification;

namespace MajokoRentals.Mobile.main;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseLocalNotification()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// Load appsettings.json configuration
		var assembly = Assembly.GetExecutingAssembly();
		using var stream = assembly.GetManifestResourceStream("MajokoRentals.Mobile.main.appsettings.json");

		if (stream != null)
		{
			var config = new ConfigurationBuilder()
				.AddJsonStream(stream)
				.Build();

			builder.Configuration.AddConfiguration(config);
		}

		// Add authentication services
		builder.Services.AddAuthorizationCore();
		builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
		builder.Services.AddScoped<CustomAuthenticationStateProvider>();
		builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
			sp.GetRequiredService<CustomAuthenticationStateProvider>());

		// Add HttpClient factory
		builder.Services.AddHttpClient();

		// Register Local SQLite Database for offline support
		var dbPath = LocalDbInitializer.GetDatabasePath();
		builder.Services.AddDbContext<LocalDbContext>(options =>
			options.UseSqlite($"Data Source={dbPath}"));

		// Register offline file service
		builder.Services.AddSingleton<IOfflineFileService, OfflineFileService>();

		// Register sync service for offline/online data synchronization
		builder.Services.AddScoped<ISyncService, SyncService>();

		// Register connectivity monitoring service
		builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();

		// Register data caching service for offline use
		builder.Services.AddScoped<IDataCacheService, DataCacheService>();

		// Register localization service for multi-language support
		builder.Services.AddSingleton<ILocalizationService, LocalizationService>();

		// Register notification service for push notifications
		builder.Services.AddSingleton<IPushNotificationService, NotificationService>();

		// Register Firebase services
		builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
		builder.Services.AddSingleton<IFirestoreService, FirestoreService>();
		builder.Services.AddScoped<IUserProfileService, UserProfileService>();
		builder.Services.AddScoped<IFirebaseMvcAuthService, FirebaseMvcAuthService>();

		// Register client-side Firebase authentication (for direct Firebase login like web app)
		builder.Services.AddScoped<IFirebaseClientAuthService, FirebaseClientAuthService>();

		// Register application services
		builder.Services.AddScoped<IEmailService, EmailService>();
		builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();

		// Note: IFileUploadService not registered for mobile - uses IFormFile which is web-specific
		// File uploads in mobile will use MAUI FilePicker API when needed
		// builder.Services.AddScoped<IFileUploadService, MobileFileUploadService>();

		builder.Services.AddScoped<IChatbotService, ChatbotService>();

		// Configure API client for mobile
		builder.Services.AddScoped<ApiClientService>(sp =>
		{
			var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
			var logger = sp.GetRequiredService<ILogger<ApiClientService>>();
			var config = sp.GetRequiredService<IConfiguration>();

			return new ApiClientService(httpClientFactory, config, logger);
		});

		// Wrap API client with authenticated version that auto-includes auth token
		builder.Services.AddScoped<IApiClientService>(sp =>
		{
			var apiClient = sp.GetRequiredService<ApiClientService>();
			var authService = sp.GetRequiredService<IAuthenticationService>();

			return new AuthenticatedApiClientService(apiClient, authService);
		});

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
