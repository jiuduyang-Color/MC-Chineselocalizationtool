using MMCT.Core.Models;

namespace MMCT.Core.Clients;

public interface IAiClient
{
    Task<string> TranslateAsync(List<TranslationItem> items, CancellationToken ct = default);
    string ProviderName { get; }
}
