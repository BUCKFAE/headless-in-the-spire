using Sts2Headless.Utils;
using Xunit;

namespace Sts2Headless.UnitTests;

public sealed class MarkdownTableTests
{
    [Fact]
    public void Single_Column_With_One_Row_Renders_Padded()
    {
        var actual = new MarkdownTable()
            .AddColumn("Name")
            .AddRow("alpha")
            .Render();

        // Width = max("Name", "alpha") = 5. Both cells padded to 5 chars.
        const string expected =
            "| Name  |\n" +
            "| ----- |\n" +
            "| alpha |\n";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Right_Align_Uses_Colon_Suffix_And_Pads_Left()
    {
        var actual = new MarkdownTable()
            .AddColumn("Wins", MarkdownAlign.Right)
            .AddRow("0/5")
            .AddRow("11/50")
            .Render();

        // Widths: max("Wins"=4, "0/5"=3, "11/50"=5) = 5.
        const string expected =
            "|  Wins |\n" +
            "| ----: |\n" +
            "|   0/5 |\n" +
            "| 11/50 |\n";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Center_Align_Pads_Both_Sides_With_Colon_Markers()
    {
        var actual = new MarkdownTable()
            .AddColumn("Status", MarkdownAlign.Center)
            .AddRow("ok")
            .Render();

        // Width = "Status" = 6, "ok" centred in 6 = 2 left + "ok" + 2 right.
        const string expected =
            "| Status |\n" +
            "| :----: |\n" +
            "|   ok   |\n";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Multi_Column_Mixed_Alignment_Looks_Like_The_Eval_Leaderboard()
    {
        var md = new MarkdownTable()
            .AddColumns(
                ("#",       MarkdownAlign.Right),
                ("Agent",   MarkdownAlign.Left),
                ("Wins",    MarkdownAlign.Right),
                ("Win%",    MarkdownAlign.Right))
            .AddRow("1", "`greedy`",   "0/5",  "0%")
            .AddRow("2", "`ironclad`", "1/5", "20%")
            .Render();

        // Each column padded to max(MinColumnWidth=3, header, cells).
        // The "#" column has natural width 1 — clamped to 3 so the
        // separator row's `---:` and the data row's `  1` line up.
        const string expected =
            "|   # | Agent      | Wins | Win% |\n" +
            "| --: | ---------- | ---: | ---: |\n" +
            "|   1 | `greedy`   |  0/5 |   0% |\n" +
            "|   2 | `ironclad` |  1/5 |  20% |\n";
        Assert.Equal(expected, md);
    }

    [Fact]
    public void Header_Wider_Than_Data_Pads_Data_To_Header_Width()
    {
        var md = new MarkdownTable()
            .AddColumn("LongHeader")
            .AddRow("x")
            .Render();
        const string expected =
            "| LongHeader |\n" +
            "| ---------- |\n" +
            "| x          |\n";
        Assert.Equal(expected, md);
    }

    [Fact]
    public void Mismatched_Row_Width_Throws()
    {
        var t = new MarkdownTable()
            .AddColumn("A")
            .AddColumn("B");
        Assert.Throws<ArgumentException>(() => t.AddRow("only one"));
        Assert.Throws<ArgumentException>(() => t.AddRow("one", "two", "three"));
    }

    [Fact]
    public void Render_Throws_When_No_Columns_Declared()
    {
        var t = new MarkdownTable();
        Assert.Throws<InvalidOperationException>(() => t.Render());
    }

    [Fact]
    public void Null_Cell_Is_Treated_As_Empty_String()
    {
        var actual = new MarkdownTable()
            .AddColumn("A")
            .AddColumn("B")
            .AddRow("x", null)
            .Render();
        // Both columns clamp to MinColumnWidth=3 since their natural
        // width (1) is below the GFM separator minimum.
        const string expected =
            "| A   | B   |\n" +
            "| --- | --- |\n" +
            "| x   |     |\n";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Object_Row_Overload_Uses_Invariant_Culture_For_Numbers()
    {
        var actual = new MarkdownTable()
            .AddColumn("Mean", MarkdownAlign.Right)
            .AddRow(31.4)
            .AddRow(6.6)
            .Render();
        // 31.4 with invariant culture is "31.4" — never "31,4".
        const string expected =
            "| Mean |\n" +
            "| ---: |\n" +
            "| 31.4 |\n" +
            "|  6.6 |\n";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Bmp_Symbol_Counts_As_One_Column_Wide()
    {
        // The eval summary has `Engine⚠` (7 runes). A regression that
        // double-counted the BMP symbol would skew the column. Cover
        // the case so the contract is explicit.
        var actual = new MarkdownTable()
            .AddColumn("Engine⚠", MarkdownAlign.Right)
            .AddRow("0")
            .Render();
        // "Engine⚠" displays at width 7; right-align "0" pads to 7.
        const string expected =
            "| Engine⚠ |\n" +
            "| ------: |\n" +
            "|       0 |\n";
        Assert.Equal(expected, actual);
    }
}
