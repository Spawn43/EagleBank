using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Auth;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Auth;

[TestFixture]
public class AuthTests
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
    public async Task Token_WithValidCredentials_Returns200()
    {
        // Arrange
        var email = await CreateUser();
        var request = new AuthRequest { Email = email, Password = "password123" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/token", request);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Token.Split('.').Should().HaveCount(3);
    }

    // 400

    [Test]
    public async Task Token_WithMissingEmail_Returns400WithDetails()
    {
        // Arrange
        var body = JsonBody(new { password = "password123" });

        // Act
        var response = await _client.PostAsync("/v1/auth/token", body);
        var responseBody = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseBody!.Message.Should().NotBeNullOrWhiteSpace();
        responseBody.Details.Should().NotBeEmpty();
        responseBody.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Token_WithMissingPassword_Returns400WithDetails()
    {
        // Arrange
        var body = JsonBody(new { email = "jane@example.com" });

        // Act
        var response = await _client.PostAsync("/v1/auth/token", body);
        var responseBody = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseBody!.Details.Should().Contain(d => d.Field.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Token_WithInvalidEmailFormat_Returns400WithDetails()
    {
        // Arrange
        var body = JsonBody(new { email = "not-an-email", password = "password123" });

        // Act
        var response = await _client.PostAsync("/v1/auth/token", body);
        var responseBody = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseBody!.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    // 401

    [Test]
    public async Task Token_WithWrongPassword_Returns401()
    {
        // Arrange
        var email = await CreateUser();
        var request = new AuthRequest { Email = email, Password = "wrongpassword" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Token_WithUnknownEmail_Returns401()
    {
        // Arrange
        var request = new AuthRequest { Email = "nobody@example.com", Password = "password123" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Token_WithWrongPassword_AndUnknownEmail_ReturnSameMessage()
    {
        // Arrange
        var email = await CreateUser();
        var wrongPasswordRequest = new AuthRequest { Email = email, Password = "wrongpassword" };
        var unknownEmailRequest = new AuthRequest { Email = "nobody@example.com", Password = "password123" };

        // Act
        var wrongPasswordResponse = await _client.PostAsJsonAsync("/v1/auth/token", wrongPasswordRequest);
        var unknownEmailResponse = await _client.PostAsJsonAsync("/v1/auth/token", unknownEmailRequest);
        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        var unknownEmailBody = await unknownEmailResponse.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        wrongPasswordBody!.Message.Should().Be(unknownEmailBody!.Message);
    }

    // 500

    [Test]
    public async Task Token_WhenDatabaseDown_Returns500()
    {
        // Arrange
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();
        var request = new AuthRequest { Email = "jane@example.com", Password = "password123" };

        // Act
        var response = await client.PostAsJsonAsync("/v1/auth/token", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    private async Task<string> CreateUser()
    {
        var email = $"jane-{Guid.NewGuid()}@example.com";
        var request = new CreateUserRequest
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
        };
        await _client.PostAsJsonAsync("/v1/users", request);
        return email;
    }

    private static StringContent JsonBody(object content) =>
        new(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
}
