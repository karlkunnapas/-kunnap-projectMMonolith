namespace WebAppClient.Services;

public interface IApiClient
{
    Task<T> GetAsync<T>(string endpoint);
    Task<T> PostAsync<T>(string endpoint, object? body = null);
    Task PostAsync(string endpoint, object? body = null);
    Task<T> PutAsync<T>(string endpoint, object? body = null);
    Task PutAsync(string endpoint, object? body = null);
    Task<T> PatchAsync<T>(string endpoint, object? body = null);
    Task PatchAsync(string endpoint, object? body = null);
    Task DeleteAsync(string endpoint);
    void SaveTokens(string jwt, string refreshToken);
    void ClearTokens();
    string? GetRefreshToken();
    string? GetJwt();
}
