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
public class UserServiceTests
{
    private IUserRepository _repository = null!;
    private IPasswordHasher _passwordHasher = null!;
    private UserService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _passwordHasher.Hash(Arg.Any<string>()).Returns(x => $"hashed:{x.Arg<string>()}");
        _sut = new UserService(_repository, _passwordHasher, NullLogger<UserService>.Instance);
    }

    // CreateUserAsync

    [Test]
    public async Task CreateUserAsync_ReturnsUserDtoWithCorrectFields()
    {
        // Arrange
        var address = BuildAddress();
        _repository.CreateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        // Act
        var result = await _sut.CreateUserAsync("Jane Doe", address, "+447700900000", "jane@example.com", "password123");

        // Assert
        result.Name.Should().Be("Jane Doe");
        result.PhoneNumber.Should().Be("+447700900000");
        result.Email.Should().Be("jane@example.com");
        result.Address.Line1.Should().Be(address.Line1);
    }

    [Test]
    public async Task CreateUserAsync_GeneratesUserIdWithPrefix()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        // Act
        var result = await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        // Assert
        result.Id.Should().StartWith("usr-");
    }

    [Test]
    public async Task CreateUserAsync_SetsCreatedAndUpdatedTimestamps()
    {
        // Arrange
        var before = DateTime.UtcNow;
        _repository.CreateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        // Act
        var result = await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        // Assert
        result.CreatedTimestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        result.UpdatedTimestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Test]
    public async Task CreateUserAsync_WithDuplicateEmail_ThrowsDuplicateEmailException()
    {
        // Arrange
        _repository.GetByEmailAsync("jane@example.com").Returns(BuildUser());

        // Act
        var act = () => _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        // Assert
        await act.Should().ThrowAsync<DuplicateEmailException>();
    }

    [Test]
    public async Task CreateUserAsync_WithDuplicateEmail_DoesNotCallRepository()
    {
        // Arrange
        _repository.GetByEmailAsync("jane@example.com").Returns(BuildUser());

        // Act
        var act = () => _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        // Assert
        await act.Should().ThrowAsync<DuplicateEmailException>();
        await _repository.DidNotReceive().CreateAsync(Arg.Any<User>());
    }

    [Test]
    public async Task CreateUserAsync_HashesPasswordBeforeStorage()
    {
        // Arrange
        User? capturedUser = null;
        _repository.CreateAsync(Arg.Do<User>(u => capturedUser = u)).Returns(x => x.Arg<User>());

        // Act
        await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        // Assert
        _passwordHasher.Received(1).Hash("password123");
        capturedUser!.PasswordHash.Should().Be("hashed:password123");
    }

    [Test]
    public async Task CreateUserAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<User>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // GetUserAsync

    [Test]
    public async Task GetUserAsync_WhenUserExists_ReturnsUserDto()
    {
        // Arrange
        var user = BuildUser();
        _repository.GetByIdAsync(user.Id).Returns(user);

        // Act
        var result = await _sut.GetUserAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Name.Should().Be(user.Name);
    }

    [Test]
    public async Task GetUserAsync_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<string>()).Returns((User?)null);

        // Act
        var result = await _sut.GetUserAsync("usr-doesnotexist");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetUserAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.GetUserAsync("usr-123");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // UpdateUserAsync

    [Test]
    public async Task UpdateUserAsync_UpdatesOnlyNonNullFields()
    {
        // Arrange
        var user = BuildUser(name: "Old Name", email: "old@example.com");
        _repository.GetByIdAsync(user.Id).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        // Act
        var result = await _sut.UpdateUserAsync(user.Id, name: "New Name", address: null, phoneNumber: null, email: null);

        // Assert
        result.Name.Should().Be("New Name");
        result.Email.Should().Be("old@example.com");
    }

    [Test]
    public async Task UpdateUserAsync_WithEmptyName_DoesNotOverwriteName()
    {
        // Arrange
        var user = BuildUser(name: "Original Name");
        _repository.GetByIdAsync(user.Id).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        // Act
        var result = await _sut.UpdateUserAsync(user.Id, name: "", address: null, phoneNumber: null, email: null);

        // Assert
        result.Name.Should().Be("Original Name");
    }

    [Test]
    public async Task UpdateUserAsync_WithEmptyEmail_DoesNotOverwriteEmail()
    {
        // Arrange
        var user = BuildUser(email: "original@example.com");
        _repository.GetByIdAsync(user.Id).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        // Act
        var result = await _sut.UpdateUserAsync(user.Id, name: null, address: null, phoneNumber: null, email: "");

        // Assert
        result.Email.Should().Be("original@example.com");
    }

    [Test]
    public async Task UpdateUserAsync_WithDuplicateEmail_ThrowsDuplicateEmailException()
    {
        // Arrange
        var user = BuildUser(email: "original@example.com");
        _repository.GetByIdAsync(user.Id).Returns(user);
        _repository.GetByEmailAsync("taken@example.com").Returns(BuildUser(email: "taken@example.com"));

        // Act
        var act = () => _sut.UpdateUserAsync(user.Id, name: null, address: null, phoneNumber: null, email: "taken@example.com");

        // Assert
        await act.Should().ThrowAsync<DuplicateEmailException>();
    }

    [Test]
    public async Task UpdateUserAsync_WithSameEmail_DoesNotCheckForDuplicate()
    {
        // Arrange
        var user = BuildUser(email: "same@example.com");
        _repository.GetByIdAsync(user.Id).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        // Act
        await _sut.UpdateUserAsync(user.Id, name: null, address: null, phoneNumber: null, email: "same@example.com");

        // Assert
        await _repository.DidNotReceive().GetByEmailAsync(Arg.Any<string>());
    }

    [Test]
    public async Task UpdateUserAsync_UpdatesTimestamp()
    {
        // Arrange
        var user = BuildUser();
        var originalTimestamp = user.UpdatedTimestamp;
        _repository.GetByIdAsync(user.Id).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        // Act
        var result = await _sut.UpdateUserAsync(user.Id, name: "New Name", address: null, phoneNumber: null, email: null);

        // Assert
        result.UpdatedTimestamp.Should().BeAfter(originalTimestamp);
    }

    [Test]
    public async Task UpdateUserAsync_WhenUserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<string>()).Returns((User?)null);

        // Act
        var act = () => _sut.UpdateUserAsync("usr-ghost", "New Name", null, null, null);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task UpdateUserAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.UpdateUserAsync("usr-123", "New Name", null, null, null);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // DeleteUserAsync

    [Test]
    public async Task DeleteUserAsync_WhenUserExists_CallsRepositoryDelete()
    {
        // Arrange
        var user = BuildUser();
        _repository.GetByIdAsync(user.Id).Returns(user);

        // Act
        await _sut.DeleteUserAsync(user.Id);

        // Assert
        await _repository.Received(1).DeleteAsync(user);
    }

    [Test]
    public async Task DeleteUserAsync_WhenUserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<string>()).Returns((User?)null);

        // Act
        var act = () => _sut.DeleteUserAsync("usr-ghost");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task DeleteUserAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.DeleteUserAsync("usr-123");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    private static Address BuildAddress() => new()
    {
        Line1 = "123 Test Street",
        Town = "London",
        County = "Greater London",
        Postcode = "EC1A 1BB"
    };

    private static User BuildUser(string name = "Jane Doe", string email = "jane@example.com") => new()
    {
        Id = $"usr-{Guid.NewGuid():N}",
        Name = name,
        Address = BuildAddress(),
        PhoneNumber = "+447700900000",
        Email = email,
        PasswordHash = "hashed:password123",
        CreatedTimestamp = DateTime.UtcNow.AddMinutes(-5),
        UpdatedTimestamp = DateTime.UtcNow.AddMinutes(-5)
    };
}
