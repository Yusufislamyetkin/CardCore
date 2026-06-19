using Xunit;
using FluentAssertions;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.Shared.Domain.UnitTests.Primitives;

public class StronglyTypedIdTests
{
    [Fact] public void New_ShouldCreateUniqueIds() { AccountId.New().Should().NotBe(AccountId.New()); }
    [Fact] public void From_ShouldWrapExistingGuid() { var g = Guid.NewGuid(); AccountId.From(g).Value.Should().Be(g); }
    [Fact] public void SameGuid_DifferentTypes_DifferentTypes() { var g = Guid.NewGuid(); AccountId.From(g).GetType().Should().NotBe(CardId.From(g).GetType()); }
    [Fact] public void SameGuid_SameType_ShouldBeEqual() { var g = Guid.NewGuid(); CardId.From(g).Should().Be(CardId.From(g)); }
    [Fact] public void ToString_ShouldReturnGuidString() { var g = Guid.NewGuid(); TransactionId.From(g).ToString().Should().Be(g.ToString()); }
}
