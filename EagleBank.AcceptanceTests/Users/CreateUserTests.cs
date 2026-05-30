using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Users;
using FluentAssertions;

namespace EagleBank.AcceptanceTests.Users;

public class CreateUserTests(EagleBankApiFactory factory) : IClassFixture<EagleBankApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 201
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateUser_WithValidRequest_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/v1/users", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateUser_WithValidRequest_ReturnsUserResponse()
    {
        var request = ValidRequest();

        var response = await _client.PostAsJsonAsync("/v1/users", request);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        body.Should().NotBeNull();
        body!.Id.Should().StartWith("usr-");
        body.Name.Should().Be(request.Name);
        body.Email.Should().Be(request.Email);
        body.PhoneNumber.Should().Be(request.PhoneNumber);
        body.Address.Line1.Should().Be(request.Address.Line1);
        body.CreatedTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        body.UpdatedTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateUser_WithValidRequest_ReturnsCorrelationIdHeader()
    {
        var response = await _client.PostAsJsonAsync("/v1/users", ValidRequest());

        response.Headers.Should().ContainKey("X-Correlation-ID");
        response.Headers.GetValues("X-Correlation-ID").First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateUser_WithProvidedCorrelationId_EchoesItBack()
    {
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/users")
        {
            Content = JsonContent.Create(ValidRequest()),
            Headers = { { "X-Correlation-ID", correlationId } }
        };

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-ID").First().Should().Be(correlationId);
    }

    // -------------------------------------------------------------------------
    // 409
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_Returns409()
    {
        var email = $"duplicate-{Guid.NewGuid()}@example.com";
        var first = ValidRequest();
        first.Email = email;
        await _client.PostAsJsonAsync("/v1/users", first);

        var second = ValidRequest();
        second.Email = email;
        var response = await _client.PostAsJsonAsync("/v1/users", second);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ReturnsErrorResponseShape()
    {
        var email = $"duplicate-{Guid.NewGuid()}@example.com";
        var first = ValidRequest();
        first.Email = email;
        await _client.PostAsJsonAsync("/v1/users", first);

        var second = ValidRequest();
        second.Email = email;
        var response = await _client.PostAsJsonAsync("/v1/users", second);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateUser_SameName_DifferentEmail_Returns201()
    {
        var first = ValidRequest();
        first.Name = "Jane Doe";
        await _client.PostAsJsonAsync("/v1/users", first);

        var second = ValidRequest();
        second.Name = "Jane Doe";
        var response = await _client.PostAsJsonAsync("/v1/users", second);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // -------------------------------------------------------------------------
    // 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateUser_WithMissingPassword_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            name = "Jane Doe",
            address = ValidAddress(),
            phoneNumber = "+447700900000",
            email = "jane@example.com"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUser_WithPasswordTooShort_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            name = "Jane Doe",
            address = ValidAddress(),
            phoneNumber = "+447700900000",
            email = "jane@example.com",
            password = "short"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUser_ResponseDoesNotContainPasswordOrHash()
    {
        var response = await _client.PostAsJsonAsync("/v1/users", ValidRequest());
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainAny("password", "passwordHash", "hash", "Password", "PasswordHash");
    }

    [Fact]
    public async Task CreateUser_ResponseDoesNotContainSubmittedPasswordValue()
    {
        var request = ValidRequest();
        request.Password = "SuperSecret99!";

        var response = await _client.PostAsJsonAsync("/v1/users", request);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("SuperSecret99!");
    }

    [Fact]
    public async Task CreateUser_WithMissingName_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            address = ValidAddress(),
            phoneNumber = "+447700900000",
            email = "jane@example.com"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("Name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUser_WithMissingAddress_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            name = "Jane Doe",
            phoneNumber = "+447700900000",
            email = "jane@example.com"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("Address", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUser_WithMissingAddressLine1_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            name = "Jane Doe",
            address = new { town = "London", county = "Greater London", postcode = "EC1A 1BB" },
            phoneNumber = "+447700900000",
            email = "jane@example.com"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("Line1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUser_WithMissingPhoneNumber_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            name = "Jane Doe",
            address = ValidAddress(),
            email = "jane@example.com"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("PhoneNumber", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUser_WithInvalidPhoneFormat_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            name = "Jane Doe",
            address = ValidAddress(),
            phoneNumber = "07700900000",
            email = "jane@example.com"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("PhoneNumber", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUser_WithMissingEmail_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            name = "Jane Doe",
            address = ValidAddress(),
            phoneNumber = "+447700900000"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmailFormat_Returns400WithDetails()
    {
        var response = await _client.PostAsync("/v1/users", JsonBody(new
        {
            name = "Jane Doe",
            address = ValidAddress(),
            phoneNumber = "+447700900000",
            email = "not-an-email"
        }));

        var body = await AssertBadRequest(response);
        body.Details.Should().Contain(d => d.Field.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    // -------------------------------------------------------------------------
    // 500
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateUser_WhenDatabaseDown_Returns500()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/users", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateUser_WhenDatabaseDown_ReturnsErrorResponseWithMessage()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/users", ValidRequest());
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        body.Should().NotBeNull();
        body!.Message.Should().Be("An unexpected error occurred");
    }

    [Fact]
    public async Task CreateUser_WhenDatabaseDown_DoesNotLeakExceptionDetails()
    {
        await using var downFactory = new DatabaseDownApiFactory();
        var client = downFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/users", ValidRequest());
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("Database connection failed");
        body.Should().NotContain("StackTrace");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<BadRequestErrorResponse> AssertBadRequest(HttpResponseMessage response)
    {
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

        return body;
    }

    private static CreateUserRequest ValidRequest() => new()
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
    };

    private static object ValidAddress() => new
    {
        line1 = "123 Test Street",
        town = "London",
        county = "Greater London",
        postcode = "EC1A 1BB"
    };

    private static StringContent JsonBody(object content) =>
        new(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
}
