using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Auth;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

public class UpdateUserTests(EagleBankApiFactory factory) : IClassFixture<EagleBankApiFactory>
{
    private readonly EagleBankApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 200
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUser_WithValidRequest_Returns200()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var response = await client.PatchAsJsonAsync($"/v1/users/{user.Id}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUser_WithValidRequest_ReturnsUpdatedFields()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var response = await client.PatchAsJsonAsync($"/v1/users/{user.Id}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        body!.Name.Should().Be("Updated Name");
        body.Email.Should().Be(user.Email);
    }

    // -------------------------------------------------------------------------
    // 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUser_WithInvalidPhoneFormat_Returns400WithDetails()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var response = await client.PatchAsync($"/v1/users/{user.Id}",
            JsonBody(new { phoneNumber = "07700900000" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();
        body!.Message.Should().NotBeNullOrWhiteSpace();
        body.Details.Should().NotBeEmpty();
        body.Details.Should().AllSatisfy(d =>
        {
            d.Field.Should().NotBeNullOrWhiteSpace();
            d.Message.Should().NotBeNullOrWhiteSpace();
            d.Type.Should().Be("validation_error");
        });
        body.Details.Should().Contain(d => d.Field.Contains("PhoneNumber", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateUser_WithInvalidEmailFormat_Returns400WithDetails()
    {
        var (user, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var response = await client.PatchAsync($"/v1/users/{user.Id}",
            JsonBody(new { email = "not-an-email" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();
        body!.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    // -------------------------------------------------------------------------
    // 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUser_WithNoToken_Returns401()
    {
        var (user, _) = await CreateUserAndAuthenticate();

        var response = await _client.PatchAsJsonAsync($"/v1/users/{user.Id}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_WithInvalidToken_Returns401()
    {
        var (user, _) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient("this.is.not.valid");

        var response = await client.PatchAsJsonAsync($"/v1/users/{user.Id}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUser_WithAnotherUsersToken_Returns403()
    {
        var (userA, _) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        var response = await clientB.PatchAsJsonAsync($"/v1/users/{userA.Id}", new UpdateUserRequest
        {
            Name = "Malicious Update"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUser_WhenUserNotFound_Returns404()
    {
        var nonExistentId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(nonExistentId);
        var client = AuthenticatedClient(token);

        var response = await client.PatchAsJsonAsync($"/v1/users/{nonExistentId}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // 500
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUser_WhenDatabaseGoesDownAfterAuthentication_Returns500()
    {
        var (user, token) = await CreateUserAndAuthenticate();

        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await downClient.PatchAsJsonAsync($"/v1/users/{user.Id}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateUser_WhenDatabaseGoesDownAfterAuthentication_DoesNotLeakExceptionDetails()
    {
        var (user, token) = await CreateUserAndAuthenticate();

        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await downClient.PatchAsJsonAsync($"/v1/users/{user.Id}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });
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

    private static StringContent JsonBody(object content) =>
        new(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
}
