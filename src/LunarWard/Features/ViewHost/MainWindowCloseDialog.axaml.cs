using Avalonia.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LunarWard.Helpers.Enumeration;
using LunarWard.Language;
using LunarWard.Views.Starward.Controls;

namespace LunarWard.Features.ViewHost;

/// <summary>
/// 关闭主窗口时的询问对话框（隐藏到托盘 / 退出）。<b>移植自</b>
/// <c>Starward/src/Starward/Features/ViewHost/MainWindowCloseDialog.xaml(.cs)</c>。
/// </summary>
/// <remarks>
/// <b>平台替换</b>：WinUI <c>ContentDialog</c> + <c>RadioButtons</c> 控件 → <see cref="StarwardContentDialog"/>
/// + 由 <see cref="EnumItemsSource{T}"/> 在 code-behind 生成的 <see cref="RadioButton"/> 列表。
/// 选项来源、双向绑定（<c>MainWindowCloseOption.Value</c>）与上游一致。
/// </remarks>
public sealed partial class MainWindowCloseDialog : StarwardContentDialog, INotifyPropertyChanged
{

    public event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>提示文字。</summary>
    public static string HintText => Lang.CloseWindowDialog_YouCouldChangeTheOptionAgainInSettingPage;


    public EnumItemsSource<MainWindowCloseOption> MainWindowCloseOption
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MainWindowCloseOption)));
        }
    } = new(LunarWard.Features.ViewHost.MainWindowCloseOption.Hide);


    public MainWindowCloseDialog()
    {
        InitializeComponent();
        BuildOptions();
    }


    private void BuildOptions()
    {
        foreach (EnumItemsSource<MainWindowCloseOption>.EnumNameValue<MainWindowCloseOption> item in MainWindowCloseOption.Items)
        {
            RadioButton radio = new()
            {
                GroupName = nameof(MainWindowCloseOption),
                Content = item.Name,
                IsChecked = item.Value == MainWindowCloseOption.Value,
                Tag = item,
            };
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked == true && radio.Tag is EnumItemsSource<MainWindowCloseOption>.EnumNameValue<MainWindowCloseOption> value)
                {
                    MainWindowCloseOption.SelectedItem = value;
                }
            };
            OptionsPanel.Children.Add(radio);
        }
    }

}
