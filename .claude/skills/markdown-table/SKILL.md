---
name: markdown-table
description: Write aligned, source-readable Markdown tables. Activates whenever you're about to emit a `| col | col |` GFM table — in C# code, Python, scripts, docs, PR descriptions, or anywhere else. The dominant failure mode is hand-rolling `sb.AppendLine($"| {x} | {y} | …")` with header-only separator dashes, which renders fine in GitHub but leaves an unreadable, diff-hostile source. This skill points you at the in-repo helper for C# code paths and at the manual padding rule for everything else.
---

# Markdown tables that don't make people squint

The header you saw in the user's last paste was the problem this skill exists
to prevent:

```markdown
| # | Agent | Version | Wins | Win% | Mean floor | p25 floor | Engine⚠ | Agent⚠ | Host⚠ | Timeout | Median wall |
|---|-------|---------|-----:|-----:|-----------:|----------:|--------:|-------:|------:|--------:|------------:|
| 1 | `greedy` | 0.1.0 | 0/5 | 0% | 6.6 | 5 | 0 | 0 | 0 | 0 | 4.4s |
```

GFM renders it fine. Humans reading the raw `.md` file (`cat`, IDE, `git diff`,
PR review pane) see a mess. The fix is to pad cells in the source so columns
line up visually:

```markdown
|   # | Agent      | Version | Wins | Win% | Mean floor | p25 floor | Engine⚠ | Agent⚠ | Host⚠ | Timeout | Median wall |
| --: | ---------- | ------- | ---: | ---: | ---------: | --------: | ------: | -----: | ----: | ------: | ----------: |
|   1 | `greedy`   | 0.1.0   |  0/5 |   0% |        6.6 |         5 |       0 |      0 |     0 |       0 |        4.4s |
```

## How to apply

### C# code in this repo — use `Sts2Headless.Utils.MarkdownTable`

Anything that emits a table from C# **must** route through the helper.
Hand-rolled `StringBuilder.AppendLine($"| {x} | {y} |")` is a regression and
will drift the first time someone adds a column.

```csharp
using Sts2Headless.Utils;

var table = new MarkdownTable()
    .AddColumns(
        ("#",       MarkdownAlign.Right),
        ("Agent",   MarkdownAlign.Left),
        ("Wins",    MarkdownAlign.Right),
        ("Win%",    MarkdownAlign.Right));

foreach (var row in ranking)
{
    table.AddRow(
        row.Rank,                                                // IFormattable → invariant culture
        $"`{row.Agent.Name}`",
        $"{row.Wins}/{row.Cells}",
        $"{row.WinRate * 100:0.#}%");
}

sb.Append(table.Render());        // or table.RenderTo(sb) to stream
```

Rules the helper enforces for you:

- Column widths take the max of (header, all cells).
- Separator row is a valid GFM separator with the correct `:` markers for the
  declared alignment (`Left`, `Right`, `Center`).
- Cells shorter than 3 chars are padded to 3 — GFM mandates ≥ 3 dashes in
  the separator, and clamping the data rows to match keeps everything flush.
- Numbers serialise through invariant culture (`31.4`, never `31,4`).
- BMP symbols (`⚠`, en-dash, …) count as one column wide; rune count > UTF-16
  code units. Wide CJK / supplementary-plane emoji will be slightly off — flag
  this in your PR if you're shipping a table with those glyphs.

If you find yourself reaching for `string.Format` + `PadLeft` + literal pipes,
stop and use the helper. The contract is "data → table", not "data → table
that I padded myself this time".

### Outside C# — pad cells by hand

For Python, scripts, PR descriptions, or docs:

1. **Figure column widths**: each column's width is the max length of the
   header and every data cell.
2. **Clamp to ≥ 3**: GFM requires three dashes minimum.
3. **Pad cells**:
   - Left-aligned (default): pad with spaces on the **right**.
   - Right-aligned (`---:`): pad with spaces on the **left**.
   - Center (`:---:`): split padding both sides.
4. **Separator row**: dashes filling the column width, with `:` markers for
   alignment.

For Python-side renderers that already exist (e.g. the
`headless-in-the-spire-leaderboard` package), keep them byte-compatible with
the C# `MarkdownTable.Render()` output — same column widths, same alignment,
same `:` placement. A diff-tool comparison between the two emissions is a
useful belt-and-braces.

### When you're tempted to "just inline a small one"

A 2×2 reminder table feels too small to bother with the helper. Don't do it
anyway — small tables grow columns, and the next change adds drift you have
to undo. The helper costs three lines.

## What this skill is NOT for

- **Markdown rendering inside a chat reply.** Reply markdown gets rendered by
  Claude Code's display layer; ad-hoc tables in conversation don't need
  alignment, they need correct GFM. Don't reach for the helper for
  "let me show you the matrix" turns.
- **HTML tables / Jira tables / Confluence tables.** This skill is GFM only.
- **Bookkeeping CSVs**, structured data dumps, or anything that should be a
  `.csv` / `.json` artefact. Tables are for humans; if a tool will read it
  back, emit the structured shape.
