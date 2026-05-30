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

public class GetUserTests(EagleBankApiFactory factory) : IClassFixture<EagleBankApiFactory>
{
    private readonly EagleBankApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 200
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUser_WhenUserExists_Returns200()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var response = await client.GetAsync($"/v1/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUser_WhenUserExists_ReturnsCorrectUser()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var response = await client.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        body!.Id.Should().Be(user.Id);
        body.Name.Should().Be(user.Name);
        body.Email.Should().Be(user.Email);
    }

    // -------------------------------------------------------------------------
    // 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUser_WithNoToken_Returns401()
    {
        var (user, _) = await CreateUserAndAuthenticate();

        var response = await _client.GetAsync($"/v1/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUser_WithInvalidToken_Returns401()
    {
        var (user, _) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient("this.is.not.valid");

        var response = await client.GetAsync($"/v1/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUser_WithNoToken_ReturnsErrorResponseShape()
    {
        var (user, _) = await CreateUserAndAuthenticate();

        var response = await _client.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    // -------------------------------------------------------------------------
    // 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUser_WithAnotherUsersToken_Returns403()
    {
        var (userA, _) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        var response = await clientB.GetAsync($"/v1/users/{userA.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUser_WithAnotherUsersToken_ReturnsErrorResponseShape()
    {
        var (userA, _) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        var response = await clientB.GetAsync($"/v1/users/{userA.Id}");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    // -------------------------------------------------------------------------
    // 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUser_WhenUserNotFound_Returns404()
    {
        var nonExistentId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(nonExistentId);
        var client = AuthenticatedClient(token);

        var response = await client.GetAsync($"/v1/users/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // 500
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUser_WhenDatabaseGoesDownAfterAuthentication_Returns500()
    {
        // Simulate: user authenticates successfully, then the DB goes down,
        // then they make a request with their existing valid token
        var (user, token) = await CreateUserAndAuthenticate();

        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await downClient.GetAsync($"/v1/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetUser_WhenDatabaseGoesDownAfterAuthentication_DoesNotLeakExceptionDetails()
    {
        var (user, token) = await CreateUserAndAuthenticate();

        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await downClient.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    // -------------------------------------------------------------------------
    // Password / data protection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUser_ResponseDoesNotContainPasswordOrHash()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var response = await client.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainAny("password", "passwordHash", "hash", "Password", "PasswordHash");
    }

    [Fact]
    public async Task GetUser_ResponseDoesNotContainSubmittedPasswordValue()
    {
        var (user, token) = await CreateUserAndAuthenticate(password: "SuperSecret99!");
        var client = AuthenticatedClient(token);

        var response = await client.GetAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("SuperSecret99!");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<(UserResponse user, string token)> CreateUserAndAuthenticate(string password = "password123")
    {
        var email = $"jane-{Guid.NewGuid()}@example.com";
        var createResponse = await _client.PostAsJsonAsync("/v1/users", new CreateUserRequest
        {
            Name = "Jane Doe",
            Address = new AddressDto
            {
                Line1 = "123 Test Street",
                Town = "London",
                County = "Greater London",
                Postcode = "EC1A 1BB"
            },
            PhoneNumber = "+447700900000",
            Email = email,
            Password = password
        });
        var user = (await createResponse.Content.ReadFromJsonAsync<UserResponse>())!;

        var tokenResponse = await _client.PostAsJsonAsync("/v1/auth/token",
            new AuthRequest { Email = email, Password = password });
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
