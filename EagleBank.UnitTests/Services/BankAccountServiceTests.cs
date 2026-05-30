using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using EagleBank.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EagleBank.UnitTests.Services;

[TestFixture]
public class BankAccountServiceTests
{
    private IBankAccountRepository _repository = null!;
    private BankAccountService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IBankAccountRepository>();
        _sut = new BankAccountService(_repository, NullLogger<BankAccountService>.Instance);
    }

    // CreateAccountAsync

    [Test]
    public async Task CreateAccountAsync_ReturnsDto_WithCorrectFields()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<BankAccount>()).Returns(x => x.Arg<BankAccount>());

        // Act
        var result = await _sut.CreateAccountAsync("usr-abc123", "My Account", AccountType.Personal);

        // Assert
        result.Name.Should().Be("My Account");
        result.AccountType.Should().Be(AccountType.Personal);
        result.UserId.Should().Be("usr-abc123");
    }

    [Test]
    public async Task CreateAccountAsync_SetsCorrectDefaults()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<BankAccount>()).Returns(x => x.Arg<BankAccount>());

        // Act
        var result = await _sut.CreateAccountAsync("usr-abc123", "My Account", AccountType.Personal);

        // Assert
        result.SortCode.Should().Be("10-10-10");
        result.Currency.Should().Be("GBP");
        result.Balance.Should().Be(0);
    }

    [Test]
    public async Task CreateAccountAsync_GeneratesAccountNumberMatchingFormat()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<BankAccount>()).Returns(x => x.Arg<BankAccount>());

        // Act
        var result = await _sut.CreateAccountAsync("usr-abc123", "My Account", AccountType.Personal);

        // Assert
        result.AccountNumber.Should().MatchRegex(@"^01\d{6}$");
    }

    [Test]
    public async Task CreateAccountAsync_SetsCreatedAndUpdatedTimestamps()
    {
        // Arrange
        var before = DateTime.UtcNow;
        _repository.CreateAsync(Arg.Any<BankAccount>()).Returns(x => x.Arg<BankAccount>());

        // Act
        var result = await _sut.CreateAccountAsync("usr-abc123", "My Account", AccountType.Personal);

        // Assert
        result.CreatedTimestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        result.UpdatedTimestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Test]
    public async Task CreateAccountAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<BankAccount>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.CreateAccountAsync("usr-abc123", "My Account", AccountType.Personal);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // ListAccountsAsync

    [Test]
    public async Task ListAccountsAsync_ReturnsAccountsForUser()
    {
        // Arrange
        var accounts = new List<BankAccount>
        {
            BuildAccount("01000001", "usr-abc123"),
            BuildAccount("01000002", "usr-abc123")
        };
        _repository.GetByUserIdAsync("usr-abc123").Returns(accounts);

        // Act
        var result = await _sut.ListAccountsAsync("usr-abc123");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.UserId.Should().Be("usr-abc123"));
    }

    [Test]
    public async Task ListAccountsAsync_WhenUserHasNoAccounts_ReturnsEmptyList()
    {
        // Arrange
        _repository.GetByUserIdAsync("usr-abc123").Returns([]);

        // Act
        var result = await _sut.ListAccountsAsync("usr-abc123");

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task ListAccountsAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _repository.GetByUserIdAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.ListAccountsAsync("usr-abc123");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // GetAccountAsync

    [Test]
    public async Task GetAccountAsync_WhenAccountExists_ReturnsDto()
    {
        // Arrange
        var account = BuildAccount("01123456", "usr-abc123");
        _repository.GetByAccountNumberAsync("01123456").Returns(account);

        // Act
        var result = await _sut.GetAccountAsync("01123456");

        // Assert
        result.Should().NotBeNull();
        result!.AccountNumber.Should().Be("01123456");
        result.UserId.Should().Be("usr-abc123");
    }

    [Test]
    public async Task GetAccountAsync_WhenAccountNotFound_ReturnsNull()
    {
        // Arrange
        _repository.GetByAccountNumberAsync(Arg.Any<string>()).Returns((BankAccount?)null);

        // Act
        var result = await _sut.GetAccountAsync("01999999");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetAccountAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _repository.GetByAccountNumberAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.GetAccountAsync("01123456");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    private static BankAccount BuildAccount(string accountNumber, string userId) => new()
    {
        AccountNumber = accountNumber,
        SortCode = "10-10-10",
        Name = "My Account",
        AccountType = AccountType.Personal,
        Balance = 0,
        Currency = "GBP",
        UserId = userId,
        CreatedTimestamp = DateTime.UtcNow.AddMinutes(-5),
        UpdatedTimestamp = DateTime.UtcNow.AddMinutes(-5)
    };
}
