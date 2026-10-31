namespace SpikeAiPipeline.Office.Baseline.Tests;

// The scorer's text rules, Text.NumberTokens and Text.Key, on texts written by hand from their
// documented rules, so a change to either shows here before it moves a findings number.
public sealed class TextTests
{
    [Theory]
    [InlineData("Q1")]
    [InlineData("x2")]
    [InlineData("1.2.3")]
    [InlineData("2021-10-31")]
    [InlineData("10/31")]
    [InlineData("16:51")]
    [InlineData("1-5")]
    public void PartsOfWordsAndOtherNumbersAreNoTokens(string text)
    {
        Assert.Empty(Text.NumberTokens(text));
    }

    [Theory]
    [InlineData("1,234.50", "#1234.5")]
    [InlineData("1234.5", "#1234.5")]
    [InlineData("2.0", "#2")]
    [InlineData("2", "#2")]
    [InlineData("-3", "#-3")]
    [InlineData("4.4000000000000004", "#4.4")]
    public void AWholeNumberIsOneTokenOfItsValue(string text, string key)
    {
        var token = Assert.Single(Text.NumberTokens(text));

        Assert.Equal((key, 0), token);
    }

    [Fact]
    public void TokensCarryTheirOffsetsAndASignOnlyWhereNothingPrecedesIt()
    {
        // Q1's 1 follows a letter, so it is no token. In "x-2" the hyphen follows a letter, so it
        // is no sign, but the 2 follows only the hyphen, so it is a token, unsigned.
        var tokens = Text.NumberTokens("Q1 rose 1,234.50 then -3 and x-2").ToList();

        Assert.Equal([("#1234.5", 8), ("#-3", 22), ("#2", 31)], tokens);
    }

    [Theory]
    [InlineData("14", "#14")]
    [InlineData("14.0", "#14")]
    [InlineData("1.4E1", "#14")]
    [InlineData("-0.5", "#-0.5")]
    [InlineData("TRUE", "?true")]
    [InlineData("True", "?true")]
    [InlineData("false", "?false")]
    [InlineData("2021-10-31", "@2021-10-31")]
    [InlineData("2021-10-31T00:00:00", "@2021-10-31")]
    [InlineData("2021-10-31 00:00", "@2021-10-31")]
    [InlineData("2021-10-31T16:51", "@2021-10-31T16:51:00")]
    [InlineData("2021-10-31 16:51:00", "@2021-10-31T16:51:00")]
    [InlineData("2021-10-31T16:51:00.500", "@2021-10-31T16:51:00.5")]
    [InlineData("2021-10-31 16:51:00.000", "@2021-10-31T16:51:00")]
    [InlineData("  Some  text\n", "Some text")]
    [InlineData("1,234", "1,234")]
    public void AKeyIsANumbersBooleansOrDatesValueAndOtherwiseItsText(string text, string key)
    {
        Assert.Equal(key, Text.Key(text));
    }
}
