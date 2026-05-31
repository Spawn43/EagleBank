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
public class DeleteAccountTests : AcceptanceTestBase
{
    // 204

    [Test]
    public async Task DeleteAccount_WhenOwner_Returns204()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.DeleteAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DeleteAccount_WhenOwner_AccountIsGone()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        await client.DeleteAsync($"/v1/accounts/{account.AccountNumber}");
        var getResponse = await client.GetAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 400

    [TestCase("00123456")]
    [TestCase("12345678")]
    [TestCase("011234")]
    [TestCase("abc")]
    [TestCase("01-1234")]
    public async Task DeleteAccount_WithInvalidAccountNumberFormat_Returns400(string invalidAccountNumber)
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.DeleteAsync($"/v1/accounts/{invalidAccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 401

    [Test]
    public async Task DeleteAccount_WithNoToken_Returns401()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();

        // Act
        var response = await _client.DeleteAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task DeleteAccount_WhenNotOwner_Returns403()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.DeleteAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task DeleteAccount_WhenNotOwner_AccountIsNotDeleted()
    {
        // Arrange
        var (account, tokenA) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientA = AuthenticatedClient(tokenA);
        var clientB = AuthenticatedClient(tokenB);

        // Act
        await clientB.DeleteAsync($"/v1/accounts/{account.AccountNumber}");
        var getResponse = await clientA.GetAsync($"/v1/accounts/{account.AccountNumber}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 404

    [Test]
    public async Task DeleteAccount_WhenAccountNotFound_Returns404()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.DeleteAsync("/v1/accounts/01999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 500

    [Test]
    public async Task DeleteAccount_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.DeleteAsync("/v1/accounts/01123456");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

}
