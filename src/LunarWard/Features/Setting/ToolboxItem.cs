using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Language;

namespace LunarWard.Features.Setting;

/// <summary>工具箱卡片（上游 <c>Features/Setting/ToolboxItem.cs</c>）：图标/图 + 名称 + 说明 + 窗口标识。</summary>
public class ToolboxItem : ObservableObject
{
    public string? Icon { get; set; }

    public string? Image { get; set; }

    public string Name { get; set => SetProperty(ref field, value); }

    public string Description { get; set => SetProperty(ref field, value); }

    public string Tag { get; set; }

    public string NameResourceKey { get; set; }

    public string DescriptionResourceKey { get; set; }


    /// <summary>语言切换后重新从 <see cref="Lang"/> 资源取值（上游同名方法）。</summary>
    public void UpdateLanguage()
    {
        Name = Lang.ResourceManager.GetString(NameResourceKey, Lang.Culture) ?? Name;
        Description = Lang.ResourceManager.GetString(DescriptionResourceKey, Lang.Culture) ?? Description;
    }


    public ToolboxItem(string? icon, string? image, string tag, string nameResourceKey, string descriptionResourceKey)
    {
        Icon = icon;
        Image = image;
        Tag = tag;
        NameResourceKey = nameResourceKey;
        DescriptionResourceKey = descriptionResourceKey;
        UpdateLanguage();
    }
}
