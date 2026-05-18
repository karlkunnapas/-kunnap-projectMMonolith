using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using WebAppClient.Models;

namespace WebAppClient.Services;

public class ApiClient : IApiClient
{
    private const string JwtSessionKey = "ApiJwt";
    private const string RefreshTokenSessionKey = "ApiRefreshToken";
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        return await SendForModelAsync<T>(request);
    }

    public async Task<T> PostAsync<T>(string endpoint, object? body = null)
    {
        using var request = CreateRequestWithBody(HttpMethod.Post, endpoint, body);
        return await SendForModelAsync<T>(request);
    }

    public async Task PostAsync(string endpoint, object? body = null)
    {
        using var request = CreateRequestWithBody(HttpMethod.Post, endpoint, body);
        await SendNoContentAsync(request);
    }

    public async Task<T> PutAsync<T>(string endpoint, object? body = null)
    {
        using var request = CreateRequestWithBody(HttpMethod.Put, endpoint, body);
        return await SendForModelAsync<T>(request);
    }

    public async Task PutAsync(string endpoint, object? body = null)
    {
        using var request = CreateRequestWithBody(HttpMethod.Put, endpoint, body);
        await SendNoContentAsync(request);
    }

    public async Task<T> PatchAsync<T>(string endpoint, object? body = null)
    {
        using var request = CreateRequestWithBody(HttpMethod.Patch, endpoint, body);
        return await SendForModelAsync<T>(request);
    }

    public async Task PatchAsync(string endpoint, object? body = null)
    {
        using var request = CreateRequestWithBody(HttpMethod.Patch, endpoint, body);
        await SendNoContentAsync(request);
    }

    public async Task DeleteAsync(string endpoint)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        await SendNoContentAsync(request);
    }

    public void SaveTokens(string jwt, string refreshToken)
    {
        var session = GetSession();
        session.SetString(JwtSessionKey, jwt);
        session.SetString(RefreshTokenSessionKey, refreshToken);
    }

    public void ClearTokens()
    {
        var session = GetSession();
        session.Remove(JwtSessionKey);
        session.Remove(RefreshTokenSessionKey);
    }

    public string? GetRefreshToken()
    {
        return GetSession().GetString(RefreshTokenSessionKey);
    }

    public string? GetJwt()
    {
        return GetSession().GetString(JwtSessionKey);
    }

    private async Task<T> SendForModelAsync<T>(HttpRequestMessage request)
    {
        using var response = await SendWithRefreshAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ApiException((int)response.StatusCode, "Empty API response.");
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        if (model == null)
        {
            throw new ApiException((int)response.StatusCode, "Failed to parse API response.");
        }

        return model;
    }

    private async Task SendNoContentAsync(HttpRequestMessage request)
    {
        using var response = await SendWithRefreshAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw CreateApiException(response.StatusCode, body);
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(HttpRequestMessage originalRequest)
    {
        var client = _httpClientFactory.CreateClient("ApiClient");
        var jwtBeforeAttempt = GetJwt();
        var firstAttempt = await SendCoreAsync(client, originalRequest);
        if (firstAttempt.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return firstAttempt;
        }

        var refreshed = await TryRefreshTokenAsync(jwtBeforeAttempt);
        if (!refreshed)
        {
            return firstAttempt;
        }

        firstAttempt.Dispose();
        using var retryRequest = await CloneRequestAsync(originalRequest);
        return await SendCoreAsync(client, retryRequest);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpClient client, HttpRequestMessage request)
    {
        var jwt = GetJwt();
        if (!string.IsNullOrWhiteSpace(jwt))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        }

        return await client.SendAsync(request);
    }

    private async Task<bool> TryRefreshTokenAsync(string? jwtUsedForFailedRequest)
    {
        await RefreshLock.WaitAsync();
        try
        {
            var currentJwt = GetJwt();
            // Another concurrent request may have refreshed tokens while this request was waiting.
            if (!string.IsNullOrWhiteSpace(currentJwt) &&
                !string.Equals(currentJwt, jwtUsedForFailedRequest, StringComparison.Ordinal))
            {
                return true;
            }

            var jwt = GetJwt();
            var refreshToken = GetRefreshToken();
            if (string.IsNullOrWhiteSpace(jwt) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            var client = _httpClientFactory.CreateClient("ApiClient");
            var request = new RefreshTokenRequestDto { Jwt = jwt, RefreshToken = refreshToken };
            using var response = await client.PostAsync("api/v1/account/renewrefreshtoken", JsonContent(request));
            if (!response.IsSuccessStatusCode)
            {
                ClearTokens();
                await SignOutCookieAsync();
                return false;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JwtResponseDto>(responseBody, JsonOptions);
            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.JWT) || string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                ClearTokens();
                await SignOutCookieAsync();
                return false;
            }

            SaveTokens(tokenResponse.JWT, tokenResponse.RefreshToken);
            await ResignCookieAsync(tokenResponse.JWT);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT refresh failed.");
            ClearTokens();
            await SignOutCookieAsync();
            return false;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private async Task ResignCookieAsync(string jwt)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var claims = token.Claims.ToList();

        var selectedCompanyId = httpContext.Session.GetString("SelectedCompanyId");
        var selectedCompanySlug = httpContext.Session.GetString("SelectedCompanySlug");
        var selectedCompanyRole = httpContext.Session.GetString("SelectedCompanyRole");

        if (!string.IsNullOrWhiteSpace(selectedCompanyId))
        {
            claims.Add(new Claim("selected_company_id", selectedCompanyId));
        }
        if (!string.IsNullOrWhiteSpace(selectedCompanySlug))
        {
            claims.Add(new Claim("selected_company_slug", selectedCompanySlug));
        }
        if (!string.IsNullOrWhiteSpace(selectedCompanyRole))
        {
            claims.Add(new Claim("selected_company_role", selectedCompanyRole));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    private async Task SignOutCookieAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private ISession GetSession()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null)
        {
            throw new InvalidOperationException("Session is not available.");
        }

        return session;
    }

    private static HttpRequestMessage CreateRequestWithBody(HttpMethod method, string endpoint, object? body)
    {
        var request = new HttpRequestMessage(method, endpoint);
        if (body != null)
        {
            request.Content = JsonContent(body);
        }

        return request;
    }

    private static StringContent JsonContent(object body)
    {
        var json = JsonSerializer.Serialize(body);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            var contentClone = new ByteArrayContent(contentBytes);
            foreach (var header in request.Content.Headers)
            {
                contentClone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = contentClone;
        }

        return clone;
    }

    private static ApiException CreateApiException(System.Net.HttpStatusCode statusCode, string body)
    {
        var message = ExtractMessage(body);
        return (int)statusCode switch
        {
            400 => new ApiBadRequestException(message),
            401 => new ApiUnauthorizedException(message),
            403 => new ApiForbiddenException(message),
            404 => new ApiNotFoundException(message),
            _ => new ApiException((int)statusCode, string.IsNullOrWhiteSpace(message) ? $"API request failed with status {(int)statusCode}." : message)
        };
    }

    private static string ExtractMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                {
                    return string.Join("; ", messages.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
                }
                if (root.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? string.Empty;
                }
                if (root.TryGetProperty("title", out var title))
                {
                    return title.GetString() ?? string.Empty;
                }
            }

            return body;
        }
        catch
        {
            return body;
        }
    }
}
