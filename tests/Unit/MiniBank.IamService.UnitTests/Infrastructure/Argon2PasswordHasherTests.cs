using FluentAssertions;
using MiniBank.IamService.Infrastructure;
using Xunit;

namespace MiniBank.IamService.UnitTests.Infrastructure;

public sealed class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _sut = new();

    [Fact]
    public void Hash_ShouldReturnNonEmptyString()
    {
        var hash = _sut.Hash("SecureP@ss1");

        hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Hash_ShouldReturnDifferentHashesForSamePassword()
    {
        var hash1 = _sut.Hash("SecureP@ss1");
        var hash2 = _sut.Hash("SecureP@ss1");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenPasswordMatchesHash()
    {
        const string password = "SecureP@ss1";
        var hash = _sut.Hash(password);

        _sut.Verify(password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenPasswordDoesNotMatch()
    {
        var hash = _sut.Hash("CorrectPassword");

        _sut.Verify("WrongPassword", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForInvalidHashFormat()
    {
        _sut.Verify("password", "not-a-valid-hash").Should().BeFalse();
    }
}
