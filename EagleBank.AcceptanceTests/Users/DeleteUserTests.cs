using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

[TestFixture]
public class DeleteUserTests : AcceptanceTestBase
{
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
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }
}
