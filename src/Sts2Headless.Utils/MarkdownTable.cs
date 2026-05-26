using System.Globalization;
using System.Text;

namespace Sts2Headless.Utils;

// Builds a GitHub-Flavored-Markdown table whose *source* lines up
// nicely: column widths are computed from the max(header, cells) length
// and every cell is padded to that width with the right side for the
// declared alignment. The rendered HTML is identical to a non-padded
// table (GFM doesn't care about source whitespace inside cells), but
// the raw .md file is grep-friendly, diff-friendly, and pleasant to
// read in any monospace context.
//
// Why this exists: every time we hand-rolled a table inside a
// StringBuilder (sweep reports, eval summary.md, future leaderboards)
// the cell padding drifted as columns grew. Centralising the rule
// means a `summary.md` diff stays scoped to the actual data changing.
//
// Width counting uses System.Text.Rune so a BMP-class symbol
// (`⚠`, en-dash, …) counts as one column. Wide CJK or supplementary-
// plane emoji that render as two terminal columns will be slightly
// misaligned; nothing in the repo's table set hits that case today and
// fixing it requires the East Asian Width tables, which would be the
// only Unicode dep in Utils. Revisit if we ship a table with wide
// glyphs in it.
public enum MarkdownAlign
{
    Left,
    Right,
    Center,
}

public sealed class MarkdownTable
{
    private readonly List<(string Header, MarkdownAlign Align)> _columns = [];
    private readonly List<string[]> _rows = [];

    // Declare a column. Call once per column in left-to-right order
    // before adding rows. Mixing AddColumn calls with AddRow calls is
    // unsupported — the column count is captured implicitly by the
    // first AddRow.
    public MarkdownTable AddColumn(string header, MarkdownAlign align = MarkdownAlign.Left)
    {
        ArgumentNullException.ThrowIfNull(header);
        _columns.Add((header, align));
        return this;
    }

    // Convenience for `AddColumn` per (header, align) pair.
    public MarkdownTable AddColumns(params (string Header, MarkdownAlign Align)[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        foreach (var (header, align) in columns) AddColumn(header, align);
        return this;
    }

    // Append one data row. Cell count must match the declared column
    // count or the call throws — a silent mismatch would corrupt every
    // row past the offending one.
    public MarkdownTable AddRow(params string?[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length != _columns.Count)
            throw new ArgumentException(
                $"MarkdownTable.AddRow: row has {cells.Length} cell(s), table has {_columns.Count} column(s).",
                nameof(cells));
        _rows.Add(cells.Select(c => c ?? "").ToArray());
        return this;
    }

    // ─── Convenience overload for IFormattable values ─────────────────
    // Numerics and dates go through the invariant culture so the
    // source is locale-stable (a German workstation must produce the
    // same `2.5` the CI runner does, not `2,5`).
    public MarkdownTable AddRow(params object?[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var asStrings = cells
            .Select(c => c switch
            {
                null            => "",
                string s        => s,
                IFormattable f  => f.ToString(format: null, formatProvider: CultureInfo.InvariantCulture),
                _               => c.ToString() ?? "",
            })
            .ToArray();
        return AddRow(asStrings);
    }

    public int ColumnCount => _columns.Count;
    public int RowCount    => _rows.Count;

    // Render to a string. Trailing newline included so the table sits
    // cleanly inside a larger document.
    public string Render()
    {
        var sb = new StringBuilder();
        RenderTo(sb);
        return sb.ToString();
    }

    // GFM mandates at least three dashes in a separator row. Clamping
    // the column's *display* width to >= MinColumnWidth keeps the
    // header, separator, and data rows all flush — otherwise a
    // single-character column would have padded header `| A |` but a
    // separator `| --- |`, and the table source mis-aligns visually.
    private const int MinColumnWidth = 3;

    public void RenderTo(StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(sb);
        if (_columns.Count == 0)
            throw new InvalidOperationException("MarkdownTable.Render: no columns declared.");

        Span<int> widths = stackalloc int[_columns.Count];
        for (var i = 0; i < _columns.Count; i++)
            widths[i] = DisplayWidth(_columns[i].Header);

        foreach (var row in _rows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                var w = DisplayWidth(row[i]);
                if (w > widths[i]) widths[i] = w;
            }
        }

        for (var i = 0; i < widths.Length; i++)
            if (widths[i] < MinColumnWidth) widths[i] = MinColumnWidth;

        // Header row.
        sb.Append('|');
        for (var i = 0; i < _columns.Count; i++)
        {
            sb.Append(' ');
            AppendAligned(sb, _columns[i].Header, widths[i], _columns[i].Align);
            sb.Append(' ');
            sb.Append('|');
        }
        sb.Append('\n');

        // Separator row. Alignment markers (`:`) eat from the dashes,
        // so the minimum separator is `---` (3 chars) when the
        // alignment is Left or Right and `:-:` (3 chars) for Center.
        sb.Append('|');
        for (var i = 0; i < _columns.Count; i++)
        {
            sb.Append(' ');
            AppendSeparator(sb, widths[i], _columns[i].Align);
            sb.Append(' ');
            sb.Append('|');
        }
        sb.Append('\n');

        // Data rows.
        foreach (var row in _rows)
        {
            sb.Append('|');
            for (var i = 0; i < row.Length; i++)
            {
                sb.Append(' ');
                AppendAligned(sb, row[i], widths[i], _columns[i].Align);
                sb.Append(' ');
                sb.Append('|');
            }
            sb.Append('\n');
        }
    }

    private static void AppendAligned(StringBuilder sb, string value, int width, MarkdownAlign align)
    {
        var current = DisplayWidth(value);
        var pad = Math.Max(0, width - current);
        switch (align)
        {
            case MarkdownAlign.Left:
                sb.Append(value);
                sb.Append(' ', pad);
                break;
            case MarkdownAlign.Right:
                sb.Append(' ', pad);
                sb.Append(value);
                break;
            case MarkdownAlign.Center:
                var left = pad / 2;
                var right = pad - left;
                sb.Append(' ', left);
                sb.Append(value);
                sb.Append(' ', right);
                break;
        }
    }

    private static void AppendSeparator(StringBuilder sb, int width, MarkdownAlign align)
    {
        // RenderTo() already clamped width to >= MinColumnWidth (3),
        // so the dash count for any align is positive.
        switch (align)
        {
            case MarkdownAlign.Left:
                sb.Append('-', width);
                break;
            case MarkdownAlign.Right:
                sb.Append('-', width - 1);
                sb.Append(':');
                break;
            case MarkdownAlign.Center:
                sb.Append(':');
                sb.Append('-', width - 2);
                sb.Append(':');
                break;
        }
    }

    private static int DisplayWidth(string s)
    {
        // Rune count is a better proxy for terminal width than
        // `s.Length` (UTF-16 code units), which over-counts surrogate
        // pairs. Wide CJK + double-width emoji still under-count by
        // one column per rune — see the type-level note above.
        var width = 0;
        foreach (var _ in s.EnumerateRunes()) width++;
        return width;
    }
}
