using LunarWard.App.Localization;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>应用自身 UI 的 i18n（docs/05-ROADMAP.md 交付清单要求 zh-CN + en-US）。</summary>
public class AppStringsTests
{
    [Fact]
    public void English_CoversEveryChineseKey()
    {
        var missing = AppStrings.MissingEnglishKeys;
        Assert.True(missing.Count == 0, "缺少英文翻译：" + string.Join(", ", missing));
    }


    /// <summary>中文词条里不该出现纯英文值（上一轮把英文键插进中文字典就是这么暴露的）。</summary>
    [Fact]
    public void Chinese_ValuesAreNotAccidentallyEnglish()
    {
        AppStrings.Instance.Language = "zh-CN";
        var suspects = new List<string>();
        foreach (var key in AppStrings.AllKeys)
        {
            var value = AppStrings.Instance[key];
            // 只抓「纯 ASCII 多词短语」：单缩写（HUD / UID）与带全角符号的值都不算嫌疑
            bool hasCjkOrFullWidth = value.Any(c => c >= 0x3000 && c <= 0x9FFF);
            bool isAsciiPhrase = value.All(c => c < 0x80) && value.Contains(' ');
            if (!hasCjkOrFullWidth && isAsciiPhrase)
            {
                suspects.Add($"{key} = {value}");
            }
        }

        Assert.True(suspects.Count == 0, "中文字典里疑似混入英文：" + string.Join(" | ", suspects.Take(10)));
    }


    [Fact]
    public void BothLanguages_HaveNoEmptyValues()
    {
        foreach (var language in new[] { "zh-CN", "en-US" })
        {
            AppStrings.Instance.Language = language;
            foreach (var key in AppStrings.AllKeys)
            {
                Assert.False(string.IsNullOrWhiteSpace(AppStrings.Instance[key]), $"{language}: {key} 为空");
            }
        }

        AppStrings.Instance.Language = "zh-CN";
    }


    [Fact]
    public void UnknownKey_ReturnsKeyItself()
    {
        AppStrings.Instance.Language = "zh-CN";
        Assert.Equal("No.Such.Key", AppStrings.Instance["No.Such.Key"]);
    }


    [Fact]
    public void Language_SwitchesValues()
    {
        var strings = AppStrings.Instance;
        try
        {
            strings.Language = "zh-CN";
            Assert.Equal("启动", strings["Main.Launch"]);

            strings.Language = "en-US";
            Assert.Equal("Launch", strings["Main.Launch"]);
        }
        finally
        {
            strings.Language = "zh-CN";
        }
    }


    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh_CN", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("zh", "zh-CN")]
    [InlineData("en-US", "en-US")]
    [InlineData("en_GB", "en-US")]
    [InlineData("en", "en-US")]
    [InlineData("ja-JP", "zh-CN")]
    [InlineData(null, "zh-CN")]
    [InlineData("", "zh-CN")]
    public void Normalize_MapsToSupportedLanguage(string? input, string expected)
    {
        Assert.Equal(expected, AppStrings.Normalize(input));
    }


    [Fact]
    public void LanguageChange_RaisesItemIndexerNotification()
    {
        var strings = AppStrings.Instance;
        var raised = new List<string>();
        void Handler(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => raised.Add(e.PropertyName ?? "");

        strings.PropertyChanged += Handler;
        try
        {
            strings.Language = "en-US";
            strings.Language = "en-US"; // 同值不重复通知
        }
        finally
        {
            strings.PropertyChanged -= Handler;
            strings.Language = "zh-CN";
        }

        Assert.Contains("Item[]", raised);
        Assert.Single(raised, x => x == "Item[]");
    }


    [Fact]
    public void NoDuplicateChineseValues_ForDistinctMeaningless()
    {
        // 只是防止复制粘贴时把两个键写成同一个东西（英文同理）
        AppStrings.Instance.Language = "zh-CN";
        var values = AppStrings.AllKeys.Select(k => AppStrings.Instance[k]).ToList();
        Assert.All(values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    /// <summary>
    /// 两个字典内部都不允许重复键（审计 CO-01）。Dictionary 的索引器初始化会静默覆盖，
    /// 重复时后写的那份永远不会生效，运行时也看不出问题——只能解析源码。
    /// </summary>
    [Fact]
    public void Source_HasNoDuplicateKeysInEitherDictionary()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Join(root, "LunarWard.slnx")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new InvalidOperationException("找不到仓库根目录");
        }
        var lines = File.ReadAllLines(Path.Join(root, "src", "LunarWard.App", "Localization", "AppStrings.cs"));
        var spans = new List<(int Start, int End)>();
        var starts = lines.Select((line, index) => (line, index))
            .Where(x => x.line.Contains("Dictionary<string, string>", StringComparison.Ordinal))
            .Select(x => x.index).ToList();
        for (var i = 0; i < starts.Count; i++)
        {
            spans.Add((starts[i], i + 1 < starts.Count ? starts[i + 1] : lines.Length));
        }

        var keyPattern = new System.Text.RegularExpressions.Regex("^\\s*\\[\"([^\"]+)\"\\]\\s*=");
        var duplicates = new List<string>();
        foreach (var (start, end) in spans)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = start; i < end; i++)
            {
                var match = keyPattern.Match(lines[i]);
                if (match.Success && !seen.Add(match.Groups[1].Value))
                {
                    duplicates.Add($"{match.Groups[1].Value}（第 {i + 1} 行）");
                }
            }
        }

        Assert.True(duplicates.Count == 0, "发现重复键：" + string.Join(", ", duplicates));
    }

}
