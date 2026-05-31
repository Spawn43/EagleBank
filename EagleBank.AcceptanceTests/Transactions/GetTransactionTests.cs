using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EagleBank.AcceptanceTests.Factories;
using EagleBank.AcceptanceTests.Helpers;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Accounts;
using EagleBank.Api.DTOs.Transactions;
using EagleBank.Domain.Models;
using FluentAssertions;

using JsonOpts = EagleBank.AcceptanceTests.Helpers.TestSerializerOptions;

namespace EagleBank.AcceptanceTests.Transactions;

[TestFixture]
public class GetTransactionTests : AcceptanceTestBase
{
    // 200

    [Test]
    public async Task GetTransaction_WhenValid_Returns200WithTransactionResponse()
    {
        // Arrange
        var (transaction, account, token) = await CreateTransactionAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/accounts/{account.AccountNumber}/transactions/{transaction.Id}");
        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>(JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Id.Should().Be(transaction.Id);
        body.Amount.Should().Be(transaction.Amount);
        body.Type.Should().Be(transaction.Type);
        body.Currency.Should().Be("GBP");
    }

    // 400

    [TestCase("00123456")]
    [TestCase("abc")]
    public async Task GetTransaction_WithInvalidAccountNumberFormat_Returns400(string invalidAccountNumber)
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/accounts/{invalidAccountNumber}/transactions/tan-abc123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestCase("notvalid")]
    [TestCase("abc-123")]
    [TestCase("tan_abc")]
    public async Task GetTransaction_WithInvalidTransactionIdFormat_Returns400(string invalidTransactionId)
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/accounts/01123456/transactions/{invalidTransactionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 401

    [Test]
    public async Task GetTransaction_WithNoToken_Returns401()
    {
        // Arrange
        var (transaction, account, _) = await CreateTransactionAndAuthenticate();

        // Act
        var response = await _client.GetAsync($"/v1/accounts/{account.AccountNumber}/transactions/{transaction.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task GetTransaction_WhenNotOwner_Returns403()
    {
        // Arrange
        var (transaction, account, _) = await CreateTransactionAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);

        // Act
        var response = await clientB.GetAsync($"/v1/accounts/{account.AccountNumber}/transactions/{transaction.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 404 (account not found)

    [Test]
    public async Task GetTransaction_WhenAccountNotFound_Returns404()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/v1/accounts/01999999/transactions/tan-abc123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 404 (transaction not found)

    [Test]
    public async Task GetTransaction_WhenTransactionNotFound_Returns404()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);

        // Act
        var response = await client.GetAsync($"/v1/accounts/{account.AccountNumber}/transactions/tan-doesnotexist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 404 (transaction belongs to different account)

    [Test]
    public async Task GetTransaction_WhenTransactionBelongsToWrongAccount_Returns404()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);

        var accountRequest = new CreateBankAccountRequest { Name = "Account One", AccountType = AccountType.Personal };
        var account1Response = await client.PostAsJsonAsync("/v1/accounts", accountRequest, JsonOpts.Default);
        var account1 = (await account1Response.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default))!;

        var account2Response = await client.PostAsJsonAsync("/v1/accounts", new CreateBankAccountRequest { Name = "Account Two", AccountType = AccountType.Personal }, JsonOpts.Default);
        var account2 = (await account2Response.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default))!;

        var depositRequest = new CreateTransactionRequest { Amount = 50, Currency = "GBP", Type = TransactionType.Deposit };
        var txResponse = await client.PostAsJsonAsync($"/v1/accounts/{account1.AccountNumber}/transactions", depositRequest, JsonOpts.Default);
        var transaction = (await txResponse.Content.ReadFromJsonAsync<TransactionResponse>(JsonOpts.Default))!;

        // Act — use account2 path but transaction from account1
        var response = await client.GetAsync($"/v1/accounts/{account2.AccountNumber}/transactions/{transaction.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 500

    [Test]
    public async Task GetTransaction_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await downClient.GetAsync("/v1/accounts/01123456/transactions/tan-abc123");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body!.Message.Should().Be("An unexpected error occurred");
    }

    private async Task<(TransactionResponse transaction, BankAccountResponse account, string token)> CreateTransactionAndAuthenticate()
    {
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new CreateTransactionRequest { Amount = 75, Currency = "GBP", Type = TransactionType.Deposit };
        var response = await client.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", request, JsonOpts.Default);
        var transaction = (await response.Content.ReadFromJsonAsync<TransactionResponse>(JsonOpts.Default))!;
        return (transaction, account, token);
    }

}
