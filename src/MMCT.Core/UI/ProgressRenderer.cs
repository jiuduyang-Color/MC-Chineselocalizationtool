using System.Text;

namespace MMCT.Core.UI;

/// <summary>
/// Renders a single-line, carriage-return-refreshed progress display.
/// Pure string function, fully unit-testable. NO Console I/O.
/// </summary>
public static class ProgressRenderer
{
    /// <summary>
    /// Build the progress line. The caller is responsible for padding/truncating
    /// to the console width and writing with \r.
    /// </summary>
    /// <param name="padTo">Target character width for padding (0 = no padding).</param>
    public static string RenderLine(
        int totalMods,
        int completedMods,
        string currentModName,
        long itemsDone,
        long itemsTotal,
        string pun,
        int barWidth = 16,
        int padTo = 0)
    {
        var modPct = totalMods <= 0 ? 100 : (int)Math.Round(100.0 * completedMods / totalMods);
        if (modPct > 100) modPct = 100;
        if (modPct < 0) modPct = 0;

        var filled = (int)Math.Round(barWidth * (modPct / 100.0));
        if (filled > barWidth) filled = barWidth;
        var bar = new string('=', filled) + new string('-', barWidth - filled);

        var itemText = itemsTotal <= 0 ? "-/-" : $"{itemsDone}/{itemsTotal}";
        var current = string.IsNullOrWhiteSpace(currentModName) ? "-" : currentModName;
        if (current.Length > 20) current = current[..17] + "...";

        // Compact format: [bar] P% M/T I/N | mod | pun
        var line = $"[{bar}] {modPct}% {completedMods}/{totalMods} {itemText} {current} {pun}";

        if (padTo > 0)
        {
            if (line.Length < padTo) line += new string(' ', padTo - line.Length);
            else if (line.Length > padTo) line = line[..padTo];
        }
        return line;
    }

    /// <summary>Calculate the display width of a string (East Asian wide chars = 2).</summary>
    public static int DisplayWidth(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int w = 0;
        foreach (var c in s)
            w += IsWide(c) ? 2 : 1;
        return w;
    }

    /// <summary>Truncate string to fit within <paramref name="maxDisplayWidth"/> display columns.</summary>
    public static string TruncateToWidth(string s, int maxDisplayWidth)
    {
        if (string.IsNullOrEmpty(s) || maxDisplayWidth <= 0) return "";
        int w = 0;
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            int cw = IsWide(c) ? 2 : 1;
            if (w + cw > maxDisplayWidth) break;
            sb.Append(c);
            w += cw;
        }
        return sb.ToString();
    }

    private static bool IsWide(char c) =>
        (c >= 0x1100 && c <= 0x115F) ||
        (c >= 0x2E80 && c <= 0xA4CF) ||
        (c >= 0xAC00 && c <= 0xD7A3) ||
        (c >= 0xF900 && c <= 0xFAFF) ||
        (c >= 0xFE30 && c <= 0xFE4F) ||
        (c >= 0xFF00 && c <= 0xFF60) ||
        (c >= 0xFFE0 && c <= 0xFFE6);
}
