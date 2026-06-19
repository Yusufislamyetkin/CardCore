using MiniBank.IamService.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.IamService.Domain.Entities;

public sealed class User : AggregateRoot<UserId>
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public Role Role { get; private set; }
    public bool IsActive { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash, Role role = Role.Customer)
    {
        var user = new User
        {
            Id = UserId.New(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            FailedLoginCount = 0,
            LockedUntil = null
        };
        return user;
    }

    public bool IsLockedOut(DateTime utcNow) =>
        LockedUntil.HasValue && LockedUntil.Value > utcNow;

    public void RecordFailedLogin(DateTime utcNow)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLoginAttempts)
            LockedUntil = utcNow.Add(LockoutDuration);

        MarkModified();
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        MarkModified();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        MarkModified();
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidStateTransitionException("User", "Deactivate", "already inactive");

        IsActive = false;
        MarkModified();
    }
}
