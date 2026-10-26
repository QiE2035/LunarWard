namespace LunarWard.Features.Setting;

/// <summary>
/// 语言切换完成的通知消息（对应 Starward <c>Features/Setting/LanguageChangedMessage.cs</c>）。
/// </summary>
/// <remarks>
/// 上游用 <c>WeakReferenceMessenger</c> 发这条消息，<c>SettingPage</c> 收到后调 <c>Bindings.Update()</c>，
/// 因为 WinUI 的 <c>{x:Bind}</c> 不会自动跟随 <c>AppStrings</c> 变化。
/// LunarWard 的 XAML 用运行期 <c>{Binding Strings[...]}</c> 绑定：<see cref="LunarWard.Localization.AppStrings"/>
/// 在语言变化时已经抛出 <c>PropertyChanged("Item[]")</c>，绑定会自动刷新，因此这里保留消息类型
/// 只为与上游文件/类型对齐，页面不再需要手动 <c>Update()</c>。以后有需要手动刷新的页面可以注册它。
/// </remarks>
public sealed class LanguageChangedMessage
{

}
