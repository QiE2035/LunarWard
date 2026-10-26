using System.Runtime.InteropServices;

namespace LunarWard.Frameworks.WebView.Interop;

/// <summary>
/// 原生库 soname。Linux 上运行时库的 soname 稳定，dev 包（.so 软链）只在编译
/// 其它原生代码时需要，所以这里直接按 soname 加载，不依赖 *-dev 包。
/// </summary>
internal static class Sonames
{
    public const string Glib = "libglib-2.0.so.0";
    public const string GObject = "libgobject-2.0.so.0";
    public const string Gtk = "libgtk-3.so.0";
    public const string Gdk = "libgdk-3.so.0";
    public const string WebKit = "libwebkit2gtk-4.1.so.0";
    public const string Jsc = "libjavascriptcoregtk-4.1.so.0";
    public const string Soup = "libsoup-3.0.so.0";
    public const string Cairo = "libcairo.so.2";

    public static readonly string[] Required = [WebKit, Gtk, Gdk, GObject, Glib];
    public static readonly string[] Optional = [Soup, Jsc, Cairo];

    /// <summary>缺库时给用户的安装提示（Arch / Debian / Fedora）。</summary>
    public const string InstallHint =
        "安装运行时依赖：\n" +
        "  Arch:    sudo pacman -S webkit2gtk-4.1 gtk3 libsoup3\n" +
        "  Debian:  sudo apt install libwebkit2gtk-4.1-0 libgtk-3-0 libsoup-3.0-0\n" +
        "  Fedora:  sudo dnf install webkit2gtk4.1 gtk3 libsoup3\n" +
        "编译/开发头文件（只有自己写原生代码才需要）：\n" +
        "  Arch:    webkit2gtk-4.1（含头文件）\n" +
        "  Debian:  libwebkit2gtk-4.1-dev";
}

internal enum GtkWindowType
{
    Toplevel = 0,
    Popup = 1,
}

/// <summary>WebKitLoadEvent（WebKitWebView.h）。</summary>
internal enum WebKitLoadEvent
{
    Started = 0,
    Redirected = 1,
    Committed = 2,
    Finished = 3,
}

/// <summary>WebKitSnapshotRegion（WebKitWebView.h）。</summary>
internal enum WebKitSnapshotRegion
{
    Visible = 0,
    FullDocument = 1,
}

[Flags]
internal enum WebKitSnapshotOptions
{
    None = 0,
    IncludeSelectionHighlighting = 1 << 0,
    TransparentBackground = 1 << 1,
}

/// <summary>WebKitUserContentInjectedFrames（WebKitUserContent.h）。</summary>
internal enum WebKitUserContentInjectedFrames
{
    AllFrames = 0,
    TopFrame = 1,
}

/// <summary>WebKitUserScriptInjectionTime（WebKitUserContent.h）。</summary>
internal enum WebKitUserScriptInjectionTime
{
    DocumentStart = 0,
    DocumentEnd = 1,
}

/// <summary>WebKitWebProcessTerminationReason（WebKitWebView.h）。</summary>
internal enum WebKitWebProcessTerminationReason
{
    Crashed = 0,
    ExceededMemoryLimit = 1,
    TerminatedByApi = 2,
}

/// <summary>WebKitPolicyDecisionType（WebKitWebView.h）。</summary>
internal enum WebKitPolicyDecisionType
{
    NavigationAction = 0,
    NewWindowAction = 1,
    Response = 2,
}

/// <summary>SoupSameSitePolicy（libsoup-3.0 soup-cookie.h）。</summary>
internal enum SoupSameSitePolicy
{
    None = 0,
    Lax = 1,
    Strict = 2,
}

[StructLayout(LayoutKind.Sequential)]
internal struct GdkRGBA
{
    public double Red;
    public double Green;
    public double Blue;
    public double Alpha;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int GSourceFunc(IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void GAsyncReadyCallback(IntPtr sourceObject, IntPtr res, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void WebKitURISchemeRequestCallback(IntPtr request, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void GDestroyNotify(IntPtr data);

/// <summary>
/// GLib / GObject / GTK3 / GDK3 / WebKitGTK 4.1 / libsoup3 / JSC / cairo 的 P/Invoke 声明。
/// 函数名与头文件一一对应：字段名即 C 符号去掉库前缀，方便对照
/// /usr/include/webkitgtk-4.1/webkit/*.h 与 /usr/include/gtk-3.0。
/// </summary>
internal static class Glib
{
    public const int GSourceRemove = 0;
    public const int ConnectAfter = 1 << 1;

    [DllImport(Sonames.Glib)]
    public static extern uint g_idle_add(GSourceFunc function, IntPtr data);

    [DllImport(Sonames.Glib)]
    public static extern bool g_source_remove(uint tag);

    [DllImport(Sonames.Glib)]
    public static extern void g_free(IntPtr mem);

    [DllImport(Sonames.Glib)]
    public static extern void g_error_free(IntPtr error);

    [DllImport(Sonames.Glib)]
    public static extern IntPtr g_list_next(IntPtr list);

    [DllImport(Sonames.Glib)]
    public static extern IntPtr g_list_nth_data(IntPtr list, uint n);

    [DllImport(Sonames.Glib)]
    public static extern void g_list_free(IntPtr list);

    [DllImport(Sonames.Glib)]
    public static extern void g_list_free_full(IntPtr list, IntPtr freeFunc);

    // --- GObject ---

    [DllImport(Sonames.GObject)]
    public static extern void g_object_unref(IntPtr obj);

    [DllImport(Sonames.GObject)]
    public static extern IntPtr g_object_ref_sink(IntPtr obj);

    [DllImport(Sonames.GObject)]
    public static extern IntPtr g_object_ref(IntPtr obj);

    [DllImport(Sonames.GObject)]
    public static extern ulong g_signal_connect_data(
        IntPtr instance,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string detailedSignal,
        IntPtr handler,
        IntPtr data,
        IntPtr destroyData,
        int connectFlags);

    [DllImport(Sonames.GObject)]
    public static extern void g_signal_handler_disconnect(IntPtr instance, ulong handlerId);

    // --- GValue（用于 g_object_set_property，避免 P/Invoke 变参函数）---

    [DllImport(Sonames.GObject)]
    public static extern void g_value_init(IntPtr value, nuint gtype);

    [DllImport(Sonames.GObject)]
    public static extern void g_value_unset(IntPtr value);

    [DllImport(Sonames.GObject)]
    public static extern void g_value_set_boolean(IntPtr value, bool v);

    [DllImport(Sonames.GObject)]
    public static extern void g_object_set_property(
        IntPtr obj,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr value);

    // --- GType ---

    [DllImport(Sonames.GObject)]
    public static extern nuint g_type_from_name([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    /// <summary>G_TYPE_BOOLEAN 是基本类型，固定值 5（gtype.h），免去一次类型系统查询。</summary>
    public const nuint GTypeBoolean = 20; // G_TYPE_BOOLEAN = 5 << 2
}

internal static class Gtk
{
    [DllImport(Sonames.Gtk)]
    public static extern bool gtk_init_check(ref int argc, ref IntPtr argv);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_main();

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_main_quit();

    [DllImport(Sonames.Gtk)]
    public static extern uint gtk_get_major_version();

    [DllImport(Sonames.Gtk)]
    public static extern uint gtk_get_minor_version();

    [DllImport(Sonames.Gtk)]
    public static extern uint gtk_get_micro_version();

    // --- 容器/窗口 ---

    [DllImport(Sonames.Gtk)]
    public static extern IntPtr gtk_plug_new(ulong socketId);

    [DllImport(Sonames.Gtk)]
    public static extern ulong gtk_plug_get_id(IntPtr plug);

    [DllImport(Sonames.Gtk)]
    public static extern bool gtk_plug_get_embedded(IntPtr plug);

    [DllImport(Sonames.Gtk)]
    public static extern IntPtr gtk_window_new(GtkWindowType type);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_window_set_title(
        IntPtr window, [MarshalAs(UnmanagedType.LPUTF8Str)] string title);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_window_set_default_size(IntPtr window, int width, int height);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_window_present(IntPtr window);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_window_resize(IntPtr window, int width, int height);

    [DllImport(Sonames.Gtk)]
    public static extern IntPtr gtk_settings_get_default();

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_container_add(IntPtr container, IntPtr widget);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_widget_show_all(IntPtr widget);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_widget_show(IntPtr widget);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_widget_realize(IntPtr widget);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_widget_destroy(IntPtr widget);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_widget_grab_focus(IntPtr widget);

    /// <summary>gtk_widget_size_allocate(widget, GtkAllocation*)；GtkAllocation = GdkRectangle = 4 个 int。</summary>
    [DllImport(Sonames.Gtk)]
    public static extern void gtk_widget_size_allocate(IntPtr widget, IntPtr allocation);

    [DllImport(Sonames.Gtk)]
    public static extern int gtk_widget_get_allocated_width(IntPtr widget);

    [DllImport(Sonames.Gtk)]
    public static extern int gtk_widget_get_allocated_height(IntPtr widget);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_widget_set_size_request(IntPtr widget, int width, int height);

    [DllImport(Sonames.Gtk)]
    public static extern void gtk_widget_queue_resize(IntPtr widget);

    [DllImport(Sonames.Gtk)]
    public static extern IntPtr gtk_widget_get_window(IntPtr widget);
}

internal static class Gdk
{
    [DllImport(Sonames.Gdk)]
    public static extern IntPtr gdk_display_get_default();

    [DllImport(Sonames.Gdk)]
    public static extern IntPtr gdk_display_get_name(IntPtr display);

    [DllImport(Sonames.Gdk)]
    public static extern void gdk_window_move_resize(IntPtr window, int x, int y, int width, int height);

    [DllImport(Sonames.Gdk)]
    public static extern ulong gdk_x11_window_get_xid(IntPtr window);

    [DllImport(Sonames.Gdk)]
    public static extern void gdk_window_focus(IntPtr window, uint timestamp);
}

internal static class WebKit
{
    [DllImport(Sonames.WebKit)]
    public static extern uint webkit_get_major_version();

    [DllImport(Sonames.WebKit)]
    public static extern uint webkit_get_minor_version();

    [DllImport(Sonames.WebKit)]
    public static extern uint webkit_get_micro_version();

    // --- WebView 构造 ---

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_view_new_with_user_content_manager(IntPtr userContentManager);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_view_get_user_content_manager(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_view_get_context(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_view_get_settings(IntPtr webView);

    // --- 导航 ---

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_load_uri(
        IntPtr webView, [MarshalAs(UnmanagedType.LPUTF8Str)] string uri);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_load_html(
        IntPtr webView,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string content,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? baseUri);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_reload(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_stop_loading(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern bool webkit_web_view_is_loading(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern bool webkit_web_view_can_go_back(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern bool webkit_web_view_can_go_forward(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_go_back(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_go_forward(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_view_get_uri(IntPtr webView);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_view_get_title(IntPtr webView);

    // --- 外观 ---

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_set_background_color(IntPtr webView, ref GdkRGBA rgba);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_set_zoom_level(IntPtr webView, double zoomLevel);

    [DllImport(Sonames.WebKit)]
    public static extern double webkit_web_view_get_zoom_level(IntPtr webView);

    // --- JS ---

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_evaluate_javascript(
        IntPtr webView,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string script,
        nint length,
        IntPtr worldName,
        IntPtr sourceUri,
        IntPtr cancellable,
        GAsyncReadyCallback callback,
        IntPtr userData);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_view_evaluate_javascript_finish(
        IntPtr webView, IntPtr res, out IntPtr error);

    // --- 截图（等价 WebView2 的 Page.captureScreenshot / CapturePreviewAsync）---

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_view_get_snapshot(
        IntPtr webView,
        WebKitSnapshotRegion region,
        WebKitSnapshotOptions options,
        IntPtr cancellable,
        GAsyncReadyCallback callback,
        IntPtr userData);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_view_get_snapshot_finish(IntPtr webView, IntPtr res, out IntPtr error);

    // --- 设置 ---

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_settings_set_user_agent(
        IntPtr settings, [MarshalAs(UnmanagedType.LPUTF8Str)] string? userAgent);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_settings_get_user_agent(IntPtr settings);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_settings_set_enable_developer_extras(IntPtr settings, bool enabled);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_settings_set_enable_webgl(IntPtr settings, bool enabled);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_settings_set_enable_webaudio(IntPtr settings, bool enabled);

    // --- 用户脚本 / 消息 ---

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_user_content_manager_new();

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_user_content_manager_add_script(IntPtr manager, IntPtr script);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_user_content_manager_register_script_message_handler(
        IntPtr manager, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_user_content_manager_unregister_script_message_handler(
        IntPtr manager, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_user_script_new(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        WebKitUserContentInjectedFrames injectedFrames,
        WebKitUserScriptInjectionTime injectionTime,
        IntPtr allowList,
        IntPtr blockList);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_user_script_unref(IntPtr script);

    // --- 自定义 URI scheme（虚拟主机映射）---

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_context_register_uri_scheme(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string scheme,
        WebKitURISchemeRequestCallback callback,
        IntPtr userData,
        IntPtr destroyNotify);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_context_get_security_manager(IntPtr context);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_security_manager_register_uri_scheme_as_cors_enabled(
        IntPtr manager, [MarshalAs(UnmanagedType.LPUTF8Str)] string scheme);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_security_manager_register_uri_scheme_as_secure(
        IntPtr manager, [MarshalAs(UnmanagedType.LPUTF8Str)] string scheme);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_uri_scheme_request_get_uri(IntPtr request);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_uri_scheme_request_get_path(IntPtr request);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_uri_scheme_request_finish(
        IntPtr request,
        IntPtr stream,
        long streamLength,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string contentType);

    // --- 资源（WebResourceResponseReceived 等价）---

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_resource_get_uri(IntPtr resource);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_resource_get_response(IntPtr resource);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_web_resource_get_data(
        IntPtr resource, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_resource_get_data_finish(
        IntPtr resource, IntPtr res, out nuint length, out IntPtr error);

    [DllImport(Sonames.WebKit)]
    public static extern int webkit_uri_response_get_status_code(IntPtr response);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_uri_response_get_mime_type(IntPtr response);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_uri_request_get_uri(IntPtr request);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_navigation_action_get_request(IntPtr action);

    // --- Cookie ---

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_web_context_get_cookie_manager(IntPtr context);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_cookie_manager_get_cookies(
        IntPtr cookieManager,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uri,
        IntPtr cancellable,
        GAsyncReadyCallback callback,
        IntPtr userData);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_cookie_manager_get_cookies_finish(IntPtr cookieManager, IntPtr res, out IntPtr error);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_cookie_manager_get_all_cookies(
        IntPtr cookieManager, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_cookie_manager_get_all_cookies_finish(IntPtr cookieManager, IntPtr res, out IntPtr error);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_cookie_manager_add_cookie(
        IntPtr cookieManager, IntPtr cookie, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(Sonames.WebKit)]
    public static extern bool webkit_cookie_manager_add_cookie_finish(IntPtr cookieManager, IntPtr res, out IntPtr error);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_cookie_manager_delete_cookie(
        IntPtr cookieManager, IntPtr cookie, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

    [DllImport(Sonames.WebKit)]
    public static extern bool webkit_cookie_manager_delete_cookie_finish(IntPtr cookieManager, IntPtr res, out IntPtr error);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_cookie_manager_delete_cookies_for_domain(
        IntPtr cookieManager, [MarshalAs(UnmanagedType.LPUTF8Str)] string domain);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_cookie_manager_delete_all_cookies(IntPtr cookieManager);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_cookie_manager_set_persistent_storage(
        IntPtr cookieManager,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filename,
        int storage);

    // --- 消息结果 ---

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_javascript_result_get_js_value(IntPtr jsResult);

    [DllImport(Sonames.WebKit)]
    public static extern IntPtr webkit_javascript_result_ref(IntPtr jsResult);

    [DllImport(Sonames.WebKit)]
    public static extern void webkit_javascript_result_unref(IntPtr jsResult);
}

internal static class Soup
{
    [DllImport(Sonames.Soup)]
    public static extern IntPtr soup_cookie_new(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string domain,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int maxAge);

    [DllImport(Sonames.Soup)]
    public static extern IntPtr soup_cookie_get_name(IntPtr cookie);

    [DllImport(Sonames.Soup)]
    public static extern IntPtr soup_cookie_get_value(IntPtr cookie);

    [DllImport(Sonames.Soup)]
    public static extern IntPtr soup_cookie_get_domain(IntPtr cookie);

    [DllImport(Sonames.Soup)]
    public static extern IntPtr soup_cookie_get_path(IntPtr cookie);

    [DllImport(Sonames.Soup)]
    public static extern bool soup_cookie_get_secure(IntPtr cookie);

    [DllImport(Sonames.Soup)]
    public static extern bool soup_cookie_get_http_only(IntPtr cookie);

    [DllImport(Sonames.Soup)]
    public static extern void soup_cookie_set_secure(IntPtr cookie, bool secure);

    [DllImport(Sonames.Soup)]
    public static extern void soup_cookie_set_http_only(IntPtr cookie, bool httpOnly);

    [DllImport(Sonames.Soup)]
    public static extern void soup_cookie_set_same_site_policy(IntPtr cookie, SoupSameSitePolicy policy);

    [DllImport(Sonames.Soup)]
    public static extern void soup_cookie_free(IntPtr cookie);
}

internal static class Jsc
{
    [DllImport(Sonames.Jsc)]
    public static extern IntPtr jsc_value_to_string(IntPtr value);

    [DllImport(Sonames.Jsc)]
    public static extern IntPtr jsc_value_to_json(IntPtr value, uint indent);

    [DllImport(Sonames.Jsc)]
    public static extern bool jsc_value_is_string(IntPtr value);

    [DllImport(Sonames.Jsc)]
    public static extern bool jsc_value_is_undefined(IntPtr value);

    [DllImport(Sonames.Jsc)]
    public static extern bool jsc_value_is_null(IntPtr value);

    [DllImport(Sonames.Jsc)]
    public static extern bool jsc_value_is_number(IntPtr value);

    [DllImport(Sonames.Jsc)]
    public static extern double jsc_value_to_double(IntPtr value);
}

internal static class Cairo
{
    public const int StatusSuccess = 0;

    [DllImport(Sonames.Cairo)]
    public static extern int cairo_surface_write_to_png(
        IntPtr surface, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

    [DllImport(Sonames.Cairo)]
    public static extern void cairo_surface_destroy(IntPtr surface);

    [DllImport(Sonames.Cairo)]
    public static extern int cairo_image_surface_get_width(IntPtr surface);

    [DllImport(Sonames.Cairo)]
    public static extern int cairo_image_surface_get_height(IntPtr surface);
}

internal static class Gio
{
    public const string Lib = "libgio-2.0.so.0";

    [DllImport(Lib)]
    public static extern IntPtr g_memory_input_stream_new_from_data(IntPtr data, nint length, IntPtr destroy);

    [DllImport(Lib)]
    public static extern IntPtr g_bytes_new(IntPtr data, nuint size);

    [DllImport(Lib)]
    public static extern IntPtr g_memory_input_stream_new_from_bytes(IntPtr bytes);

    [DllImport(Lib)]
    public static extern void g_bytes_unref(IntPtr bytes);
}

/// <summary>
/// libX11 的最小封装：Avalonia 的 NativeControlHost 在 X11 上需要我们自己建一个子窗口，
/// 再把这个窗口的 XID 交给 <c>gtk_plug_new</c> 作为 socket。
/// </summary>
internal static class X11
{
    public const string Lib = "libX11.so.6";

    public const int InputOutput = 1;
    public const int IsViewable = 2;

    [DllImport(Lib)]
    public static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport(Lib)]
    public static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(Lib)]
    public static extern int XCloseDisplay(IntPtr display);

    [DllImport(Lib)]
    public static extern IntPtr XCreateWindow(
        IntPtr display,
        IntPtr parent,
        int x,
        int y,
        uint width,
        uint height,
        uint borderWidth,
        int depth,
        uint windowClass,
        IntPtr visual,
        ulong valueMask,
        IntPtr attributes);

    [DllImport(Lib)]
    public static extern int XDestroyWindow(IntPtr display, IntPtr window);

    [DllImport(Lib)]
    public static extern int XMapWindow(IntPtr display, IntPtr window);

    [DllImport(Lib)]
    public static extern int XUnmapWindow(IntPtr display, IntPtr window);

    [DllImport(Lib)]
    public static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

    [DllImport(Lib)]
    public static extern int XResizeWindow(IntPtr display, IntPtr window, uint width, uint height);

    [DllImport(Lib)]
    public static extern int XFlush(IntPtr display);

    [DllImport(Lib)]
    public static extern int XSync(IntPtr display, int discard);
}

/// <summary>
/// libc 的环境变量端口。
/// .NET 的 Environment.SetEnvironmentVariable 只保证托管侧可见，GTK/GDK 这种在
/// 原生库里用 g_getenv 读变量的代码必须走真正的 setenv。
/// </summary>
internal static class Libc
{
    [DllImport("libc", CharSet = CharSet.Ansi)]
    public static extern int setenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int overwrite);

    [DllImport("libc", CharSet = CharSet.Ansi)]
    public static extern IntPtr getenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    /// <summary>同时写托管侧与原生 environ 的环境变量。</summary>
    public static void Set(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        setenv(name, value, 1);
    }

    /// <summary>读原生 environ（用于确认 GDK 看到的值）。</summary>
    public static string? GetNative(string name)
    {
        IntPtr ptr = getenv(name);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}
