using Avalonia.Controls;
using Avalonia.Input;

namespace LunarWard.Helpers;

/// <summary>
/// 上游 Helpers/PointerCursor.cs：WinUI 的附加属性 <c>PointerCursor.CursorShape</c>。
/// Avalonia 没有附加光标属性，这里给出等价的静态辅助方法，XAML 里改用
/// <c>Cursor="{x:Static helpers:PointerCursor.Hand}"</c> 之类的静态值。
/// </summary>
public static class PointerCursor
{


    public static Cursor Hand { get; } = new(StandardCursorType.Hand);


    public static Cursor Arrow { get; } = new(StandardCursorType.Arrow);


    public static Cursor SizeNorthSouth { get; } = new(StandardCursorType.SizeNorthSouth);


    public static void SetCursorShape(Control control, StandardCursorType shape)
    {
        control.Cursor = shape is StandardCursorType.Arrow or StandardCursorType.None ? null : new Cursor(shape);
    }
}
