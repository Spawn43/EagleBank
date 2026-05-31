using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs.Accounts;
using EagleBank.Api.DTOs.Auth;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

using JsonOpts = EagleBank.AcceptanceTests.Helpers.TestSerializerOptions;

namespace EagleBank.AcceptanceTests.Accounts;

[TestFixture]
public class DeleteAccountTests
{
    private EagleBankApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new EagleBankApiFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    // 204

    [Test]
    public async Task DeleteAccount_WhenOwner_Returns204()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.DeleteAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DeleteAccount_WhenOwner_AccountIsGone()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        await client.DeleteAsync($"/v1/accounts/{account.AccountNumber}");
        var getResponse = await client.GetAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 400

    [TestCase("00123456")]
    [TestCase("12345678")]
    [TestCase("011234")]
    [TestCase("abc")]
    [TestCase("01-1234")]
    public async Task DeleteAccount_WithInvalidAccountNumberFormat_Returns400(string invalidAccountNumber)
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.DeleteAsync($"/v1/accounts/{invalidAccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 401

    [Test]
    public async Task DeleteAccount_WithNoToken_Returns401()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();

        // Act
        var response = await _client.DeleteAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task DeleteAccount_WhenNotOwner_Returns403()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.DeleteAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task DeleteAccount_WhenNotOwner_AccountIsNotDeleted()
    {
        // Arrange
        var (account, tokenA) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientA = AuthenticatedClient(tokenA);
        var clientB = AuthenticatedClient(tokenB);

        // Act
        await clientB.DeleteAsync($"/v1/accounts/{account.AccountNumber}");
        var getResponse = await clientA.GetAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 404

    [Test]
    public async Task DeleteAccount_WhenAccountNotFound_Returns404()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.DeleteAsync("/v1/accounts/01999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 500

    [Test]
    public async Task DeleteAccount_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.DeleteAsync("/v1/accounts/01123456");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    private async Task<(BankAccountResponse account, string token)> CreateAccountAndAuthenticate()
    {
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var accountRequest = new CreateBankAccountRequest
        {
            Name = "My Account",
            AccountType = Domain.Models.AccountType.Personal
        };
        var accountResponse = await client.PostAsJsonAsync("/v1/accounts", accountRequest, JsonOpts.Default);
        var account = (await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default))!;

        return (account, token);
    }

    private async Task<(UserResponse user, string token)> CreateUserAndAuthenticate()
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

    private HttpClient AuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
