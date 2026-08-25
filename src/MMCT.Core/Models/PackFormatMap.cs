namespace MMCT.Core.Models;

public static class PackFormatMap
{
    private static readonly Dictionary<string, int> _map = new()
    {
        { "1.21.4", 42 }, { "1.21.3", 42 }, { "1.21.2", 41 }, { "1.21.1", 39 },
        { "1.21", 34 }, { "1.20.6", 32 }, { "1.20.5", 32 }, { "1.20.4", 32 },
        { "1.20.3", 22 }, { "1.20.2", 18 }, { "1.20.1", 15 }, { "1.20", 15 },
        { "1.19.4", 13 }, { "1.19.3", 12 }, { "1.19.2", 9 }, { "1.19.1", 9 },
        { "1.19", 9 }, { "1.18.2", 8 }, { "1.18.1", 8 }, { "1.18", 8 },
        { "1.17.1", 7 }, { "1.17", 7 }, { "1.16.5", 6 }, { "1.16.4", 6 },
        { "1.16.3", 6 }, { "1.16.2", 6 }, { "1.16.1", 5 }, { "1.16", 5 },
        { "1.15.2", 5 }, { "1.15.1", 5 }, { "1.15", 5 }, { "1.14.4", 4 },
        { "1.14.3", 4 }, { "1.14.2", 4 }, { "1.14.1", 4 }, { "1.14", 4 },
        { "1.13.2", 4 }, { "1.13.1", 4 }, { "1.13", 4 },
    };

    public static int GetPackFormat(string version)
    {
        if (_map.TryGetValue(version, out var format))
            return format;

        var prefix = version[..version.LastIndexOf('.')];
        foreach (var (key, value) in _map.OrderByDescending(kv => kv.Key))
        {
            if (key.StartsWith(prefix))
                return value;
        }
        return 15;
    }

    public static List<string> GetSupportedVersions() => _map.Keys.Distinct().OrderByDescending(v =>
    {
        var parts = v.Split('.').Select(int.Parse).ToArray();
        return parts[0] * 10000 + parts[1] * 100 + (parts.Length > 2 ? parts[2] : 0);
    }).ToList();
}
