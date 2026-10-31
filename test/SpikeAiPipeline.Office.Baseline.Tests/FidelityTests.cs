using System.Text.Json.Nodes;

namespace SpikeAiPipeline.Office.Baseline.Tests;

// The chart-value and table-cell rules of Fidelity, each on a truth and a Markdown rendering
// written by hand, the expected count worked out from the rule as the findings page states it.
public sealed class FidelityTests
{
    [Fact]
    public void AChartIsPlacedByItsTitleAndValuesBeforeItDontCount()
    {
        var score = Score(
            "chartValue",
            [Para("Intro"), Chart("Revenue", "Sales", ["North", "South"], [10, 20]), Para("Closing")],
            """
            Intro 10 20

            ### Revenue

            | Category | Sales |
            |---|---|
            | North | 10 |
            | South | |

            Closing
            """);

        Assert.Equal((2, 1), (score.Expected, score.Found));
    }

    [Fact]
    public void AnUntitledChartIsPlacedByTheTextBeforeItAndEndsAtTheTextAfterIt()
    {
        var score = Score(
            "chartValue",
            [Para("Before"), Para("Anchor text"), Chart("", "Series", ["a", "b", "c"], [3, 4, 5]), Para("After")],
            """
            Before 5

            Anchor text

            3 and 4

            After

            5
            """);

        Assert.Equal((3, 2), (score.Expected, score.Found));
    }

    [Fact]
    public void AChartWhoseTitleAndTextBeforeTheRenderingLacksIsUnplacedAndScoresNothing()
    {
        var score = Score(
            "chartValue",
            [Para("Lead-in"), Chart("Missing title", "Series", ["a", "b"], [1, 2])],
            """
            Something else 1 2
            """);

        Assert.Equal((2, 0), (score.Expected, score.Found));
        Assert.Equal(["chart 1: unplaced, 2 values"], score.Missed);
    }

    [Fact]
    public void AnUntitledChartWithNothingBeforeItIsPlacedAtTheBeginning()
    {
        var score = Score(
            "chartValue",
            [Chart("", "Series", ["a", "b"], [1, 2]), Para("After")],
            """
            1 and 2

            After
            """);

        Assert.Equal((2, 2), (score.Expected, score.Found));
    }

    [Fact]
    public void SeriesNamesAndCategoriesAreBlankedSoTheirNumbersAreNoValues()
    {
        var score = Score(
            "chartValue",
            [Chart("Sizes", "Size 9", ["Region 5", "Region 7"], [5, 7]), Chart("Other", "Size 9", ["Region 5", "Region 7"], [9, 1])],
            """
            Sizes

            | Category | Size 9 |
            |---|---|
            | Region 5 | |
            | Region 7 | |

            Other

            | Category | Size 9 |
            |---|---|
            | Region 5 | 9 |
            | Region 7 | 1 |
            """);

        Assert.Equal((4, 2), (score.Expected, score.Found));
    }

    [Fact]
    public void ANumberInTheTextCountsOnceAcrossValuesAndCharts()
    {
        // Both untitled charts are placed after "Start", so their regions are the same text: its
        // one 10 is the first chart's, so the second's 10 finds none, and its one 7 stands for
        // one of the second chart's two.
        var score = Score(
            "chartValue",
            [Para("Start"), Chart("", "A", ["x"], [10]), Chart("", "B", ["x", "y", "z"], [10, 7, 7]), Para("End")],
            """
            Start

            10 and 7

            End
            """);

        Assert.Equal((4, 2), (score.Expected, score.Found));
        Assert.Equal(["chart 2: 2 of 3 values"], score.Missed);
    }

    [Fact]
    public void TableRowsAndColumnsAreAlignedPastAnAddedHeaderRowAndRowNumbers()
    {
        var score = Score(
            "tableCell",
            [Table(["Name", "Qty"], ["Apple", "3"])],
            """
            |   | A | B |
            |---|---|---|
            | 1 | Name | Qty |
            | 2 | Apple | 3.0 |
            """);

        Assert.Equal((4, 4), (score.Expected, score.Found));
    }

    [Fact]
    public void TableCellsAreComparedAtTheirAlignedPositionNotAnywhereInTheRow()
    {
        var score = Score(
            "tableCell",
            [Table(["Name", "Qty"], ["Apple", "3"])],
            """
            | Name | Qty |
            |---|---|
            | 3 | Apple |
            """);

        Assert.Equal((4, 2), (score.Expected, score.Found));
        Assert.Equal(["r1c0 Apple", "r1c1 3"], score.Missed);
    }

    [Fact]
    public void AnEmptyColumnAToolAddsShiftsNothing()
    {
        var score = Score(
            "tableCell",
            [Table(["Name", "", "Qty"], ["Apple", "", "3"])],
            """
            | Name | | | Qty |
            |---|---|---|---|
            | Apple | | | 3 |
            """);

        Assert.Equal((4, 4), (score.Expected, score.Found));
    }

    private static KindScore Score(string kind, JsonNode[] elements, string markdown)
    {
        var truth = new JsonObject { ["elements"] = new JsonArray(elements) };
        return Fidelity.Score(Expected.Of(truth), Rendered.Of(markdown), "docx")[kind];
    }

    private static JsonObject Para(string text) => new() { ["kind"] = "paragraph", ["text"] = text };

    private static JsonObject Chart(string title, string series, string[] categories, double[] values) => new()
    {
        ["kind"] = "chart",
        ["title"] = title,
        ["series"] = new JsonArray(new JsonObject
        {
            ["name"] = series,
            ["categories"] = new JsonArray([.. categories.Select(c => (JsonNode)c)]),
            ["values"] = new JsonArray([.. values.Select(v => (JsonNode)v)]),
        }),
    };

    // A docx table: its rows of cells, each cell one paragraph, an empty text an empty cell.
    private static JsonObject Table(params string[][] rows) => new()
    {
        ["kind"] = "table",
        ["rows"] = new JsonArray([.. rows.Select(row => (JsonNode)new JsonArray([.. row.Select(text => (JsonNode)new JsonObject
        {
            ["elements"] = new JsonArray(Para(text)),
        })]))]),
    };
}
