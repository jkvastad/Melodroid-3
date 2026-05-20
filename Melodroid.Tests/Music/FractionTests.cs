using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class FractionTests
{
    [Fact]
    public void Value_returns_numerator_divided_by_denominator()
    {
        new Fraction(5, 4).Value.Should().Be(1.25);
    }

    [Fact]
    public void ToString_uses_slash_notation()
    {
        new Fraction(5, 4).ToString().Should().Be("5/4");
    }
}
