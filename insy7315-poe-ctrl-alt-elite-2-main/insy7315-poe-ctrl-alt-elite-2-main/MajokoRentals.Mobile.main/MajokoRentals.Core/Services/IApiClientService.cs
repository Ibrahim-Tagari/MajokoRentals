using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MajokoRentals.Core.Services
{
    public interface IApiClientService
    {
        Task<T?> GetAsync<T>(string endpoint) where T : class;
        Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
            where TRequest : class
            where TResponse : class;
        Task<T?> PostFormAsync<T>(string endpoint, Dictionary<string, string> formData) where T : class;
        Task<bool> PostMultipartAsync(string endpoint, MultipartFormDataContent content);
        Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
            where TRequest : class
            where TResponse : class;
        Task<bool> DeleteAsync(string endpoint);
        void SetAuthToken(string token);
    }

    public class ApiClientService : IApiClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiClientService> _logger;
        private string? _authToken;

        public ApiClientService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ApiClientService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public void SetAuthToken(string token)
        {
            _authToken = token;
        }

        public async Task<T?> GetAsync<T>(string endpoint) where T : class
        {
            try
            {
                var client = CreateClient();
                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var response = await client.GetAsync($"{apiUrl}{endpoint}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    });
                }

                _logger.LogWarning($"API GET request to {endpoint} failed with status {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling GET {endpoint}");
                return null;
            }
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
            where TRequest : class
            where TResponse : class
        {
            try
            {
                var client = CreateClient();
                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{apiUrl}{endpoint}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Handle 204 No Content or empty response
                    if (string.IsNullOrWhiteSpace(responseContent))
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    });
                }

                _logger.LogWarning($"API POST request to {endpoint} failed with status {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling POST {endpoint}");
                return null;
            }
        }

        public async Task<T?> PostFormAsync<T>(string endpoint, Dictionary<string, string> formData) where T : class
        {
            try
            {
                var client = CreateClient();
                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var content = new FormUrlEncodedContent(formData);

                var response = await client.PostAsync($"{apiUrl}{endpoint}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Handle 204 No Content or empty response
                    if (string.IsNullOrWhiteSpace(responseContent))
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    });
                }

                _logger.LogWarning($"API POST FORM request to {endpoint} failed with status {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling POST FORM {endpoint}");
                return null;
            }
        }

        public async Task<bool> PostMultipartAsync(string endpoint, MultipartFormDataContent content)
        {
            try
            {
                var client = CreateClient();
                var apiUrl = _configuration["ApiSettings:BaseUrl"];

                var response = await client.PostAsync($"{apiUrl}{endpoint}", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"API POST MULTIPART request to {endpoint} succeeded");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"API POST MULTIPART request to {endpoint} failed with status {response.StatusCode}: {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling POST MULTIPART {endpoint}");
                return false;
            }
        }

        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
            where TRequest : class
            where TResponse : class
        {
            try
            {
                var client = CreateClient();
                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PutAsync($"{apiUrl}{endpoint}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Handle 204 No Content or empty response
                    if (string.IsNullOrWhiteSpace(responseContent))
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    });
                }

                _logger.LogWarning($"API PUT request to {endpoint} failed with status {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling PUT {endpoint}");
                return null;
            }
        }

        public async Task<bool> DeleteAsync(string endpoint)
        {
            try
            {
                var client = CreateClient();
                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var response = await client.DeleteAsync($"{apiUrl}{endpoint}");

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                _logger.LogWarning($"API DELETE request to {endpoint} failed with status {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling DELETE {endpoint}");
                return false;
            }
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();

            if (!string.IsNullOrEmpty(_authToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _authToken);
            }

            return client;
        }
    }
}
