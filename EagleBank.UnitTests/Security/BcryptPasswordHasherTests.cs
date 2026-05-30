using EagleBank.Data.Security;
using FluentAssertions;

namespace EagleBank.UnitTests.Security;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _sut = new();

    // -------------------------------------------------------------------------
    // Hash
    // -------------------------------------------------------------------------

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        var result = _sut.Hash("password123");

        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Hash_DoesNotReturnPlainTextPassword()
    {
        var result = _sut.Hash("password123");

        result.Should().NotBe("password123");
    }

    [Fact]
    public void Hash_ProducesValidBcryptFormattedHash()
    {
        var result = _sut.Hash("password123");

        // All BCrypt hashes begin with $2a$, $2b$, or $2y$ followed by the cost factor
        result.Should().MatchRegex(@"^\$2[aby]\$\d{2}\$.{53}$");
    }

    [Fact]
    public void Hash_ProducesDifferentHashEachTime_DueToRandomSalt()
    {
        var hash1 = _sut.Hash("password123");
        var hash2 = _sut.Hash("password123");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Hash_DifferentPasswords_ProduceDifferentHashes()
    {
        var hash1 = _sut.Hash("password123");
        var hash2 = _sut.Hash("different456");

        hash1.Should().NotBe(hash2);
    }

    // -------------------------------------------------------------------------
    // Verify
    // -------------------------------------------------------------------------

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = _sut.Hash("password123");

        _sut.Verify("password123", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("password123");

        _sut.Verify("wrongpassword", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_WithEmptyPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("password123");

        _sut.Verify("", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrueAcrossDifferentSalts()
    {
        // Same password hashed twice produces different hashes due to random salt
        // Verify must work correctly against both
        var hash1 = _sut.Hash("password123");
        var hash2 = _sut.Hash("password123");

        hash1.Should().NotBe(hash2);
        _sut.Verify("password123", hash1).Should().BeTrue();
        _sut.Verify("password123", hash2).Should().BeTrue();
    }

    [Fact]
    public void Verify_PasswordDoesNotVerifyAgainstDifferentPasswordsHash()
    {
        var hash = _sut.Hash("password123");

        _sut.Verify("different456", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_IsCaseSensitive()
    {
        var hash = _sut.Hash("password123");

        _sut.Verify("Password123", hash).Should().BeFalse();
        _sut.Verify("PASSWORD123", hash).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Hash / Verify round-trip
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("password123")]
    [InlineData("a very long passphrase with spaces")]
    [InlineData("P@$$w0rd!#%")]
    [InlineData("12345678")]
    public void HashAndVerify_RoundTrip_AlwaysSucceeds(string password)
    {
        var hash = _sut.Hash(password);

        _sut.Verify(password, hash).Should().BeTrue();
    }
}
