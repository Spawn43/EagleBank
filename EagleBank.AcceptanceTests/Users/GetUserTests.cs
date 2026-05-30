using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Auth;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

[TestFixture]
public class GetUserTests
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
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
    }

    // 200

    [Test]
    public async Task GetUser_WhenUserExists_Returns200()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetUser_WhenUserExists_ReturnsCorrectUser()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        // Assert
        body!.Id.Should().Be(user.Id);
        body.Name.Should().Be(user.Name);
        body.Email.Should().Be(user.Email);
    }

    // 401

    [Test]
    public async Task GetUser_WithNoToken_Returns401()
    {
        // Arrange
        var (user, _) = await CreateUserAndAuthenticate();

        // Act
        var response = await _client.GetAsync($"/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetUser_WithInvalidToken_Returns401()
    {
        // Arrange
        var (user, _) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient("this.is.not.valid");

        // Act
        var response = await client.GetAsync($"/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetUser_WithNoToken_ReturnsErrorResponseShape()
    {
        // Arrange
        var (user, _) = await CreateUserAndAuthenticate();

        // Act
        var response = await _client.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    // 403

    [Test]
    public async Task GetUser_WithAnotherUsersToken_Returns403()
    {
        // Arrange
        var (userA, _) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.GetAsync($"/v1/users/{userA.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetUser_WithAnotherUsersToken_ReturnsErrorResponseShape()
    {
        // Arrange
        var (userA, _) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.GetAsync($"/v1/users/{userA.Id}");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    // 404

    [Test]
    public async Task GetUser_WhenUserNotFound_Returns404()
    {
        // Arrange
        var nonExistentId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(nonExistentId);
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 500

    [Test]
    public async Task GetUser_WhenDatabaseGoesDownAfterAuthentication_Returns500()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.GetAsync($"/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Test]
    public async Task GetUser_WhenDatabaseGoesDownAfterAuthentication_DoesNotLeakExceptionDetails()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    // Password / data protection

    [Test]
    public async Task GetUser_ResponseDoesNotContainPasswordOrHash()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        body.Should().NotContainAny("password", "passwordHash", "hash", "Password", "PasswordHash");
    }

    [Test]
    public async Task GetUser_ResponseDoesNotContainSubmittedPasswordValue()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate(password: "SuperSecret99!");
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        body.Should().NotContain("SuperSecret99!");
    }

    private async Task<(UserResponse user, string token)> CreateUserAndAuthenticate(string password = "password123")
    {
        var email = $"jane-{Guid.NewGuid()}@example.com";
        var createRequest = new CreateUserRequest
        {
            Name = "Jane Doe",
            Address = new AddressDto { Line1 = "123 Test Street", Town = "London", County = "Greater London", Postcode = "EC1A 1BB" },
            PhoneNumber = "+447700900000",
            Email = email,
            Password = password
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/users", createRequest);
        var user = (await createResponse.Content.ReadFromJsonAsync<UserResponse>())!;

        var tokenRequest = new AuthRequest { Email = email, Password = password };
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
