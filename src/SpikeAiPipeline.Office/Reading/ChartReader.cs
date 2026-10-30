using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using SpikeAiPipeline.Office.Model;

namespace SpikeAiPipeline.Office.Reading;

/// <summary>Reads a DrawingML chart part's data: the values its series cache, never its
/// rendering, and never an embedded workbook.</summary>
internal static class ChartReader
{
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const int MaxPoints = 1 << 20;

    public static Chart Read(ChartPart part)
    {
        XDocument document;
        using (var stream = part.GetStream(FileMode.Open, FileAccess.Read))
        {
            document = XDocument.Load(stream);
        }
        var chart = document.Root?.Element(C + "chart");
        // A plot is a plot area's child named for its kind: barChart, lineChart, pieChart, ...
        var plots = chart?.Element(C + "plotArea")?.Elements()
            .Where(e => e.Name.Namespace == C && e.Name.LocalName.EndsWith("Chart", StringComparison.Ordinal))
            .ToList() ?? [];
        var type = plots.Count > 0 ? plots[0].Name.LocalName[..^"Chart".Length] : "unknown";
        var series = plots.SelectMany(plot => plot.Elements(C + "ser")).Select(ReadSeries).ToList();
        return new Chart(type, Title(chart?.Element(C + "title")), series);
    }

    // A title's own text, rich or from a cell's cache. A title with neither is the application's
    // to make up, so it has none here.
    private static string? Title(XElement? title)
    {
        var tx = title?.Element(C + "tx");
        if (tx?.Element(C + "rich") is { } rich)
        {
            var text = string.Join("\n", rich.Elements(A + "p").Select(p => string.Concat(p.Descendants(A + "t").Select(t => t.Value))));
            return text.Length > 0 ? text : null;
        }
        return tx is null ? null : Text(tx);
    }

    private static ChartSeries ReadSeries(XElement series)
    {
        var name = series.Element(C + "tx") is { } tx ? Text(tx) : null;
        // A scatter or bubble series has x and y values in place of categories and values.
        var categories = series.Element(C + "cat") ?? series.Element(C + "xVal");
        var values = series.Element(C + "val") ?? series.Element(C + "yVal");
        return new ChartSeries(
            name,
            categories is null ? [] : Points(categories),
            values is null ? [] : [.. Points(values).Select(Number)]);
    }

    // A series name's text: a cell reference's cache or a literal value.
    private static string? Text(XElement tx)
    {
        if (tx.Element(C + "v") is { } literal)
        {
            return literal.Value;
        }
        var points = tx.Descendants(C + "strCache").FirstOrDefault() is { } cache ? Cache(cache) : [];
        var text = string.Join(" ", points.OfType<string>());
        return text.Length > 0 ? text : null;
    }

    // A data source's points: a reference's cache or a literal, of strings or numbers. A
    // multi-level category cache gives its innermost level, the one each point is labeled with.
    private static List<string?> Points(XElement source)
    {
        foreach (var data in source.Elements())
        {
            var cache = data.Name.LocalName switch
            {
                "strRef" => data.Element(C + "strCache"),
                "numRef" => data.Element(C + "numCache"),
                "multiLvlStrRef" => data.Element(C + "multiLvlStrCache")?.Element(C + "lvl"),
                "strLit" or "numLit" => data,
                _ => null,
            };
            if (cache is not null)
            {
                return Cache(cache);
            }
        }
        return [];
    }

    // A cache's points by index, as long as its point count, a point it lacks null.
    private static List<string?> Cache(XElement cache)
    {
        var points = cache.Elements(C + "pt")
            .Select(pt => (Index: int.TryParse((string?)pt.Attribute("idx"), CultureInfo.InvariantCulture, out var i) ? i : -1, Value: pt.Element(C + "v")?.Value))
            .Where(pt => pt.Index >= 0)
            .ToList();
        // A count beyond any point is capped, so a malformed count can't exhaust memory.
        var declared = int.TryParse((string?)cache.Element(C + "ptCount")?.Attribute("val"), CultureInfo.InvariantCulture, out var n) ? n : 0;
        var count = Math.Clamp(declared, points.Count > 0 ? points.Max(pt => pt.Index) + 1 : 0, MaxPoints);
        var result = new List<string?>(new string?[count]);
        foreach (var (index, value) in points.Where(pt => pt.Index < count))
        {
            result[index] = value;
        }
        return result;
    }

    private static double? Number(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
