using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
public class CreateTransactionTests : AcceptanceTestBase
{
    // 201

    [Test]
    public async Task CreateTransaction_Deposit_Returns201WithTransactionResponse()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new CreateTransactionRequest { Amount = 100, Currency = "GBP", Type = TransactionType.Deposit };

        // Act
        var response = await client.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", request, JsonOpts.Default);
        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>(JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotBeNull();
        body!.Id.Should().MatchRegex(@"^tan-[A-Za-z0-9]+$");
        body.Amount.Should().Be(100);
        body.Currency.Should().Be("GBP");
        body.Type.Should().Be(TransactionType.Deposit);
        body.UserId.Should().NotBeNullOrWhiteSpace();
        body.CreatedTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task CreateTransaction_Deposit_UpdatesAccountBalance()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new CreateTransactionRequest { Amount = 250, Currency = "GBP", Type = TransactionType.Deposit };

        // Act
        await client.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", request, JsonOpts.Default);
        var accountResponse = await client.GetAsync($"/v1/accounts/{account.AccountNumber}");
        var updatedAccount = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default);

        // Assert
        updatedAccount!.Balance.Should().Be(250);
    }

    [Test]
    public async Task CreateTransaction_Withdrawal_WithSufficientFunds_Returns201()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var depositRequest = new CreateTransactionRequest { Amount = 200, Currency = "GBP", Type = TransactionType.Deposit };
        await client.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", depositRequest, JsonOpts.Default);
        var withdrawalRequest = new CreateTransactionRequest { Amount = 100, Currency = "GBP", Type = TransactionType.Withdrawal };

        // Act
        var response = await client.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", withdrawalRequest, JsonOpts.Default);
        var accountResponse = await client.GetAsync($"/v1/accounts/{account.AccountNumber}");
        var updatedAccount = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>(JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        updatedAccount!.Balance.Should().Be(100);
    }

    // 400

    [TestCase("""{"amount":100,"currency":"GBP"}""", "Type")]
    [TestCase("""{"amount":100,"type":"deposit"}""", "Currency")]
    [TestCase("""{"amount":0,"currency":"GBP","type":"deposit"}""", "Amount")]
    public async Task CreateTransaction_WithInvalidRequest_Returns400WithFieldInDetails(string json, string expectedField)
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var body = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync($"/v1/accounts/{account.AccountNumber}/transactions", body);
        var responseBody = await response.Content.ReadFromJsonAsync<BadRequestErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseBody!.Details.Should().Contain(d => d.Field.Contains(expectedField, StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task CreateTransaction_WithInvalidCurrency_Returns400()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var body = JsonBody(new { amount = 100, currency = "USD", type = "deposit" });

        // Act
        var response = await client.PostAsync($"/v1/accounts/{account.AccountNumber}/transactions", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 401

    [Test]
    public async Task CreateTransaction_WithNoToken_Returns401()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var request = new CreateTransactionRequest { Amount = 100, Currency = "GBP", Type = TransactionType.Deposit };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 403

    [Test]
    public async Task CreateTransaction_WhenNotOwner_Returns403()
    {
        // Arrange
        var (account, _) = await CreateAccountAndAuthenticate();
        var (_, tokenB) = await CreateUserAndAuthenticate();
        var clientB = AuthenticatedClient(tokenB);
        var request = new CreateTransactionRequest { Amount = 100, Currency = "GBP", Type = TransactionType.Deposit };

        // Act
        var response = await clientB.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 404

    [Test]
    public async Task CreateTransaction_WhenAccountNotFound_Returns404()
    {
        // Arrange
        var (_, token) = await CreateUserAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new CreateTransactionRequest { Amount = 100, Currency = "GBP", Type = TransactionType.Deposit };

        // Act
        var response = await client.PostAsJsonAsync("/v1/accounts/01999999/transactions", request, JsonOpts.Default);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 422

    [Test]
    public async Task CreateTransaction_Withdrawal_WithInsufficientFunds_Returns422()
    {
        // Arrange
        var (account, token) = await CreateAccountAndAuthenticate();
        var client = AuthenticatedClient(token);
        var request = new CreateTransactionRequest { Amount = 100, Currency = "GBP", Type = TransactionType.Withdrawal };

        // Act
        var response = await client.PostAsJsonAsync($"/v1/accounts/{account.AccountNumber}/transactions", request, JsonOpts.Default);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    // 500

    [Test]
    public async Task CreateTransaction_WhenDatabaseDown_Returns500()
    {
        // Arrange
        var userId = $"usr-{Guid.NewGuid():N}";
        var token = TokenHelper.GenerateTokenForUser(userId);
        await using var downFactory = new DatabaseDownApiFactory();
        var downClient = downFactory.CreateClient();
        downClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new CreateTransactionRequest { Amount = 100, Currency = "GBP", Type = TransactionType.Deposit };

        // Act
        var response = await downClient.PostAsJsonAsync("/v1/accounts/01123456/transactions", request, JsonOpts.Default);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body!.Message.Should().Be("An unexpected error occurred");
    }

    private static StringContent JsonBody(object content) =>
        new(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
}
