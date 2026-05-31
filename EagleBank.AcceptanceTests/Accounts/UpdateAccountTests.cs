using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs.Accounts;
using FluentAssertions;

using JsonOpts = EagleBank.AcceptanceTests.Helpers.TestSerializerOptions;

namespace EagleBank.AcceptanceTests.Accounts;

[TestFixture]
public class UpdateAccountTests : AcceptanceTestBase
{
    // 200

    [Test]
    public async Task UpdateAccount_WhenOwner_Returns200()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new UpdateBankAccountRequest { Name = "Updated Account Name" };

        // Act
        var response = await client.PatchAsJsonAsync($"/v1/accounts/{account.AccountNumber}", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task UpdateAccount_WhenOwner_ReturnsUpdatedBankAccountResponse()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new UpdateBankAccountRequest { Name = "Updated Account Name" };

        // Act
        var response = await client.PatchAsJsonAsync($"/v1/accounts/{account.AccountNumber}", request, JsonOpts.Default);
        var body = await response.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default);

        // Assert
        body.Should().NotBeNull();
        body!.AccountNumber.Should().Be(account.AccountNumber);
        body.Name.Should().Be("Updated Account Name");
        body.SortCode.Should().Be("10-10-10");
        body.Currency.Should().Be("GBP");
    }

    [Test]
    public async Task UpdateAccount_WhenOwner_UpdatesTimestamp()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new UpdateBankAccountRequest { Name = "Updated Account Name" };

        // Act
        var response = await client.PatchAsJsonAsync($"/v1/accounts/{account.AccountNumber}", request, JsonOpts.Default);
        var body = await response.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default);

        // Assert
        body!.UpdatedTimestamp.Should().BeOnOrAfter(account.UpdatedTimestamp);
    }

    // 400

    [TestCase("00123456")]
    [TestCase("12345678")]
    [TestCase("011234")]
    [TestCase("abc")]
    [TestCase("01-1234")]
    public async Task UpdateAccount_WithInvalidAccountNumberFormat_Returns400(string invalidAccountNumber)
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new UpdateBankAccountRequest { Name = "Updated Name" };

        // Act
        var response = await client.PatchAsJsonAsync($"/v1/accounts/{invalidAccountNumber}", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 401

    [Test]
    public async Task UpdateAccount_WithNoToken_Returns401()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var request = new UpdateBankAccountRequest { Name = "Updated Name" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/v1/accounts/{account.AccountNumber}", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task UpdateAccount_WhenNotOwner_Returns403()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);
        var request = new UpdateBankAccountRequest { Name = "Updated Name" };

        // Act
        var response = await clientB.PatchAsJsonAsync($"/v1/accounts/{account.AccountNumber}", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 404

    [Test]
    public async Task UpdateAccount_WhenAccountNotFound_Returns404()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new UpdateBankAccountRequest { Name = "Updated Name" };

        // Act
        var response = await client.PatchAsJsonAsync("/v1/accounts/01999999", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 500

    [Test]
    public async Task UpdateAccount_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new UpdateBankAccountRequest { Name = "Updated Name" };

        // Act
        var response = await downClient.PatchAsJsonAsync("/v1/accounts/01123456", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

}
