using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.App.Localization;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>语言下拉里的一项（代码 + 显示名）。</summary>
public sealed record SettingLanguageOption(string Code, string Display);
