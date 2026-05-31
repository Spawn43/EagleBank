using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

[TestFixture]
public class GetUserTests : AcceptanceTestBase
{
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
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }
}
