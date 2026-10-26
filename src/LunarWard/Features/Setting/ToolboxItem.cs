namespace LunarWard.Features.Setting;

/// <summary>工具箱卡片（Starward <c>ToolboxItem</c>）：图标 + 名称 + 说明 + 动作。</summary>
public sealed class ToolboxItem
{
    private readonly Action _action;


    public ToolboxItem(string icon, string name, string description, Action action)
    {
        Icon = icon;
        Name = name;
        Description = description;
        _action = action;
    }


    public string Icon { get; }

    public string Name { get; }

    public string Description { get; }

    public void Invoke() => _action();
}
