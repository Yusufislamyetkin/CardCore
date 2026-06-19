using FluentAssertions;
using MiniBank.IamService.Domain.Entities;
using MiniBank.IamService.Domain.Enums;
using Xunit;

namespace MiniBank.IamService.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldNormalizeEmail()
    {
        var user = User.Create("  Test@EXAMPLE.COM  ", "hash", Role.Customer);

        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Create_ShouldSetDefaultsCorrectly()
    {
        var user = User.Create("user@test.com", "hash");

        user.IsActive.Should().BeTrue();
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        user.Role.Should().Be(Role.Customer);
    }

    [Fact]
    public void RecordFailedLogin_ShouldIncrementFailedCount()
    {
        var user = User.Create("user@test.com", "hash");

        user.RecordFailedLogin(UtcNow);

        user.FailedLoginCount.Should().Be(1);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void RecordFailedLogin_ShouldLockAfterFiveFailures()
    {
        var user = User.Create("user@test.com", "hash");

        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(UtcNow);

        user.FailedLoginCount.Should().Be(5);
        user.LockedUntil.Should().NotBeNull();
        user.LockedUntil!.Value.Should().BeAfter(UtcNow);
    }

    [Fact]
    public void IsLockedOut_ShouldReturnTrue_WhenLockoutNotExpired()
    {
        var user = User.Create("user@test.com", "hash");
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(UtcNow);

        user.IsLockedOut(UtcNow.AddMinutes(5)).Should().BeTrue();
    }

    [Fact]
    public void IsLockedOut_ShouldReturnFalse_WhenLockoutExpired()
    {
        var user = User.Create("user@test.com", "hash");
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(UtcNow);

        user.IsLockedOut(UtcNow.AddMinutes(20)).Should().BeFalse();
    }

    [Fact]
    public void RecordSuccessfulLogin_ShouldResetFailedCount()
    {
        var user = User.Create("user@test.com", "hash");
        user.RecordFailedLogin(UtcNow);
        user.RecordFailedLogin(UtcNow);

        user.RecordSuccessfulLogin();

        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void ChangePassword_ShouldUpdatePasswordHash()
    {
        var user = User.Create("user@test.com", "old-hash");

        user.ChangePassword("new-hash");

        user.PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        var user = User.Create("user@test.com", "hash");

        user.Deactivate();

        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_ShouldThrow_WhenAlreadyInactive()
    {
        var user = User.Create("user@test.com", "hash");
        user.Deactivate();

        var act = () => user.Deactivate();

        act.Should().Throw<MiniBank.Shared.Domain.Exceptions.InvalidStateTransitionException>();
    }
}
