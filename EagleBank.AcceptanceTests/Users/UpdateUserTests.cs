using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EagleBank.AcceptanceTests;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

[TestFixture]
public class UpdateUserTests : AcceptanceTestBase
{
    // 200

    [Test]
    public async Task UpdateUser_WithValidRequest_Returns200()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new UpdateUserRequest { Name = "Updated Name" };

        // Act
        var response = await client.PatchAsJsonAsync($"/v1/users/{user.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task UpdateUser_WithValidRequest_ReturnsUpdatedFields()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new UpdateUserRequest { Name = "Updated Name" };

        // Act
        var response = await client.PatchAsJsonAsync($"/v1/users/{user.Id}", request);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        // Assert
        body!.Name.Should().Be("Updated Name");
        body.Email.Should().Be(user.Email);
    }

    // 400

    [Test]
    public async Task UpdateUser_WithInvalidPhoneFormat_Returns400WithDetails()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var body = JsonBody(new { phoneNumber = "07700900000" });

        // Act
        var response = await client.PatchAsync($"/v1/users/{user.Id}", body);
        var responseBody = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseBody!.Message.Should().NotBeNullOrWhiteSpace();
        responseBody.Details.Should().NotBeEmpty();
        responseBody.Details.Should().AllSatisfy(d =>
        {
            d.Field.Should().NotBeNullOrWhiteSpace();
            d.Message.Should().NotBeNullOrWhiteSpace();
            d.Type.Should().Be("validation_error");
        });
        responseBody.Details.Should().Contain(d => d.Field.Contains("PhoneNumber", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task UpdateUser_WithInvalidEmailFormat_Returns400WithDetails()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var body = JsonBody(new { email = "not-an-email" });

        // Act
        var response = await client.PatchAsync($"/v1/users/{user.Id}", body);
        var responseBody = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseBody!.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    // 409

    [Test]
    public async Task UpdateUser_WithEmailAlreadyTakenByAnotherUser_Returns409()
    {
        // Arrange
        var (userA, tokenA) = await CreateUserAndAuthenticate();
        var (userB, _) = await CreateUserAndAuthenticate();
        var clientA = AuthenticatedClient(tokenA);
        var request = new UpdateUserRequest { Email = userB.Email };

        // Act
        var response = await clientA.PatchAsJsonAsync($"/v1/users/{userA.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task UpdateUser_WithSameEmail_Returns200()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new UpdateUserRequest { Email = user.Email };

        // Act
        var response = await client.PatchAsJsonAsync($"/v1/users/{user.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 401

    [Test]
    public async Task UpdateUser_WithNoToken_Returns401()
    {
        // Arrange
        var (user, _) = await CreateUserAndAuthenticate();
        var request = new UpdateUserRequest { Name = "Updated Name" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/v1/users/{user.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task UpdateUser_WithAnotherUsersToken_Returns403()
    {
        // Arrange
        var (userA, _) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);
        var request = new UpdateUserRequest { Name = "Malicious Update" };

        // Act
        var response = await clientB.PatchAsJsonAsync($"/v1/users/{userA.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 404

    [Test]
    public async Task UpdateUser_WhenUserNotFound_Returns404()
    {
        // Arrange
        var nonExistentId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(nonExistentId);
        var client = AuthenticatedClient(token);
        var request = new UpdateUserRequest { Name = "Updated Name" };

        // Act
        var response = await client.PatchAsJsonAsync($"/v1/users/{nonExistentId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 500

    [Test]
    public async Task UpdateUser_WhenDatabaseGoesDownAfterAuthentication_Returns500()
    {
        // Arrange
        var (user, token) = await CreateUserAndAuthenticate();
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new UpdateUserRequest { Name = "Updated Name" };

        // Act
        var response = await downClient.PatchAsJsonAsync($"/v1/users/{user.Id}", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    private static StringContent JsonBody(object content) =>
        new(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
}
