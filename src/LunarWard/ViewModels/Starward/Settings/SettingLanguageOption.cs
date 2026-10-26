using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Localization;
using LunarWard.Views.Starward;

namespace LunarWard.ViewModels.Starward;

/// <summary>语言下拉里的一项（代码 + 显示名）。</summary>
public sealed record SettingLanguageOption(string Code, string Display);
