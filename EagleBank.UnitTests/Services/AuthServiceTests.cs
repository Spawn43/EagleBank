using EagleBank.Domain.DTOs;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using EagleBank.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EagleBank.UnitTests.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokenGenerator.GenerateToken(Arg.Any<UserDto>()).Returns("test.jwt.token");
        _sut = new AuthService(_repository, _passwordHasher, _tokenGenerator, NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsAuthDto()
    {
        var user = BuildUser();
        _repository.GetByEmailAsync(user.Email).Returns(user);
        _passwordHasher.Verify("password123", user.PasswordHash).Returns(true);

        var result = await _sut.AuthenticateAsync(user.Email, "password123");

        result.Should().NotBeNull();
        result!.Token.Should().Be("test.jwt.token");
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_CallsGenerateToken()
    {
        var user = BuildUser();
        _repository.GetByEmailAsync(user.Email).Returns(user);
        _passwordHasher.Verify("password123", user.PasswordHash).Returns(true);

        await _sut.AuthenticateAsync(user.Email, "password123");

        _tokenGenerator.Received(1).GenerateToken(Arg.Is<UserDto>(dto => dto.Id == user.Id));
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnknownEmail_ReturnsNull()
    {
        _repository.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        var result = await _sut.AuthenticateAsync("unknown@example.com", "password123");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithWrongPassword_ReturnsNull()
    {
        var user = BuildUser();
        _repository.GetByEmailAsync(user.Email).Returns(user);
        _passwordHasher.Verify("wrongpassword", user.PasswordHash).Returns(false);

        var result = await _sut.AuthenticateAsync(user.Email, "wrongpassword");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnknownEmail_DoesNotCallGenerateToken()
    {
        _repository.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        await _sut.AuthenticateAsync("unknown@example.com", "password123");

        _tokenGenerator.DidNotReceive().GenerateToken(Arg.Any<UserDto>());
    }

    [Fact]
    public async Task AuthenticateAsync_WithWrongPassword_DoesNotCallGenerateToken()
    {
        var user = BuildUser();
        _repository.GetByEmailAsync(user.Email).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.AuthenticateAsync(user.Email, "wrongpassword");

        _tokenGenerator.DidNotReceive().GenerateToken(Arg.Any<UserDto>());
    }

    [Fact]
    public async Task AuthenticateAsync_WhenDatabaseDown_PropagatesException()
    {
        _repository.GetByEmailAsync(Arg.Any<string>()).ThrowsAsync(new Exception("Database connection failed"));

        var act = () => _sut.AuthenticateAsync("jane@example.com", "password123");

        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

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
