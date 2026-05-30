using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Accounts;
using EagleBank.Api.DTOs.Auth;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

using JsonOpts = EagleBank.AcceptanceTests.Helpers.TestSerializerOptions;

namespace EagleBank.AcceptanceTests.Accounts;

[TestFixture]
public class GetAccountTests
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
    public async Task GetAccount_WhenOwner_Returns200()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetAccount_WhenOwner_ReturnsBankAccountResponse()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/accounts/{account.AccountNumber}");
        var body = await response.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default);

        // Assert
        body.Should().NotBeNull();
        body!.AccountNumber.Should().Be(account.AccountNumber);
        body.SortCode.Should().Be("10-10-10");
        body.Name.Should().Be(account.Name);
        body.AccountType.Should().Be(account.AccountType);
        body.Balance.Should().Be(account.Balance);
        body.Currency.Should().Be("GBP");
    }

    // 400

    [TestCase("00123456")]
    [TestCase("12345678")]
    [TestCase("011234")]
    [TestCase("abc")]
    [TestCase("01-1234")]
    public async Task GetAccount_WithInvalidAccountNumberFormat_Returns400(string invalidAccountNumber)
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/accounts/{invalidAccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetAccount_WithInvalidAccountNumberFormat_ReturnsBadRequestErrorResponseShape()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts/notvalid");
        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();

        // Assert
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
        body.Details.Should().NotBeEmpty();
    }

    // 401

    [Test]
    public async Task GetAccount_WithNoToken_Returns401()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();

        // Act
        var response = await _client.GetAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAccount_WithInvalidToken_Returns401()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient("this.is.not.valid");

        // Act
        var response = await client.GetAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task GetAccount_WhenNotOwner_Returns403()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.GetAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetAccount_WhenNotOwner_ReturnsErrorResponseShape()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.GetAsync($"/v1/accounts/{account.AccountNumber}");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    // 404

    [Test]
    public async Task GetAccount_WhenAccountNotFound_Returns404()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts/01999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetAccount_WhenAccountNotFound_ReturnsErrorResponseShape()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts/01999998");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    // 500

    [Test]
    public async Task GetAccount_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.GetAsync("/v1/accounts/01123456");

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
