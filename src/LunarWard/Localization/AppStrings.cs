using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace LunarWard.Localization;

/// <summary>
/// 应用自身的 UI 文案（zh-CN / en-US），存储为 .resx，与上游 Starward 同形态。
/// </summary>
/// <remarks>
/// <para>
/// 词条在 <c>Localization/Resources.resx</c>（中性语言 = 中文，与上游一致）和
/// <c>Localization/Resources.en-US.resx</c>（英文附属程序集）。改文案只改 resx，
/// 不要再往代码里加字典；缺键时 ResourceManager 会自动回退到中性语言的同名键。
/// </para>
/// <para>
/// 只覆盖 LunarWard 自己的界面文字。<c>LunarWard.Core</c> 的游戏名、区服名等
/// 已经由 Core 的 <c>CoreLang</c> 资源（13 种语言）提供，不要在这里重复。
/// </para>
/// <para>
/// 用法：XAML 里 <c>{Binding [Main.Launch], Source={x:Static loc:AppStrings.Instance}}</c>；
/// 代码里 <c>AppStrings.Instance["Main.Launch"]</c>。
/// 索引器返回的是当前语言的文案；<b>查不到时返回键名本身</b>，这样漏翻译会立刻显形，
/// 而不是显示空白。
/// </para>
/// </remarks>
public sealed class AppStrings : INotifyPropertyChanged
{
    /// <summary>resx 的基名（= 程序集内嵌资源名 <c>LunarWard.Localization.Resources.resources</c>）。</summary>
    private const string ResourceBaseName = "LunarWard.Localization.Resources";

    private const string DefaultLanguage = "zh-CN";
    private const string EnglishLanguage = "en-US";

    private static readonly ResourceManager Resources = new(ResourceBaseName, typeof(AppStrings).Assembly);


    public static AppStrings Instance { get; } = new();


    private string _language = DefaultLanguage;


    /// <summary>当前语言（<c>zh-CN</c> | <c>en-US</c>）。</summary>
    public string Language
    {
        get => _language;
        set
        {
            var normalized = Normalize(value);
            if (_language == normalized)
            {
                return;
            }

            _language = normalized;
            // 空字符串 = "所有属性都变了"（WPF/Avalonia 的既有约定），让 Language 与索引器绑定一起重读；
            // 索引器再显式发一次 "Item[]"，兼容只认这个名的绑定实现（与迁移前行为一致）。
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }


    public IReadOnlyList<string> SupportedLanguages { get; } = [DefaultLanguage, EnglishLanguage];


    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key))
            {
                return key ?? string.Empty;
            }

            // 查不到时 ResourceManager 返回 null：回退成键名（刻意行为，见类型注释）。
            return Resources.GetString(key, CultureOf(_language)) ?? key;
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>把系统语言选项归一化到受支持的语言。</summary>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultLanguage;
        }

        var text = language.Trim().Replace('_', '-');
        if (text.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return EnglishLanguage;
        }

        if (text.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultLanguage;
        }

        return DefaultLanguage;
    }


    /// <summary>所有键（供遗漏检查/测试用）。取中性语言资源集。</summary>
    public static IReadOnlyCollection<string> AllKeys => KeysOf(DefaultLanguage);


    /// <summary>英文表是否覆盖了中文表的全部键。</summary>
    public static IReadOnlyList<string> MissingEnglishKeys
    {
        get
        {
            var english = new HashSet<string>(KeysOf(EnglishLanguage), StringComparer.Ordinal);
            return KeysOf(DefaultLanguage).Where(k => !english.Contains(k)).ToList();
        }
    }


    private static CultureInfo CultureOf(string language) => CultureInfo.GetCultureInfo(language);


    private static IReadOnlyList<string> KeysOf(string language)
    {
        var set = Resources.GetResourceSet(CultureOf(language), createIfNotExists: true, tryParents: true);
        if (set is null)
        {
            return [];
        }

        var keys = new List<string>();
        foreach (DictionaryEntry entry in set)
        {
            if (entry.Key is string key)
            {
                keys.Add(key);
            }
        }

        // 与迁移前 Dictionary 的枚举顺序不同，排序后测试与巡检输出才稳定
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }
}
