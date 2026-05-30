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

public class DeleteUserTests(EagleBankApiFactory factory) : IClassFixture<EagleBankApiFactory>
{
    private readonly EagleBankApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 204
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteUser_WhenUserExists_Returns204()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var response = await client.DeleteAsync($"/v1/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteUser_WhenUserExists_UserIsNoLongerRetrievable()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        await client.DeleteAsync($"/v1/users/{user.Id}");
        var getResponse = await client.GetAsync($"/v1/users/{user.Id}");

        // After deletion the user no longer exists — but their own token still passes
        // JWT validation so the auth check passes, returning 404 not 403
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteUser_WithNoToken_Returns401()
    {
        var (user, _) = await CreateUserAndAuthenticate();

        var response = await _client.DeleteAsync($"/v1/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteUser_WithInvalidToken_Returns401()
    {
        var (user, _) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient("this.is.not.valid");

        var response = await client.DeleteAsync($"/v1/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteUser_WithAnotherUsersToken_Returns403()
    {
        var (userA, _) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        var response = await clientB.DeleteAsync($"/v1/users/{userA.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteUser_WithAnotherUsersToken_DoesNotDeleteTheUser()
    {
        var (userA, tokenA) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        await clientB.DeleteAsync($"/v1/users/{userA.Id}");

        // User A should still exist
        var clientA = AuthenticatedClient(tokenA);
        var getResponse = await clientA.GetAsync($"/v1/users/{userA.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteUser_WhenUserNotFound_Returns404()
    {
        var nonExistentId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(nonExistentId);
        var client = AuthenticatedClient(token);

        var response = await client.DeleteAsync($"/v1/users/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // 500
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteUser_WhenDatabaseGoesDownAfterAuthentication_Returns500()
    {
        var (user, token) = await CreateUserAndAuthenticate();

        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await downClient.DeleteAsync($"/v1/users/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DeleteUser_WhenDatabaseGoesDownAfterAuthentication_DoesNotLeakExceptionDetails()
    {
        var (user, token) = await CreateUserAndAuthenticate();

        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await downClient.DeleteAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<(UserResponse user, string token)> CreateUserAndAuthenticate()
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
            Password = "password123"
        });
        var user = (await createResponse.Content.ReadFromJsonAsync<UserResponse>())!;

        var tokenResponse = await _client.PostAsJsonAsync("/v1/auth/token",
            new AuthRequest { Email = email, Password = "password123" });
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
