using EagleBank.Domain.DTOs;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using EagleBank.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EagleBank.UnitTests.Services;

[TestFixture]
public class AuthServiceTests
{
    private IUserRepository _repository = null!;
    private IPasswordHasher _passwordHasher = null!;
    private ITokenGenerator _tokenGenerator = null!;
    private AuthService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _tokenGenerator = Substitute.For<ITokenGenerator>();
        _tokenGenerator.GenerateToken(Arg.Any<UserDto>()).Returns("test.jwt.token");
        _sut = new AuthService(_repository, _passwordHasher, _tokenGenerator, NullLogger<AuthService>.Instance);
    }

    [Test]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsTokenAndCallsGenerateToken()
    {
        // Arrange
        var user = BuildUser();
        _repository.GetByEmailAsync(user.Email).Returns(user);
        _passwordHasher.Verify("password123", user.PasswordHash).Returns(true);

        // Act
        var result = await _sut.AuthenticateAsync(user.Email, "password123");

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be("test.jwt.token");
        _tokenGenerator.Received(1).GenerateToken(Arg.Is<UserDto>(dto => dto.Id == user.Id));
    }

    [Test]
    public async Task AuthenticateAsync_WithUnknownEmail_ReturnsNullAndDoesNotCallGenerateToken()
    {
        // Arrange
        _repository.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        // Act
        var result = await _sut.AuthenticateAsync("unknown@example.com", "password123");

        // Assert
        result.Should().BeNull();
        _tokenGenerator.DidNotReceive().GenerateToken(Arg.Any<UserDto>());
    }

    [Test]
    public async Task AuthenticateAsync_WithWrongPassword_ReturnsNullAndDoesNotCallGenerateToken()
    {
        // Arrange
        var user = BuildUser();
        _repository.GetByEmailAsync(user.Email).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        var result = await _sut.AuthenticateAsync(user.Email, "wrongpassword");

        // Assert
        result.Should().BeNull();
        _tokenGenerator.DidNotReceive().GenerateToken(Arg.Any<UserDto>());
    }

    [Test]
    public async Task AuthenticateAsync_WithUnknownEmail_StillCallsPasswordVerify()
    {
        // Arrange
        _repository.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        // Act
        await _sut.AuthenticateAsync("unknown@example.com", "password123");

        // Assert
        _passwordHasher.Received(1).Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task AuthenticateAsync_WhenDatabaseDown_PropagatesException()
    {
        // Arrange
        _repository.GetByEmailAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var act = () => _sut.AuthenticateAsync("jane@example.com", "password123");

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    private static User BuildUser() => new()
    {
        Id = $"usr-{Guid.NewGuid():N}",
        Name = "Jane Doe",
        Address = new Address
        {
            Line1 = "123 Test Street",
            Town = "London",
            County = "Greater London",
            Postcode = "EC1A 1BB"
        },
        PhoneNumber = "+447700900000",
        Email = "jane@example.com",
        PasswordHash = "hashed:password123",
        CreatedTimestamp = DateTime.UtcNow.AddMinutes(-5),
        UpdatedTimestamp = DateTime.UtcNow.AddMinutes(-5)
    };
}
