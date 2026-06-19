using Xunit;
using FluentAssertions;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.UnitTests.ValueObjects;

public class NationalIdTests
{
    private const string ValidId = "10000000146";

    [Fact] public void Create_ValidId_ShouldSucceed() { NationalId.Create(ValidId).Value.Should().Be(ValidId); }
    [Fact] public void Create_InvalidChecksum_ShouldThrow() { var act = () => NationalId.Create("10000000147"); act.Should().Throw<ArgumentException>(); }
    [Fact] public void Create_StartsWithZero_ShouldThrow() { var act = () => NationalId.Create("01234567890"); act.Should().Throw<ArgumentException>().WithMessage("*start with 0*"); }
    [Fact] public void Masked_ShouldHideFirst7() { NationalId.Create(ValidId).Masked.Should().StartWith("*******"); }
    [Fact] public void ToString_ShouldReturnMasked() { NationalId.Create(ValidId).ToString().Should().Contain("*"); }
}
