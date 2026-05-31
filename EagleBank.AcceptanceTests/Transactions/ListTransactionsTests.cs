using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Transactions;
using EagleBank.Domain.Models;
using FluentAssertions;

using JsonOpts = EagleBank.AcceptanceTests.Helpers.TestSerializerOptions;

namespace EagleBank.AcceptanceTests.Transactions;

[TestFixture]
public class ListTransactionsTests : AcceptanceTestBase
{
    // 200

    [Test]
    public async Task ListTransactions_WhenOwner_Returns200WithTransactionList()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var depositRequest = new CreateTransactionRequest { Amount = 50, Currency = "GBP", Type = TransactionType.Deposit };
        await client.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", depositRequest, JsonOpts.Default);

        // Act
        var response = await client.GetAsync($"/v1/accounts/{account.AccountNumber}/transactions");
        var body = await response.Content.ReadFromJsonAsync<ListTransactionsResponse>(JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Transactions.Should().HaveCount(1);
        body.Transactions[0].Amount.Should().Be(50);
        body.Transactions[0].Type.Should().Be(TransactionType.Deposit);
    }

    [Test]
    public async Task ListTransactions_WhenNoTransactions_ReturnsEmptyList()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/accounts/{account.AccountNumber}/transactions");
        var body = await response.Content.ReadFromJsonAsync<ListTransactionsResponse>(JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Transactions.Should().BeEmpty();
    }

    // 401

    [Test]
    public async Task ListTransactions_WithNoToken_Returns401()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();

        // Act
        var response = await _client.GetAsync($"/v1/accounts/{account.AccountNumber}/transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task ListTransactions_WhenNotOwner_Returns403()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.GetAsync($"/v1/accounts/{account.AccountNumber}/transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 404

    [Test]
    public async Task ListTransactions_WhenAccountNotFound_Returns404()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts/01999999/transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 500

    [Test]
    public async Task ListTransactions_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.GetAsync("/v1/accounts/01123456/transactions");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body!.Message.Should().Be("An unexpected error occurred");
    }

}
