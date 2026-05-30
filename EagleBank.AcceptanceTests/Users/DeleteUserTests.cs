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
public class DeleteUserTests
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

    // 204

    [Test]
    public async Task DeleteUser_WhenUserExists_Returns204()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.DeleteAsync($"/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DeleteUser_WhenUserExists_UserIsNoLongerRetrievable()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        await client.DeleteAsync($"/v1/users/{user.Id}");
        var getResponse = await client.GetAsync($"/v1/users/{user.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 401

    [Test]
    public async Task DeleteUser_WithNoToken_Returns401()
    {
        // Arrange
        var (user, _) = await CreateUserAndAuthenticate();

        // Act
        var response = await _client.DeleteAsync($"/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteUser_WithInvalidToken_Returns401()
    {
        // Arrange
        var (user, _) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient("this.is.not.valid");

        // Act
        var response = await client.DeleteAsync($"/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task DeleteUser_WithAnotherUsersToken_Returns403()
    {
        // Arrange
        var (userA, _) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.DeleteAsync($"/v1/users/{userA.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task DeleteUser_WithAnotherUsersToken_DoesNotDeleteTheUser()
    {
        // Arrange
        var (userA, tokenA) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);
        var clientA = AuthenticatedClient(tokenA);

        // Act
        await clientB.DeleteAsync($"/v1/users/{userA.Id}");
        var getResponse = await clientA.GetAsync($"/v1/users/{userA.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 404

    [Test]
    public async Task DeleteUser_WhenUserNotFound_Returns404()
    {
        // Arrange
        var nonExistentId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(nonExistentId);
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.DeleteAsync($"/v1/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 500

    [Test]
    public async Task DeleteUser_WhenDatabaseGoesDownAfterAuthentication_Returns500()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.DeleteAsync($"/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Test]
    public async Task DeleteUser_WhenDatabaseGoesDownAfterAuthentication_DoesNotLeakExceptionDetails()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.DeleteAsync($"/v1/users/{user.Id}");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    private async Task<(UserResponse user, string token)> CreateUserAndAuthenticate()
    {
        var email = $"jane-{Guid.NewGuid()}@example.com";
        var createRequest = new CreateUserRequest
        {
            Name = "Jane Doe",
            Address = new AddressDto { Line1 = "123 Test Street", Town = "London", County = "Greater London", Postcode = "EC1A 1BB" },
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
