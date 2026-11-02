using ShortLink.Api.Infrastructure;

namespace ShortLink.Api.Tests;

public sealed class CodeGeneratorTests
{
    private readonly CodeGenerator _sut = new();

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(16)]
    public void Generates_requested_length(int length)
    {
        var code = _sut.Generate(length);

        Assert.Equal(length, code.Length);
    }

    [Fact]
    public void Only_uses_unambiguous_alphabet()
    {
        for (var i = 0; i < 500; i++)
        {
            var code = _sut.Generate(8);
            Assert.All(code, ch => Assert.Contains(ch, CodeGenerator.Alphabet));
            Assert.DoesNotContain('0', code);
            Assert.DoesNotContain('O', code);
            Assert.DoesNotContain('l', code);
            Assert.DoesNotContain('I', code);
        }
    }

    [Fact]
    public void Produces_distinct_codes()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => _sut.Generate(7)).ToHashSet();

        Assert.Equal(1000, codes.Count);
    }

    [Fact]
    public void Rejects_non_positive_length()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.Generate(0));
    }
}
