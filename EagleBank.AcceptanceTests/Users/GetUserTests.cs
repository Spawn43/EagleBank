using System.Net;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

public class GetUserTests(EagleBankApiFactory factory) : IClassFixture<EagleBankApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetUser_WhenUserExists_Returns200()
    {
        var created = await CreateUser();

        var response = await _client.GetAsync($"/v1/users/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUser_WhenUserExists_ReturnsCorrectUser()
    {
        var created = await CreateUser();

        var response = await _client.GetAsync($"/v1/users/{created.Id}");
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.Name.Should().Be(created.Name);
        body.Email.Should().Be(created.Email);
    }

    [Fact]
    public async Task GetUser_WhenUserNotFound_Returns404()
    {
        var response = await _client.GetAsync("/v1/users/usr-doesnotexist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUser_WhenUserNotFound_ReturnsErrorResponseShape()
    {
        var response = await _client.GetAsync("/v1/users/usr-doesnotexist");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetUser_ResponseDoesNotContainPasswordOrHash()
    {
        var created = await CreateUser();

        var response = await _client.GetAsync($"/v1/users/{created.Id}");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainAny("password", "passwordHash", "hash", "Password", "PasswordHash");
    }

    [Fact]
    public async Task GetUser_ResponseDoesNotContainSubmittedPasswordValue()
    {
        var created = await CreateUser(password: "SuperSecret99!");

        var response = await _client.GetAsync($"/v1/users/{created.Id}");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("SuperSecret99!");
    }

    [Fact]
    public async Task GetUser_WhenDatabaseDown_Returns500()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.GetAsync("/v1/users/usr-123");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetUser_WhenDatabaseDown_DoesNotLeakExceptionDetails()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.GetAsync("/v1/users/usr-123");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<UserResponse> CreateUser(string password = "password123")
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
            Password = password
        });

        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }
}
