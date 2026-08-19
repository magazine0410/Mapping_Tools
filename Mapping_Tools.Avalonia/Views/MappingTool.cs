using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace Mapping_Tools.Avalonia.Views {
    /// <summary>
    /// The base of every tool view.
    /// </summary>
    /// <remarks>
    /// This mirrors the WPF <c>MappingTool</c>. <see cref="ViewCollection"/> finds tools
    /// by looking for classes that extend this one, so the shell needs no list of tools.
    /// The WPF version used a DependencyProperty for <see cref="IsActive"/>. Avalonia
    /// uses a StyledProperty instead.
    /// </remarks>
    [HiddenTool]
    public class MappingTool : UserControl, INotifyPropertyChanged {
        public new event PropertyChangedEventHandler PropertyChanged;

        public static readonly StyledProperty<bool> IsActiveProperty =
            AvaloniaProperty.Register<MappingTool, bool>(nameof(IsActive), defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

        public bool IsActive {
            get => GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public virtual void Activate() {
            IsActive = true;
        }

        public virtual void Deactivate() {
            IsActive = false;
        }

        protected void Set<T>(ref T target, T value, [CallerMemberName] string propertyName = "") {
            target = value;
            RaisePropertyChanged(propertyName);
        }

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void Dispose() {
            IsActive = false;
        }
    }
}
