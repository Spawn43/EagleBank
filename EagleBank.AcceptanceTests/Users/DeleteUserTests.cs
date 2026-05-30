using System.Net;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

public class DeleteUserTests(EagleBankApiFactory factory) : IClassFixture<EagleBankApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DeleteUser_WhenUserExists_Returns204()
    {
        var created = await CreateUser();

        var response = await _client.DeleteAsync($"/v1/users/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteUser_WhenUserExists_UserIsNoLongerRetrievable()
    {
        var created = await CreateUser();
        await _client.DeleteAsync($"/v1/users/{created.Id}");

        var response = await _client.GetAsync($"/v1/users/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_WhenUserNotFound_Returns404()
    {
        var response = await _client.DeleteAsync("/v1/users/usr-doesnotexist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_WhenUserNotFound_ReturnsErrorResponseShape()
    {
        var response = await _client.DeleteAsync("/v1/users/usr-doesnotexist");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeleteUser_WhenDatabaseDown_Returns500()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.DeleteAsync("/v1/users/usr-123");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DeleteUser_WhenDatabaseDown_DoesNotLeakExceptionDetails()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.DeleteAsync("/v1/users/usr-123");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

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
