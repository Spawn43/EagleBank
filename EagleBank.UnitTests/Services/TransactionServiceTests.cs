using EagleBank.Domain.Exceptions;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using EagleBank.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EagleBank.UnitTests.Services;

[TestFixture]
public class TransactionServiceTests
{
    private IBankAccountRepository _accountRepository = null!;
    private ITransactionRepository _transactionRepository = null!;
    private TransactionService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _accountRepository = Substitute.For<IBankAccountRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _sut = new TransactionService(_accountRepository, _transactionRepository, NullLogger<TransactionService>.Instance);
    }

    // CreateTransactionAsync

    [Test]
    public async Task CreateTransactionAsync_Deposit_IncreasesBalanceAndReturnsDto()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123", balance: 100);
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);
        _accountRepository.UpdateAsync(Arg.Any<BankAccount>()).Returns(x => x.Arg<BankAccount>());
        _transactionRepository.CreateAsync(Arg.Any<Transaction>()).Returns(x => x.Arg<Transaction>());

        // Act
        var result = await _sut.CreateTransactionAsync("01123456", 50, "GBP", TransactionType.Deposit, null, "usr-abc123");

        // Assert
        await _accountRepository.Received(1).UpdateAsync(Arg.Is<BankAccount>(a => a.Balance == 150));
        result.Amount.Should().Be(50);
        result.Type.Should().Be(TransactionType.Deposit);
        result.Currency.Should().Be("GBP");
        result.Id.Should().MatchRegex(@"^tan-[A-Za-z0-9]+$");
        result.CreatedTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [TestCase(200, 100)]
    [TestCase(100, 100)]
    public async Task CreateTransactionAsync_Withdrawal_WithSufficientFunds_DecreasesBalance(decimal balance, decimal amount)
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123", balance);
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);
        _accountRepository.UpdateAsync(Arg.Any<BankAccount>()).Returns(x => x.Arg<BankAccount>());
        _transactionRepository.CreateAsync(Arg.Any<Transaction>()).Returns(x => x.Arg<Transaction>());

        // Act
        var result = await _sut.CreateTransactionAsync("01123456", amount, "GBP", TransactionType.Withdrawal, null, "usr-abc123");

        // Assert
        await _accountRepository.Received(1).UpdateAsync(Arg.Is<BankAccount>(a => a.Balance == balance - amount));
        result.Type.Should().Be(TransactionType.Withdrawal);
    }

    [Test]
    public async Task CreateTransactionAsync_Withdrawal_WithInsufficientFunds_ThrowsInsufficientFundsException()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123", balance: 50);
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);

        // Act
        var act = () => _sut.CreateTransactionAsync("01123456", 100, "GBP", TransactionType.Withdrawal, null, "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<InsufficientFundsException>();
        await _accountRepository.DidNotReceive().UpdateAsync(Arg.Any<BankAccount>());
    }

    [Test]
    public async Task CreateTransactionAsync_WhenAccountNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _accountRepository.GetByAccountNumberAsync(Arg.Any<string>()).Returns((BankAccount?)null);

        // Act
        var act = () => _sut.CreateTransactionAsync("01999999", 50, "GBP", TransactionType.Deposit, null, "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task CreateTransactionAsync_WhenNotOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123", balance: 100);
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);

        // Act
        var act = () => _sut.CreateTransactionAsync("01123456", 50, "GBP", TransactionType.Deposit, null, "usr-other");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task CreateTransactionAsync_StoresReference()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123", balance: 100);
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);
        _accountRepository.UpdateAsync(Arg.Any<BankAccount>()).Returns(x => x.Arg<BankAccount>());
        _transactionRepository.CreateAsync(Arg.Any<Transaction>()).Returns(x => x.Arg<Transaction>());

        // Act
        var result = await _sut.CreateTransactionAsync("01123456", 50, "GBP", TransactionType.Deposit, "ref-001", "usr-abc123");

        // Assert
        result.Reference.Should().Be("ref-001");
    }

    [Test]
    public async Task CreateTransactionAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _accountRepository.GetByAccountNumberAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.CreateTransactionAsync("01123456", 50, "GBP", TransactionType.Deposit, null, "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // ListTransactionsAsync

    [Test]
    public async Task ListTransactionsAsync_ReturnsTransactionsForAccount()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123");
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);
        var transactions = new List<Transaction>
        {
            BuildTransaction("tan-aaa", "01123456"),
            BuildTransaction("tan-bbb", "01123456")
        };
        _transactionRepository.GetByAccountNumberAsync("01123456").Returns(transactions);

        // Act
        var result = await _sut.ListTransactionsAsync("01123456", "usr-abc123");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.AccountNumber.Should().Be("01123456"));
    }

    [Test]
    public async Task ListTransactionsAsync_WhenNoTransactions_ReturnsEmptyList()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123");
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);
        _transactionRepository.GetByAccountNumberAsync("01123456").Returns([]);

        // Act
        var result = await _sut.ListTransactionsAsync("01123456", "usr-abc123");

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task ListTransactionsAsync_WhenAccountNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _accountRepository.GetByAccountNumberAsync(Arg.Any<string>()).Returns((BankAccount?)null);

        // Act
        var act = () => _sut.ListTransactionsAsync("01999999", "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task ListTransactionsAsync_WhenNotOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123");
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);

        // Act
        var act = () => _sut.ListTransactionsAsync("01123456", "usr-other");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task ListTransactionsAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _accountRepository.GetByAccountNumberAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.ListTransactionsAsync("01123456", "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // GetTransactionAsync

    [Test]
    public async Task GetTransactionAsync_WhenValid_ReturnsDto()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123");
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);
        var transaction = BuildTransaction("tan-abc123", "01123456");
        _transactionRepository.GetByIdAsync("tan-abc123").Returns(transaction);

        // Act
        var result = await _sut.GetTransactionAsync("01123456", "tan-abc123", "usr-abc123");

        // Assert
        result.Id.Should().Be("tan-abc123");
        result.AccountNumber.Should().Be("01123456");
        result.Amount.Should().Be(50);
    }

    [Test]
    public async Task GetTransactionAsync_WhenAccountNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _accountRepository.GetByAccountNumberAsync(Arg.Any<string>()).Returns((BankAccount?)null);

        // Act
        var act = () => _sut.GetTransactionAsync("01999999", "tan-abc123", "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task GetTransactionAsync_WhenNotOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123");
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);

        // Act
        var act = () => _sut.GetTransactionAsync("01123456", "tan-abc123", "usr-other");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task GetTransactionAsync_WhenTransactionNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123");
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);
        _transactionRepository.GetByIdAsync(Arg.Any<string>()).Returns((Transaction?)null);

        // Act
        var act = () => _sut.GetTransactionAsync("01123456", "tan-missing", "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task GetTransactionAsync_WhenTransactionBelongsToDifferentAccount_ThrowsKeyNotFoundException()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123");
        _accountRepository.GetByAccountNumberAsync("01123456").Returns(account);
        var transaction = BuildTransaction("tan-abc123", "01999999");
        _transactionRepository.GetByIdAsync("tan-abc123").Returns(transaction);

        // Act
        var act = () => _sut.GetTransactionAsync("01123456", "tan-abc123", "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task GetTransactionAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _accountRepository.GetByAccountNumberAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.GetTransactionAsync("01123456", "tan-abc123", "usr-abc123");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    private static BankAccount BuildAccount(string accountNumber, string userId, decimal balance = 0) => new()
    {
        AccountNumber = accountNumber,
        SortCode = "10-10-10",
        Name = "My Account",
        AccountType = AccountType.Personal,
        Balance = balance,
        Currency = "GBP",
        UserId = userId,
        CreatedTimestamp = DateTime.UtcNow.AddMinutes(-5),
        UpdatedTimestamp = DateTime.UtcNow.AddMinutes(-5)
    };

    private static Transaction BuildTransaction(string id, string accountNumber) => new()
    {
        Id = id,
        AccountNumber = accountNumber,
        Amount = 50,
        Currency = "GBP",
        Type = TransactionType.Deposit,
        UserId = "usr-abc123",
        CreatedTimestamp = DateTime.UtcNow.AddMinutes(-1)
    };
}
