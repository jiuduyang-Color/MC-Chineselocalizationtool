namespace MMCT.Core.Models;

public class TranslationBatch
{
    public List<TranslationItem> Items { get; set; } = new();
    public int TotalChars { get; set; }
}

public class TranslationItem
{
    public string Key { get; set; } = "";
    public string SourceText { get; set; } = "";
    public string? TranslatedText { get; set; }
}
