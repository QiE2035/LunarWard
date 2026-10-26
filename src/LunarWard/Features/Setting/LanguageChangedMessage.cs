namespace LunarWard.Features.Setting;

/// <summary>
/// 语言切换完成的通知消息（对应 Starward <c>Features/Setting/LanguageChangedMessage.cs</c>）。
/// </summary>
/// <remarks>
/// 上游用 <c>WeakReferenceMessenger</c> 发这条消息，<c>SettingPage</c> 收到后调 <c>Bindings.Update()</c>，
/// 因为 WinUI 的 <c>{x:Bind}</c> 不会自动跟随资源语言变化。
/// LunarWard 的设置页已改用 <c>{x:Static lang:Lang.Xxx}</c>，静态绑定同样不会自己刷新；
/// <c>GeneralSetting</c> 切换语言时仍发这条消息，<c>SettingPage</c> 收到后重建子页。
/// </remarks>
public sealed class LanguageChangedMessage
{

}
