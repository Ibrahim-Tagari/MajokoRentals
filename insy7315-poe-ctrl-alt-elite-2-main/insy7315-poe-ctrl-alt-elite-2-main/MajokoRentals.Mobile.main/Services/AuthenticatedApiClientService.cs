using MajokoRentals.Core.Services;

namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Wrapper around ApiClientService that automatically includes authentication token
    /// </summary>
    public class AuthenticatedApiClientService : IApiClientService
    {
        private readonly IApiClientService _apiClient;
        private readonly IAuthenticationService _authService;

        public AuthenticatedApiClientService(
            IApiClientService apiClient,
            IAuthenticationService authService)
        {
            _apiClient = apiClient;
            _authService = authService;
        }

        private async Task EnsureAuthTokenSetAsync()
        {
            var token = await _authService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _apiClient.SetAuthToken(token);
            }
        }

        public async Task<T?> GetAsync<T>(string endpoint) where T : class
        {
            await EnsureAuthTokenSetAsync();
            return await _apiClient.GetAsync<T>(endpoint);
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
            where TRequest : class
            where TResponse : class
        {
            await EnsureAuthTokenSetAsync();
            return await _apiClient.PostAsync<TRequest, TResponse>(endpoint, data);
        }

        public async Task<T?> PostFormAsync<T>(string endpoint, Dictionary<string, string> formData) where T : class
        {
            await EnsureAuthTokenSetAsync();
            return await _apiClient.PostFormAsync<T>(endpoint, formData);
        }

        public async Task<bool> PostMultipartAsync(string endpoint, MultipartFormDataContent content)
        {
            await EnsureAuthTokenSetAsync();
            return await _apiClient.PostMultipartAsync(endpoint, content);
        }

        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
            where TRequest : class
            where TResponse : class
        {
            await EnsureAuthTokenSetAsync();
            return await _apiClient.PutAsync<TRequest, TResponse>(endpoint, data);
        }

        public async Task<bool> DeleteAsync(string endpoint)
        {
            await EnsureAuthTokenSetAsync();
            return await _apiClient.DeleteAsync(endpoint);
        }

        public void SetAuthToken(string token)
        {
            _apiClient.SetAuthToken(token);
        }
    }
}
