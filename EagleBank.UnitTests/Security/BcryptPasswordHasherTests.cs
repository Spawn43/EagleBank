using EagleBank.Data.Security;
using FluentAssertions;

namespace EagleBank.UnitTests.Security;

[TestFixture]
public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _sut = new();

    // Hash

    [Test]
    public void Hash_ReturnsNonEmptyString()
    {
        // Act
        var result = _sut.Hash("password123");

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Hash_DoesNotReturnPlainTextPassword()
    {
        // Act
        var result = _sut.Hash("password123");

        // Assert
        result.Should().NotBe("password123");
    }

    [Test]
    public void Hash_ProducesValidBcryptFormattedHash()
    {
        // Act
        var result = _sut.Hash("password123");

        // Assert
        result.Should().MatchRegex(@"^\$2[aby]\$\d{2}\$.{53}$");
    }

    [Test]
    public void Hash_ProducesDifferentHashEachTime_DueToRandomSalt()
    {
        // Act
        var hash1 = _sut.Hash("password123");
        var hash2 = _sut.Hash("password123");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Test]
    public void Hash_DifferentPasswords_ProduceDifferentHashes()
    {
        // Act
        var hash1 = _sut.Hash("password123");
        var hash2 = _sut.Hash("different456");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    // Verify

    [Test]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var hash = _sut.Hash("password123");

        // Act
        var result = _sut.Verify("password123", hash);

        // Assert
        result.Should().BeTrue();
    }

    [TestCase("wrongpassword")]
    [TestCase("")]
    [TestCase("different456")]
    public void Verify_WithIncorrectPassword_ReturnsFalse(string wrongPassword)
    {
        // Arrange
        var hash = _sut.Hash("password123");

        // Act
        var result = _sut.Verify(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Verify_WithCorrectPassword_ReturnsTrueAcrossDifferentSalts()
    {
        // Arrange
        var hash1 = _sut.Hash("password123");
        var hash2 = _sut.Hash("password123");

        // Act
        var result1 = _sut.Verify("password123", hash1);
        var result2 = _sut.Verify("password123", hash2);

        // Assert
        hash1.Should().NotBe(hash2);
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [TestCase("Password123")]
    [TestCase("PASSWORD123")]
    public void Verify_IsCaseSensitive(string wrongCasePassword)
    {
        // Arrange
        var hash = _sut.Hash("password123");

        // Act
        var result = _sut.Verify(wrongCasePassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    // Hash / Verify round-trip

    [TestCase("password123")]
    [TestCase("a very long passphrase with spaces")]
    [TestCase("P@$$w0rd!#%")]
    [TestCase("12345678")]
    public void HashAndVerify_RoundTrip_AlwaysSucceeds(string password)
    {
        // Act
        var hash = _sut.Hash(password);
        var result = _sut.Verify(password, hash);

        // Assert
        result.Should().BeTrue();
    }
}
