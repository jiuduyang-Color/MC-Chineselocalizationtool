namespace MMCT.Core.UI;

/// <summary>
/// Rotating Minecraft-themed status messages.
/// Bilingual: Chinese puns for ZH UI, English puns for EN UI.
/// No emoji (causes console width miscalculation and line-wrap bugs).
/// </summary>
public class McPunRotator
{
    private static readonly string[] PunsZh = new[]
    {
        "啥bug？这是特性！",
        "苦力怕：Ssssss... BOOM！",
        "挖三填一，安全第一",
        "Steve 还在挖矿中...",
        "末影人：别看我的眼睛！",
        "村民：Hmm?",
        "20铁=1绿宝石？奸商！",
        "Herobrine 已移除。",
        "红石科技，启动！",
        "钻石在 y=-59 层",
        "去下界记得带水！",
        "Notch 保佑翻译成功",
        "合成：工作台 x1",
        "你死了！分数：翻译中...",
        "/gamemode creative",
        "下界合金套：我在哪？",
        "正在给猪灵扔金锭...",
        "效率 V 的键盘输入中",
        "TNT 加速翻译中",
        "末影龙：你不是来打我的？",
        "别按那个按钮！",
        "F3+B 显示碰撞箱",
        "鞘翅起飞！",
        "信标亮起，翻译开始"
    };

    private static readonly string[] PunsEn = new[]
    {
        "It's not a bug, it's a feature!",
        "Creeper? Aww man...",
        "Diggy Diggy Hole",
        "Steve is mining...",
        "Enderman: don't look!",
        "Ssssss... BOOM!",
        "Villager: Hmm?",
        "20 iron = 1 emerald?!",
        "Herobrine removed.",
        "Redstone-powered translation!",
        "Diamonds at y=-59",
        "Bring water to the Nether!",
        "Notch bless this translation",
        "Crafting: workbench x1",
        "You died! Score: translating...",
        "/gamemode creative",
        "Netherite armor: where am I?",
        "Piglin trading in progress",
        "Efficiency V typing...",
        "TNT-powered progress bar",
        "Ender Dragon: really?",
        "Don't press that button!",
        "F3+B shows hitboxes",
        "Elytra go brrr"
    };

    private readonly string[] _puns;
    private int _index = -1;

    public McPunRotator()
    {
        _puns = I18n.Current == UILanguage.Chinese ? PunsZh : PunsEn;
    }

    public int Count => _puns.Length;

    public string Next()
    {
        _index = (_index + 1) % _puns.Length;
        return _puns[_index];
    }

    public string PeekRandom(int seed)
    {
        var i = (int)((uint)seed % (uint)_puns.Length);
        return _puns[i];
    }
}
