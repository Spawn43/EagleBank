using EagleBank.Domain.Exceptions;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using EagleBank.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EagleBank.UnitTests.Services;

public class UserServiceTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns(x => $"hashed:{x.Arg<string>()}");
        _sut = new UserService(_repository, _passwordHasher, NullLogger<UserService>.Instance);
    }

    // -------------------------------------------------------------------------
    // CreateUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateUserAsync_ReturnsUserDtoWithCorrectFields()
    {
        var address = BuildAddress();
        _repository.CreateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        var result = await _sut.CreateUserAsync("Jane Doe", address, "+447700900000", "jane@example.com", "password123");

        result.Name.Should().Be("Jane Doe");
        result.PhoneNumber.Should().Be("+447700900000");
        result.Email.Should().Be("jane@example.com");
        result.Address.Line1.Should().Be(address.Line1);
    }

    [Fact]
    public async Task CreateUserAsync_GeneratesUserIdWithPrefix()
    {
        _repository.CreateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        var result = await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        result.Id.Should().StartWith("usr-");
    }

    [Fact]
    public async Task CreateUserAsync_SetsCreatedAndUpdatedTimestamps()
    {
        var before = DateTime.UtcNow;
        _repository.CreateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        var result = await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        result.CreatedTimestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        result.UpdatedTimestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateEmail_ThrowsDuplicateEmailException()
    {
        _repository.GetByEmailAsync("jane@example.com").Returns(BuildUser());

        var act = () => _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        await act.Should().ThrowAsync<DuplicateEmailException>();
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateEmail_DoesNotCallRepository()
    {
        _repository.GetByEmailAsync("jane@example.com").Returns(BuildUser());

        try { await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123"); }
        catch (DuplicateEmailException) { }

        await _repository.DidNotReceive().CreateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task CreateUserAsync_HashesPasswordBeforeStorage()
    {
        User? capturedUser = null;
        _repository.CreateAsync(Arg.Do<User>(u => capturedUser = u)).Returns(x => x.Arg<User>());

        await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        _passwordHasher.Received(1).Hash("password123");
        capturedUser!.PasswordHash.Should().Be("hashed:password123");
    }

    [Fact]
    public async Task CreateUserAsync_DoesNotExposePasswordHashInReturnedDto()
    {
        _repository.CreateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        var result = await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        // UserDto is a record — verify it has no password-related property via reflection
        var properties = result.GetType().GetProperties();
        properties.Should().NotContain(p =>
            p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateUserAsync_StoredPasswordHashIsNotPlainText()
    {
        User? capturedUser = null;
        _repository.CreateAsync(Arg.Do<User>(u => capturedUser = u)).Returns(x => x.Arg<User>());

        await _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        capturedUser!.PasswordHash.Should().NotBe("password123");
        capturedUser.PasswordHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateUserAsync_WhenDatabaseDown_PropagatesException()
    {
        _repository.CreateAsync(Arg.Any<User>()).ThrowsAsync(new Exception("Database connection failed"));

        var act = () => _sut.CreateUserAsync("Jane Doe", BuildAddress(), "+447700900000", "jane@example.com", "password123");

        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // -------------------------------------------------------------------------
    // GetUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUserAsync_WhenUserExists_ReturnsUserDto()
    {
        var user = BuildUser();
        _repository.GetByIdAsync(user.Id).Returns(user);

        var result = await _sut.GetUserAsync(user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Name.Should().Be(user.Name);
    }

    [Fact]
    public async Task GetUserAsync_WhenUserExists_DoesNotExposePasswordHash()
    {
        var user = BuildUser();
        _repository.GetByIdAsync(user.Id).Returns(user);

        var result = await _sut.GetUserAsync(user.Id);

        var properties = result!.GetType().GetProperties();
        properties.Should().NotContain(p =>
            p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetUserAsync_WhenUserNotFound_ReturnsNull()
    {
        _repository.GetByIdAsync(Arg.Any<string>()).Returns((User?)null);

        var result = await _sut.GetUserAsync("usr-doesnotexist");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_WhenDatabaseDown_PropagatesException()
    {
        _repository.GetByIdAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        var act = () => _sut.GetUserAsync("usr-123");

        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // -------------------------------------------------------------------------
    // UpdateUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUserAsync_UpdatesOnlyNonNullFields()
    {
        var user = BuildUser(name: "Old Name", email: "old@example.com");
        _repository.GetByIdAsync(user.Id).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        var result = await _sut.UpdateUserAsync(user.Id, name: "New Name", address: null, phoneNumber: null, email: null);

        result.Name.Should().Be("New Name");
        result.Email.Should().Be("old@example.com");
    }

    [Fact]
    public async Task UpdateUserAsync_UpdatesTimestamp()
    {
        var user = BuildUser();
        var originalTimestamp = user.UpdatedTimestamp;
        _repository.GetByIdAsync(user.Id).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>()).Returns(x => x.Arg<User>());

        var result = await _sut.UpdateUserAsync(user.Id, name: "New Name", address: null, phoneNumber: null, email: null);

        result.UpdatedTimestamp.Should().BeAfter(originalTimestamp);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserNotFound_ThrowsKeyNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<string>()).Returns((User?)null);

        var act = () => _sut.UpdateUserAsync("usr-ghost", "New Name", null, null, null);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateUserAsync_WhenDatabaseDown_PropagatesException()
    {
        _repository.GetByIdAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        var act = () => _sut.UpdateUserAsync("usr-123", "New Name", null, null, null);

        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // -------------------------------------------------------------------------
    // DeleteUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteUserAsync_WhenUserExists_CallsRepositoryDelete()
    {
        var user = BuildUser();
        _repository.GetByIdAsync(user.Id).Returns(user);

        await _sut.DeleteUserAsync(user.Id);

        await _repository.Received(1).DeleteAsync(user);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserNotFound_ThrowsKeyNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<string>()).Returns((User?)null);

        var act = () => _sut.DeleteUserAsync("usr-ghost");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDatabaseDown_PropagatesException()
    {
        _repository.GetByIdAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        var act = () => _sut.DeleteUserAsync("usr-123");

        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

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
