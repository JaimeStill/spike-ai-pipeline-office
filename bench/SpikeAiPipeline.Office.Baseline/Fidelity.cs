namespace SpikeAiPipeline.Office.Baseline;

/// <summary>One element kind's score in one file: how many elements the truth holds, how many the
/// rendering has, and the first of those it misses, for the per-file detail.</summary>
internal sealed record KindScore(int Expected, int Found, List<string> Missed);

/// <summary>The matching rules, one per element kind, the same for every tool. Each is documented
/// on the findings page (<c>docs/findings/office-cli.md</c>, "How the fidelity is scored"); every
/// text is compared normalized (<see cref="Text.Norm"/>), and a rendered element matches one
/// truth element at most.</summary>
internal static class Fidelity
{
    /// <summary>The element kinds, in the findings table's order.</summary>
    public static readonly string[] Kinds =
    [
        "heading", "paragraph", "listItem", "tableCell", "sheetName", "slideOrder", "slideNotes",
        "chartValue", "media", "headerFooter", "note", "comment",
    ];

    private const int MissedShown = 10;

    public static Dictionary<string, KindScore> Score(Expected expected, Rendered rendered, string format)
    {
        var scores = new Dictionary<string, KindScore>
        {
            // A heading matches a heading of the same text and level, a level past Markdown's six
            // as a level 6. In a pptx file the level is left out: each tool sets a slide's title
            // at a level of its own, under its slide's marker or not.
            ["heading"] = Consume(expected.Headings, rendered.Headings, (e, r) => e.Text == r.Text && (format == "pptx" || Math.Min(e.Level, 6) == r.Level), e => $"h{e.Level} {e.Text}"),
            // A paragraph matches a paragraph of the same text, anywhere outside a table.
            ["paragraph"] = Consume(expected.Paragraphs, rendered.Paragraphs, (e, r) => e == r, e => e),
            // A list item matches an item of the same text, nesting level, and kind (ordered or
            // not) that starts its list when the truth's starts one and continues it when the
            // truth's continues; an ordered list's first item also needs its list's first number.
            ["listItem"] = Consume(expected.ListItems, rendered.ListItems, ListItemMatches, e => $"{(e.Ordered ? "ol" : "ul")}{e.Level} {e.Text}"),
            ["tableCell"] = TableCells(expected.Tables, rendered.Tables),
            // A sheet's name matches a heading of that text, or of that text and a parenthesized
            // note after it, such as "(hidden)".
            ["sheetName"] = Consume(expected.SheetNames, rendered.Headings, (e, r) => r.Text == e || r.Text.StartsWith(e + " (", StringComparison.Ordinal), e => e),
            ["slideOrder"] = SlideOrder(expected.SlideAnchors, rendered.Leaves),
            ["slideNotes"] = Contained(expected.SlideNotes, rendered.Leaves),
            ["chartValue"] = ChartValues(expected.Charts, rendered.Tables),
            // A picture counts when the rendering has an image, one for each: an image link or an
            // HTML img tag. Its target isn't compared, as each tool names a picture its own way.
            ["media"] = new KindScore(expected.Media, Math.Min(expected.Media, rendered.Images), []),
            ["headerFooter"] = Contained(expected.HeaderFooter, rendered.Leaves),
            ["note"] = Contained(expected.Notes, rendered.Leaves),
            ["comment"] = Contained(expected.Comments, rendered.Leaves),
        };
        return scores;
    }

    private static bool ListItemMatches(ExpectedListItem e, RenderedListItem r) =>
        e.Text == r.Text && e.Level == r.Level && e.Ordered == r.Ordered && e.Start == r.First
        && (!e.Ordered || !e.Start || r.Start == (e.StartAt ?? 1));

    // Each truth element, in order, takes the first rendered element it matches that no earlier
    // one took.
    private static KindScore Consume<TE, TR>(List<TE> expected, List<TR> rendered, Func<TE, TR, bool> matches, Func<TE, string> describe)
    {
        var taken = new bool[rendered.Count];
        var found = 0;
        var missed = new List<string>();
        foreach (var e in expected)
        {
            var i = 0;
            while (i < rendered.Count && (taken[i] || !matches(e, rendered[i])))
            {
                i++;
            }
            if (i < rendered.Count)
            {
                taken[i] = true;
                found++;
            }
            else if (missed.Count < MissedShown)
            {
                missed.Add(describe(e));
            }
        }
        return new KindScore(expected.Count, found, missed);
    }

    // A story's text (a header's or footer's, a note's, a comment's or reply's, a slide's notes')
    // counts when it is within some text of the rendering, so a tool's own label before it
    // ("Comment by ...:") or its joining a note's paragraphs into one doesn't count against it.
    private static KindScore Contained(List<string> expected, List<string> leaves)
    {
        var found = 0;
        var missed = new List<string>();
        foreach (var e in expected)
        {
            if (leaves.Any(l => l.Contains(e, StringComparison.Ordinal)))
            {
                found++;
            }
            else if (missed.Count < MissedShown)
            {
                missed.Add(e);
            }
        }
        return new KindScore(expected.Count, found, missed);
    }

    // A slide counts when its first text is a text of the rendering after the text the last
    // counted slide's matched, so the slides that count are in presentation order.
    private static KindScore SlideOrder(List<string> anchors, List<string> leaves)
    {
        var found = 0;
        var position = -1;
        var missed = new List<string>();
        for (var s = 0; s < anchors.Count; s++)
        {
            var at = leaves.FindIndex(position + 1, l => l == anchors[s]);
            if (at >= 0)
            {
                found++;
                position = at;
            }
            else if (missed.Count < MissedShown)
            {
                missed.Add($"slide {s + 1}: {anchors[s]}");
            }
        }
        return new KindScore(anchors.Count, found, missed);
    }

    // Each truth table, in order, is compared with every rendered table no earlier truth table
    // took, and takes the one where most of its cells match; a cell matches when the rendered
    // table has its text (by its key, Text.Key) at its row and column. Rows and columns are
    // aligned first, so a header row a tool adds, a row-number column, or a column it keeps empty
    // shifts nothing: the truth's rows are paired in order with the rendered rows that share the
    // most cell texts with them, each pair after the last (a weighted longest common subsequence),
    // and the columns the same way.
    private static KindScore TableCells(List<ExpectedTable> expected, List<List<List<string>>> rendered)
    {
        var taken = new bool[rendered.Count];
        var total = 0;
        var found = 0;
        var missed = new List<string>();
        foreach (var table in expected)
        {
            total += table.Cells.Count;
            var best = -1;
            HashSet<(int, int)> bestMatched = [];
            for (var t = 0; t < rendered.Count; t++)
            {
                if (taken[t])
                {
                    continue;
                }
                var matched = Align(table, rendered[t]);
                if (matched.Count > bestMatched.Count)
                {
                    best = t;
                    bestMatched = matched;
                }
            }
            if (best >= 0)
            {
                taken[best] = true;
            }
            found += bestMatched.Count;
            foreach (var (position, cell) in table.Cells)
            {
                if (!bestMatched.Contains(position) && missed.Count < MissedShown)
                {
                    missed.Add($"r{position.Row}c{position.Column} {cell.Text}");
                }
            }
        }
        return new KindScore(total, found, missed);
    }

    // The truth cells a rendered table matches, under the alignment of rows and columns.
    private static HashSet<(int, int)> Align(ExpectedTable table, List<List<string>> grid)
    {
        var rows = table.Cells.Keys.Select(k => k.Row).Distinct().Order().ToList();
        var columns = table.Cells.Keys.Select(k => k.Column).Distinct().Order().ToList();
        var width = grid.Count == 0 ? 0 : grid.Max(r => r.Count);
        var rowKeys = rows.Select(row => table.Cells.Where(c => c.Key.Row == row).Select(c => c.Value.Key).ToList()).ToList();
        var columnKeys = columns.Select(column => table.Cells.Where(c => c.Key.Column == column).Select(c => c.Value.Key).ToList()).ToList();
        var gridColumns = Enumerable.Range(0, width).Select(j => grid.Select(r => j < r.Count ? r[j] : "").ToList()).ToList();
        var rowMap = Pair(rows.Count, grid.Count, (i, j) => Shared(rowKeys[i], grid[j]));
        var columnMap = Pair(columns.Count, width, (i, j) => Shared(columnKeys[i], gridColumns[j]));
        var matched = new HashSet<(int, int)>();
        foreach (var (position, cell) in table.Cells)
        {
            var r = rowMap[rows.IndexOf(position.Row)];
            var c = columnMap[columns.IndexOf(position.Column)];
            if (r >= 0 && c >= 0 && c < grid[r].Count && grid[r][c] == cell.Key)
            {
                matched.Add(position);
            }
        }
        return matched;
    }

    // How many of a truth row's or column's keys a rendered one has, each key counted once per
    // occurrence; empty cells share nothing.
    private static int Shared(IEnumerable<string> keys, IEnumerable<string> rendered)
    {
        var counts = new Dictionary<string, int>();
        foreach (var key in rendered.Where(k => k.Length > 0))
        {
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        var shared = 0;
        foreach (var key in keys)
        {
            if (counts.GetValueOrDefault(key) > 0)
            {
                counts[key]--;
                shared++;
            }
        }
        return shared;
    }

    // The monotone pairing of a's items with b's that maximizes the summed weight of the pairs,
    // each item paired once at most and only where the weight is positive: for each item of a,
    // the index of its pair in b, or -1.
    private static int[] Pair(int a, int b, Func<int, int, int> weight)
    {
        var w = new int[a, b];
        for (var i = 0; i < a; i++)
        {
            for (var j = 0; j < b; j++)
            {
                w[i, j] = weight(i, j);
            }
        }
        var best = new int[a + 1, b + 1];
        for (var i = 1; i <= a; i++)
        {
            for (var j = 1; j <= b; j++)
            {
                best[i, j] = Math.Max(Math.Max(best[i - 1, j], best[i, j - 1]), w[i - 1, j - 1] > 0 ? best[i - 1, j - 1] + w[i - 1, j - 1] : 0);
            }
        }
        var pairs = Enumerable.Repeat(-1, a).ToArray();
        for (int i = a, j = b; i > 0 && j > 0;)
        {
            if (w[i - 1, j - 1] > 0 && best[i, j] == best[i - 1, j - 1] + w[i - 1, j - 1])
            {
                pairs[i - 1] = j - 1;
                i--;
                j--;
            }
            else if (best[i, j] == best[i - 1, j])
            {
                i--;
            }
            else
            {
                j--;
            }
        }
        return pairs;
    }

    // Each chart, in order, takes the rendered table no earlier chart took that holds the most of
    // its values, compared as numbers; a value counts when that table has it, each table cell
    // standing for one value at most.
    private static KindScore ChartValues(List<List<double>> charts, List<List<List<string>>> rendered)
    {
        var taken = new bool[rendered.Count];
        var total = 0;
        var found = 0;
        var missed = new List<string>();
        foreach (var values in charts)
        {
            total += values.Count;
            var keys = values.Select(Text.NumberKey).ToList();
            var best = -1;
            var bestShared = 0;
            for (var t = 0; t < rendered.Count; t++)
            {
                var shared = taken[t] ? 0 : Shared(keys, rendered[t].SelectMany(r => r));
                if (shared > bestShared)
                {
                    best = t;
                    bestShared = shared;
                }
            }
            if (best >= 0)
            {
                taken[best] = true;
            }
            found += bestShared;
            if (bestShared < values.Count && missed.Count < MissedShown)
            {
                missed.Add($"chart {charts.IndexOf(values) + 1}: {values.Count - bestShared} of {values.Count} values");
            }
        }
        return new KindScore(total, found, missed);
    }
}
