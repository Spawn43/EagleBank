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

public class AuthTests(EagleBankApiFactory factory) : IClassFixture<EagleBankApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 200
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Token_WithValidCredentials_Returns200()
    {
        var email = await CreateUser();

        var response = await _client.PostAsJsonAsync("/v1/auth/token", new AuthRequest
        {
            Email = email,
            Password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_WithValidCredentials_ReturnsNonEmptyToken()
    {
        var email = await CreateUser();

        var response = await _client.PostAsJsonAsync("/v1/auth/token", new AuthRequest
        {
            Email = email,
            Password = "password123"
        });

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Token_WithValidCredentials_ReturnsValidJwtFormat()
    {
        var email = await CreateUser();

        var response = await _client.PostAsJsonAsync("/v1/auth/token", new AuthRequest
        {
            Email = email,
            Password = "password123"
        });

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        // JWT format: three base64 segments separated by dots
        body!.Token.Split('.').Should().HaveCount(3);
    }

    // -------------------------------------------------------------------------
    // 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Token_WithMissingEmail_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/auth/token",
            JsonBody(new { password = "password123" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();
        body!.Message.Should().NotBeNullOrWhiteSpace();
        body.Details.Should().NotBeEmpty();
        body.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Token_WithMissingPassword_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/auth/token",
            JsonBody(new { email = "jane@example.com" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();
        body!.Details.Should().Contain(d => d.Field.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Token_WithInvalidEmailFormat_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/auth/token",
            JsonBody(new { email = "not-an-email", password = "password123" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();
        body!.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    // -------------------------------------------------------------------------
    // 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Token_WithWrongPassword_Returns401()
    {
        var email = await CreateUser();

        var response = await _client.PostAsJsonAsync("/v1/auth/token", new AuthRequest
        {
            Email = email,
            Password = "wrongpassword"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_WithUnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/v1/auth/token", new AuthRequest
        {
            Email = "nobody@example.com",
            Password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_WithWrongPassword_AndUnknownEmail_ReturnSameMessage()
    {
        // Prevents user enumeration — attacker cannot distinguish
        // between "email doesn't exist" and "wrong password"
        var email = await CreateUser();

        var wrongPasswordResponse = await _client.PostAsJsonAsync("/v1/auth/token",
            new AuthRequest { Email = email, Password = "wrongpassword" });
        var unknownEmailResponse = await _client.PostAsJsonAsync("/v1/auth/token",
            new AuthRequest { Email = "nobody@example.com", Password = "password123" });

        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        var unknownEmailBody = await unknownEmailResponse.Content.ReadFromJsonAsync<ErrorResponse>();

        wrongPasswordBody!.Message.Should().Be(unknownEmailBody!.Message);
    }

    // -------------------------------------------------------------------------
    // 500
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Token_WhenDatabaseDown_Returns500()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/token", new AuthRequest
        {
            Email = "jane@example.com",
            Password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Token_WhenDatabaseDown_DoesNotLeakExceptionDetails()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/token", new AuthRequest
        {
            Email = "jane@example.com",
            Password = "password123"
        });

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<string> CreateUser()
    {
        var email = $"jane-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/v1/users", new CreateUserRequest
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
        return email;
    }

    private static StringContent JsonBody(object content) =>
        new(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
}
