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
public class ListAccountsTests
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

    // 200

    [Test]
    public async Task ListAccounts_Returns200()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListAccounts_ReturnsOnlyAuthenticatedUsersAccounts()
    {
        // Arrange
        var (_, tokenA) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientA = AuthenticatedClient(tokenA);
        var clientB = AuthenticatedClient(tokenB);

        var accountRequest = new CreateBankAccountRequest { Name = "User A Account", AccountType = Domain.Models.AccountType.Personal };
        await clientA.PostAsJsonAsync("/v1/accounts", accountRequest, JsonOpts.Default);
        await clientA.PostAsJsonAsync("/v1/accounts", accountRequest, JsonOpts.Default);

        var accountRequestB = new CreateBankAccountRequest { Name = "User B Account", AccountType = Domain.Models.AccountType.Personal };
        await clientB.PostAsJsonAsync("/v1/accounts", accountRequestB, JsonOpts.Default);

        // Act
        var response = await clientA.GetAsync("/v1/accounts");
        var body = await response.Content.ReadFromJsonAsync<ListBankAccountsResponse>(JsonOpts.Default);

        // Assert
        body.Should().NotBeNull();
        body!.Accounts.Should().HaveCount(2);
        body.Accounts.Should().AllSatisfy(a => a.Name.Should().Be("User A Account"));
    }

    [Test]
    public async Task ListAccounts_WhenUserHasNoAccounts_ReturnsEmptyList()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts");
        var body = await response.Content.ReadFromJsonAsync<ListBankAccountsResponse>(JsonOpts.Default);

        // Assert
        body.Should().NotBeNull();
        body!.Accounts.Should().BeEmpty();
    }

    // 401

    [Test]
    public async Task ListAccounts_WithNoToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ListAccounts_WithInvalidToken_Returns401()
    {
        // Arrange
        var client = AuthenticatedClient("this.is.not.valid");

        // Act
        var response = await client.GetAsync("/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 500

    [Test]
    public async Task ListAccounts_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.GetAsync("/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
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
