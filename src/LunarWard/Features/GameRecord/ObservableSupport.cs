// Linux: 上游这些页面用 CommunityToolkit.Mvvm 的 [ObservableProperty] 生成 INPC。
// AvaloniaObject 已用 Avalonia 的 PropertyChanged（AvaloniaPropertyChangedEventArgs）实现
// INotifyPropertyChanged，生成器不会为派生类再造 On<Prop>Changed 分部方法（CS0759/MVVMTK0001）。
// 这里为每个类重新实现 INotifyPropertyChanged（接口再实现，Avalonia 绑定会走新事件），
// 并提供上游 code-behind 用到的 SetProperty/OnPropertyChanged 形状。


namespace LunarWard.Features.GameRecord.DailyNote
{
    public sealed partial class DailyNoteButton : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

}


namespace LunarWard.Features.GameRecord.Genshin
{
    public sealed partial class ImaginariumTheaterPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class SpiralAbyssPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class StygianOnslaughtPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class TravelersDiaryPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

}


namespace LunarWard.Features.GameRecord.StarRail
{
    public sealed partial class ApocalypticShadowPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class ChallengePeakPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class ForgottenHallPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class PureFictionPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class SimulatedUniversePage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class TrailblazeCalendarPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

}


namespace LunarWard.Features.GameRecord.ZZZ
{
    public sealed partial class DeadlyAssaultPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class InterKnotMonthlyReportPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class ShiyuDefensePage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

}


namespace LunarWard.Features.GameRecord
{
    public sealed partial class BBSWebBridge : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class BattleChronicleWindow : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

    public sealed partial class GameRecordPage : global::System.ComponentModel.INotifyPropertyChanged
    {
        public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string? propertyName)
            => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) => RaisePropertyChanged(propertyName);
    }

}
