using Xunit;
using FluentAssertions;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.UnitTests.ValueObjects;

public class PanTests
{
    private const string ValidPan = "4532015112830366";

    [Fact] public void Create_ValidPan_ShouldSucceed() { Pan.Create(ValidPan).Should().NotBeNull(); }
    [Fact] public void Create_InvalidLuhn_ShouldThrow() { var act = () => Pan.Create("4532015112830367"); act.Should().Throw<ArgumentException>().WithMessage("*Luhn*"); }
    [Fact] public void Create_WithSpaces_ShouldAccept() { Pan.Create("4532 0151 1283 0366").Last4.Should().Be("0366"); }
    [Fact] public void Last4_ShouldReturnLastFour() { Pan.Create(ValidPan).Last4.Should().Be("0366"); }
    [Fact] public void Bin_ShouldReturnFirstSix() { Pan.Create(ValidPan).Bin.Should().Be("453201"); }
    [Fact] public void Masked_ShouldHideMiddle() { var m = Pan.Create(ValidPan).Masked; m.Should().Contain("*"); m.Should().StartWith("453201"); m.Should().EndWith("0366"); }
    [Fact] public void ToString_ShouldNeverReturnClear() { Pan.Create(ValidPan).ToString().Should().NotBe(ValidPan); }
    [Fact] public void Hashed_ShouldBeConsistent() { Pan.Create(ValidPan).Hashed.Should().Be(Pan.Create(ValidPan).Hashed); }
    [Fact] public void Equality_SamePan_ShouldBeEqual() { Pan.Create(ValidPan).Should().Be(Pan.Create(ValidPan)); }
}
