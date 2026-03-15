using System.Net.Http.Json;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Admin.Services;

public class AdminAuthService
{
    public string? Token { get; private set; }
    public bool IsAuthenticated => Token is not null;
    public bool MustChangePassword { get; set; }

    public void SetAuth(string token, bool mustChangePassword)
    {
        Token = token;
        MustChangePassword = mustChangePassword;
    }

    public void ClearAuth()
    {
        Token = null;
        MustChangePassword = false;
    }

    public async Task<HttpResponseMessage> LoginAsync(HttpClient http, string username, string password)
    {
        var response = await http.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest(username, password));
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AdminLoginResponse>();
            if (result is not null)
                SetAuth(result.Token, result.MustChangePassword);
        }
        return response;
    }

    public async Task<HttpResponseMessage> ChangePasswordAsync(HttpClient http, string currentPassword, string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/change-password");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        request.Content = JsonContent.Create(new AdminChangePasswordRequest(currentPassword, newPassword));
        return await http.SendAsync(request);
    }
}
