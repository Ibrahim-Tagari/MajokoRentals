namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Service for monitoring network connectivity and triggering auto-sync
    /// </summary>
    public interface IConnectivityService
    {
        /// <summary>
        /// Gets current connectivity status
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Starts monitoring connectivity changes
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// Stops monitoring connectivity changes
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// Event fired when connectivity changes
        /// </summary>
        event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;

        /// <summary>
        /// Manually trigger a sync check
        /// </summary>
        Task TriggerSyncAsync();
    }

    public class ConnectivityChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public NetworkAccess NetworkAccess { get; set; }
    }
}
