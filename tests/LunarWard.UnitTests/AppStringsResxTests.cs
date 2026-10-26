using System.Xml.Linq;
using LunarWard.App.Localization;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 文案存储是 .resx（中性语言 = 中文）+ <c>Resources.en-US.resx</c> 附属程序集，
/// 与上游 Starward 同形态；对外仍是 <see cref="AppStrings"/> 的索引器。
/// </summary>
public class AppStringsResxTests
{
    private static readonly string LocalizationDirectory = Path.Join(
        RepositoryRoot(), "src", "LunarWard.App", "Localization");

    private static string NeutralResx => Path.Join(LocalizationDirectory, "Resources.resx");
    private static string EnglishResx => Path.Join(LocalizationDirectory, "Resources.en-US.resx");


    [Fact]
    public void NeutralAndEnglishResx_HaveIdenticalKeySets()
    {
        var zh = ReadEntries(NeutralResx);
        var en = ReadEntries(EnglishResx);

        var onlyZh = zh.Keys.Except(en.Keys, StringComparer.Ordinal).ToList();
        var onlyEn = en.Keys.Except(zh.Keys, StringComparer.Ordinal).ToList();

        Assert.True(onlyZh.Count == 0, "英文缺这些键：" + string.Join(", ", onlyZh.Take(20)));
        Assert.True(onlyEn.Count == 0, "中文缺这些键：" + string.Join(", ", onlyEn.Take(20)));
        Assert.True(zh.Count > 1000, $"键数看起来不对：{zh.Count}");
    }


    [Fact]
    public void ResxFiles_HaveNoDuplicateNames()
    {
        // .resx 里 name 重复时只有最后一个生效，编译期不会报错——只能解析文件。
        foreach (var path in new[] { NeutralResx, EnglishResx })
        {
            var names = XDocument.Load(path).Root!
                .Elements("data")
                .Select(e => e.Attribute("name")!.Value)
                .ToList();
            var duplicates = names.GroupBy(n => n, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(duplicates.Count == 0,
                $"{Path.GetFileName(path)} 有重复 name：" + string.Join(", ", duplicates.Take(20)));
        }
    }


    [Fact]
    public void ResxFiles_HaveNoEmptyValues()
    {
        foreach (var path in new[] { NeutralResx, EnglishResx })
        {
            var empty = ReadEntries(path)
                .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => kv.Key)
                .ToList();

            Assert.True(empty.Count == 0,
                $"{Path.GetFileName(path)} 有空值：" + string.Join(", ", empty.Take(20)));
        }
    }


    /// <summary>中性 resx 必须是中文（与上游 Starward 一致），否则默认语言会变成英文。</summary>
    [Fact]
    public void NeutralResx_IsChinese()
    {
        var zh = ReadEntries(NeutralResx);

        Assert.Equal("启动", zh["Main.Launch"]);
        Assert.Equal("Launch", ReadEntries(EnglishResx)["Main.Launch"]);
    }


    [Theory]
    [InlineData("Main.Launch")]
    [InlineData("Common.Close")]
    [InlineData("Launcher.Gallery")]
    [InlineData("Settings.Title")]
    [InlineData("Diagnose.Title")]
    public void KnownKeys_ReturnNonEmptyValuesDistinctFromKey(string key)
    {
        var strings = AppStrings.Instance;
        try
        {
            foreach (var language in new[] { "zh-CN", "en-US" })
            {
                strings.Language = language;
                var value = strings[key];
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: {key} 为空");
                Assert.NotEqual(key, value);
            }
        }
        finally
        {
            strings.Language = "zh-CN";
        }
    }


    [Fact]
    public void UnknownKey_ReturnsKeyItself()
    {
        var strings = AppStrings.Instance;
        try
        {
            strings.Language = "en-US";
            Assert.Equal("No.Such.Key", strings["No.Such.Key"]);
        }
        finally
        {
            strings.Language = "zh-CN";
        }
    }


    /// <summary>
    /// 切语言要让 XAML 重新取词：既发"所有属性都变了"（空字符串），也发索引器约定的 "Item[]"。
    /// </summary>
    [Fact]
    public void LanguageChange_RaisesAllPropertiesAndIndexerNotifications()
    {
        var strings = AppStrings.Instance;
        var raised = new List<string>();
        void Handler(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            => raised.Add(e.PropertyName ?? "<null>");

        strings.PropertyChanged += Handler;
        try
        {
            strings.Language = "en-US";
        }
        finally
        {
            strings.PropertyChanged -= Handler;
            strings.Language = "zh-CN";
        }

        Assert.Contains(string.Empty, raised);
        Assert.Contains("Item[]", raised);
    }


    [Fact]
    public void SwitchingToEnglish_ReturnsEnglishText()
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


    /// <summary>同一批键在两个资源里的取值必须真的不同（防止英文文件被整份复制成中文）。</summary>
    [Fact]
    public void EnglishResx_IsNotACopyOfChinese()
    {
        var zh = ReadEntries(NeutralResx);
        var en = ReadEntries(EnglishResx);
        var identical = zh.Keys.Count(k => en.TryGetValue(k, out var v) && v == zh[k]);

        Assert.True(identical < zh.Count / 2,
            $"{identical}/{zh.Count} 条英文值和中文字典完全相同，英文 resx 可能被覆盖成了中文");
    }


    private static Dictionary<string, string> ReadEntries(string path)
    {
        return XDocument.Load(path).Root!
            .Elements("data")
            .ToDictionary(
                e => e.Attribute("name")!.Value,
                e => e.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }


    private static string RepositoryRoot()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Join(root, "LunarWard.slnx")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new InvalidOperationException("找不到仓库根目录");
        }

        return root;
    }
}
