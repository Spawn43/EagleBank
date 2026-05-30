using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

public class UpdateUserTests(EagleBankApiFactory factory) : IClassFixture<EagleBankApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UpdateUser_WithValidRequest_Returns200()
    {
        var created = await CreateUser();

        var response = await _client.PatchAsJsonAsync($"/v1/users/{created.Id}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUser_WithValidRequest_ReturnsUpdatedFields()
    {
        var created = await CreateUser();

        var response = await _client.PatchAsJsonAsync($"/v1/users/{created.Id}", new UpdateUserRequest
        {
            Name = "Updated Name"
        });
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        body!.Name.Should().Be("Updated Name");
        body.Email.Should().Be(created.Email);
    }

    [Fact]
    public async Task UpdateUser_WithInvalidPhoneFormat_Returns400WithDetails()
    {
        var created = await CreateUser();

        var response = await _client.PatchAsync($"/v1/users/{created.Id}", JsonBody(new
        {
            phoneNumber = "07700900000"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();
        body.Should().NotBeNull();
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
        var created = await CreateUser();

        var response = await _client.PatchAsync($"/v1/users/{created.Id}", JsonBody(new
        {
            email = "not-an-email"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
        body.Details.Should().NotBeEmpty();
        body.Details.Should().AllSatisfy(d =>
        {
            d.Field.Should().NotBeNullOrWhiteSpace();
            d.Message.Should().NotBeNullOrWhiteSpace();
            d.Type.Should().Be("validation_error");
        });
        body.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateUser_WhenUserNotFound_Returns404()
    {
        var response = await _client.PatchAsJsonAsync("/v1/users/usr-doesnotexist", new UpdateUserRequest
        {
            Name = "Updated Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateUser_WhenUserNotFound_ReturnsErrorResponseShape()
    {
        var response = await _client.PatchAsJsonAsync("/v1/users/usr-doesnotexist", new UpdateUserRequest
        {
            Name = "Updated Name"
        });
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateUser_WhenDatabaseDown_Returns500()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.PatchAsJsonAsync("/v1/users/usr-123", new UpdateUserRequest
        {
            Name = "Updated Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateUser_WhenDatabaseDown_DoesNotLeakExceptionDetails()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.PatchAsJsonAsync("/v1/users/usr-123", new UpdateUserRequest
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

    private static StringContent JsonBody(object content) =>
        new(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");

    private async Task<UserResponse> CreateUser()
    {
        var response = await _client.PostAsJsonAsync("/v1/users", new CreateUserRequest
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
            Email = $"jane-{Guid.NewGuid()}@example.com",
            Password = "password123"
        });

        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }
}
