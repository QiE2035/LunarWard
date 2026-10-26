using System.Globalization;
using LunarWard.Language;
using LunarWard.Localization;

namespace LunarWard.Features.Setting;

/// <summary>
/// 让设置页使用上游 <see cref="Lang"/> 资源时与配置里的界面语言一致。
/// </summary>
/// <remarks>
/// 上游在启动时 <c>AppConfig.SetLanguage</c> 设置资源文化，随后各页直接用资源属性；
/// 本仓库的设置页现在也走 <see cref="Lang"/>，但页面可能先于语言初始化被构造
/// （截图自检、设计器、测试），所以每个设置页构造时对齐一次。
/// </remarks>
internal static class SettingLocalization
{
    public static void EnsureConfigured()
    {
        try
        {
            string code = AppStrings.Normalize(AppServices.Current.Config.Launcher.Language);
            CultureInfo culture = CultureInfo.GetCultureInfo(code);
            if (!string.Equals(AppConfig.Language, code, StringComparison.OrdinalIgnoreCase))
            {
                AppConfig.SetLanguage(code);
            }
            if (!string.Equals(Lang.Culture?.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
            {
                Lang.Culture = culture;
            }
        }
        catch
        {
            // 语言配置异常不阻塞页面构造
        }
    }
}
