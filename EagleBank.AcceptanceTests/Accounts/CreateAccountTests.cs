using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
public class CreateAccountTests
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

    // 201

    [Test]
    public async Task CreateAccount_WithValidRequest_Returns201()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = ValidRequest();

        // Act
        var response = await client.PostAsJsonAsync("/v1/accounts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateAccount_WithValidRequest_ReturnsBankAccountResponse()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = ValidRequest();

        // Act
        var response = await client.PostAsJsonAsync("/v1/accounts", request, JsonOpts.Default);
        var body = await response.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default);

        // Assert
        body.Should().NotBeNull();
        body!.AccountNumber.Should().MatchRegex(@"^01\d{6}$");
        body.SortCode.Should().Be("10-10-10");
        body.Name.Should().Be(request.Name);
        body.AccountType.Should().Be(request.AccountType!.Value);
        body.Balance.Should().Be(0);
        body.Currency.Should().Be("GBP");
        body.CreatedTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        body.UpdatedTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // 400

    [Test]
    public async Task CreateAccount_WithMissingName_Returns400WithDetails()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var body = JsonBody(new { accountType = "personal" });

        // Act
        var response = await client.PostAsync("/v1/accounts", body);
        var responseBody = await AssertBadRequest(response);

        // Assert
        responseBody.Details.Should().Contain(d => d.Field.Contains("Name", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task CreateAccount_WithMissingAccountType_Returns400WithDetails()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var body = JsonBody(new { name = "My Account" });

        // Act
        var response = await client.PostAsync("/v1/accounts", body);
        var responseBody = await AssertBadRequest(response);

        // Assert
        responseBody.Details.Should().Contain(d => d.Field.Contains("AccountType", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task CreateAccount_WithInvalidAccountType_Returns400()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var body = JsonBody(new { name = "My Account", accountType = "savings" });

        // Act
        var response = await client.PostAsync("/v1/accounts", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 401

    [Test]
    public async Task CreateAccount_WithNoToken_Returns401()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/v1/accounts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 500

    [Test]
    public async Task CreateAccount_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = ValidRequest();

        // Act
        var response = await downClient.PostAsJsonAsync("/v1/accounts", request);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body!.Message.Should().Be("An unexpected error occurred");
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

    private static async Task<BadRequestErrorResponse> AssertBadRequest(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
        body.Details.Should().NotBeEmpty();
        return body;
    }

    private static CreateBankAccountRequest ValidRequest() => new()
    {
        Name = "My Account",
        AccountType = Domain.Models.AccountType.Personal
    };

    private static StringContent JsonBody(object content) =>
        new(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
}
