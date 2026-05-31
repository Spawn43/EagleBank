using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs.Accounts;
using EagleBank.Api.DTOs.Auth;
using EagleBank.Api.DTOs.Users;
using EagleBank.Domain.Models;

namespace EagleBank.AcceptanceTests;

public abstract class AcceptanceTestBase
{
    protected EagleBankApiFactory _factory = null!;
    protected HttpClient _client = null!;

    [OneTimeSetUp]
    public void BaseOneTimeSetUp()
    {
        _factory = new EagleBankApiFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task BaseOneTimeTearDown() => await _factory.DisposeAsync();

    protected async Task<(UserResponse user, string token)> CreateUserAndAuthenticate()
    {
        var email = $"test-{Guid.NewGuid()}@example.com";
        var createRequest = new CreateUserRequest
        {
            Name = "Test User",
            Address = new AddressDto { Line1 = "1 Test Street", Town = "London", County = "Greater London", Postcode = "EC1A 1BB" },
            PhoneNumber = "+447700900000",
            Email = email,
            Password = "password123"
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/users", createRequest);
        var user = (await createResponse.Content.ReadFromJsonAsync<UserResponse>())!;

        var tokenRequest = new AuthRequest { Email = email, Password = "password123" };
        var tokenResponse = await _client.PostAsJsonAsync("/v1/auth/token", tokenRequest);
        var tokenBody = (await tokenResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        return (user, tokenBody.Token);
    }

    protected HttpClient AuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task<(BankAccountResponse account, string token)> CreateAccountAndAuthenticate()
    {
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new CreateBankAccountRequest { Name = "My Account", AccountType = AccountType.Personal };
        var response = await client.PostAsJsonAsync("/v1/accounts", request, TestSerializerOptions.Default);
        var account = (await response.Content.ReadFromJsonAsync<BankAccountResponse>(TestSerializerOptions.Default))!;
        return (account, token);
    }
}
