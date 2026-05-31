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
public class ListAccountsTests : AcceptanceTestBase
{
    // 200

    [Test]
    public async Task ListAccounts_Returns200()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListAccounts_ReturnsOnlyAuthenticatedUsersAccounts()
    {
        // Arrange
        var (_, tokenA) = await CreateUserAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientA = AuthenticatedClient(tokenA);
        var clientB = AuthenticatedClient(tokenB);

        var accountRequest = new CreateBankAccountRequest { Name = "User A Account", AccountType = Domain.Models.AccountType.Personal };
        await clientA.PostAsJsonAsync("/v1/accounts", accountRequest, JsonOpts.Default);
        await clientA.PostAsJsonAsync("/v1/accounts", accountRequest, JsonOpts.Default);

        var accountRequestB = new CreateBankAccountRequest { Name = "User B Account", AccountType = Domain.Models.AccountType.Personal };
        await clientB.PostAsJsonAsync("/v1/accounts", accountRequestB, JsonOpts.Default);

        // Act
        var response = await clientA.GetAsync("/v1/accounts");
        var body = await response.Content.ReadFromJsonAsync<ListBankAccountsResponse>(JsonOpts.Default);

        // Assert
        body.Should().NotBeNull();
        body!.Accounts.Should().HaveCount(2);
        body.Accounts.Should().AllSatisfy(a => a.Name.Should().Be("User A Account"));
    }

    [Test]
    public async Task ListAccounts_WhenUserHasNoAccounts_ReturnsEmptyList()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts");
        var body = await response.Content.ReadFromJsonAsync<ListBankAccountsResponse>(JsonOpts.Default);

        // Assert
        body.Should().NotBeNull();
        body!.Accounts.Should().BeEmpty();
    }

    // 401

    [Test]
    public async Task ListAccounts_WithNoToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 500

    [Test]
    public async Task ListAccounts_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.GetAsync("/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
