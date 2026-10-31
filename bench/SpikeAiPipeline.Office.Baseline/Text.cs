using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpikeAiPipeline.Office.Baseline;

/// <summary>The one normalization every text goes through, the truth's and every tool's alike, so
/// no tool is scored on how it spaces or escapes text: Markdig has already undone the Markdown's
/// escaping, and this composes the text to Unicode NFC, turns every run of whitespace (a line
/// break, a tab, a non-breaking space) into one space, and trims it.</summary>
internal static partial class Text
{
    public static string Norm(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        var result = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            if (char.IsWhiteSpace(c))
            {
                space = result.Length > 0;
                continue;
            }
            if (space)
            {
                result.Append(' ');
                space = false;
            }
            result.Append(c);
        }
        return result.ToString();
    }

    /// <summary>A cell's or chart point's comparison key: its normalized text, except that a number
    /// is its value (so 14, 14.0, and 1.4E1 are one key), a boolean its value whatever its case
    /// (TRUE, True, true), and an ISO 8601 date or date and time its value whether the time is
    /// joined by T or a space and whatever zeros its seconds' fraction has, a midnight time left
    /// out.</summary>
    public static string Key(string? text)
    {
        var norm = Norm(text);
        if (NumberPattern().IsMatch(norm) && double.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return NumberKey(number);
        }
        if (norm.Equals("true", StringComparison.OrdinalIgnoreCase) || norm.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return "?" + norm.ToLowerInvariant();
        }
        if (DatePattern().Match(norm) is { Success: true } date)
        {
            var time = date.Groups[2].Success ? date.Groups[2].Value : "";
            if (time.Length == 5)
            {
                time += ":00";
            }
            if (time.Contains('.', StringComparison.Ordinal))
            {
                time = time.TrimEnd('0').TrimEnd('.');
            }
            return "@" + date.Groups[1].Value + (time is "" or "00:00:00" ? "" : "T" + time);
        }
        return norm;
    }

    /// <summary>A number's key, as <see cref="Key"/> gives it for the number's text.</summary>
    public static string NumberKey(double number) => "#" + number.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>The numbers written in a text, each as its key (<see cref="NumberKey"/>) and its
    /// offset: a token is a whole number as written, never a part of a word or of another number.
    /// It is an optional sign, digits (grouped in threes by commas, or not), an optional fraction,
    /// and an optional exponent; it is not preceded by a letter, digit, underscore, or point, nor
    /// by a digit and a comma, hyphen, slash, or colon, and not followed by a letter, digit, or
    /// underscore, nor by a point, comma, hyphen, slash, or colon and a digit. So "Q1", "x2",
    /// "1.2.3", "2021-10-31", "10/31", "16:51", and "1-5" give no token at all; 1,234.50 and
    /// 1234.5 are one value, as are 2.0 and 2, every tool's text read the same way. A sign is the
    /// token's when no letter, digit, point, or other sign is before it.</summary>
    public static IEnumerable<(string Key, int Offset)> NumberTokens(string text)
    {
        foreach (Match match in NumberTokenPattern().Matches(text))
        {
            var digits = match.Value.Replace(",", "", StringComparison.Ordinal);
            if (double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            {
                yield return (NumberKey(number), match.Index);
            }
        }
    }

    /// <summary>A text with each whole occurrence of the given labels, one not inside a longer word
    /// or number, replaced by spaces of its length, so the offsets of what is left stay the
    /// same.</summary>
    public static string Blank(string text, IEnumerable<string> labels)
    {
        var result = text;
        foreach (var label in labels)
        {
            result = Regex.Replace(result, @"(?<![\p{L}\p{N}_])" + Regex.Escape(label) + @"(?![\p{L}\p{N}_])", m => new string(' ', m.Length));
        }
        return result;
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}_.]|\d[,\-/:])(?:(?<![\p{L}\p{N}_.+\-])[+-])?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?(?:[eE][+-]?\d+)?(?![\p{L}\p{N}_]|[.,\-/:]\d)")]
    private static partial Regex NumberTokenPattern();

    [GeneratedRegex(@"^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})(?:[T ](\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?))?$")]
    private static partial Regex DatePattern();
}
