using Microsoft.Extensions.Logging;

namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Service that monitors connectivity and auto-syncs when connection is restored
    /// </summary>
    public class ConnectivityService : IConnectivityService, IDisposable
    {
        private readonly ISyncService _syncService;
        private readonly ILogger<ConnectivityService> _logger;
        private bool _isMonitoring = false;
        private bool _wasDisconnected = false;

        public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;

        public bool IsConnected => Connectivity.NetworkAccess == NetworkAccess.Internet;

        public ConnectivityService(
            ISyncService syncService,
            ILogger<ConnectivityService> logger)
        {
            _syncService = syncService;
            _logger = logger;
        }

        public void StartMonitoring()
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;

            // Subscribe to connectivity changes
            Connectivity.ConnectivityChanged += OnConnectivityChanged;

            _logger.LogInformation("Connectivity monitoring started");
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;

            // Unsubscribe from connectivity changes
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;

            _logger.LogInformation("Connectivity monitoring stopped");
        }

        private async void OnConnectivityChanged(object? sender, Microsoft.Maui.Networking.ConnectivityChangedEventArgs e)
        {
            var isConnected = e.NetworkAccess == NetworkAccess.Internet;

            _logger.LogInformation($"Connectivity changed: {e.NetworkAccess}");

            // Notify subscribers
            ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs
            {
                IsConnected = isConnected,
                NetworkAccess = e.NetworkAccess
            });

            // If we just reconnected and were previously disconnected
            if (isConnected && _wasDisconnected)
            {
                _logger.LogInformation("Connection restored. Checking for pending sync...");

                // Wait a bit to ensure connection is stable
                await Task.Delay(2000);

                // Trigger auto-sync
                await TriggerSyncAsync();
            }

            _wasDisconnected = !isConnected;
        }

        public async Task TriggerSyncAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    _logger.LogWarning("Cannot sync: No internet connection");
                    return;
                }

                var hasPending = await _syncService.HasPendingChangesAsync();
                if (!hasPending)
                {
                    _logger.LogInformation("No pending changes to sync");
                    return;
                }

                _logger.LogInformation("Starting auto-sync...");
                var result = await _syncService.SyncAllAsync();

                if (result.Success)
                {
                    _logger.LogInformation($"Auto-sync completed: {result.ItemsSynced} items synced");
                }
                else
                {
                    _logger.LogWarning($"Auto-sync completed with errors: {result.ItemsFailed} failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-sync");
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
