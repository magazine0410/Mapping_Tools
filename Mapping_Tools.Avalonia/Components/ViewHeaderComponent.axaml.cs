using System;
using Avalonia;
using Avalonia.Controls;
using Mapping_Tools.Avalonia.Views;

namespace Mapping_Tools.Components {
    /// <summary>
    /// The title of a tool, and the bubble that holds its description.
    /// </summary>
    /// <remarks>
    /// The WPF header also shows a Quick Run bubble. Quick Run reads the memory of the
    /// running osu!, which the Linux host cannot do, so that bubble is left out here.
    /// See section 3.2 of LINUX_PORT.md.
    /// </remarks>
    public partial class ViewHeaderComponent : UserControl {
        /// <summary>The tool view that this header belongs to.</summary>
        public static readonly StyledProperty<Control> ParentControlProperty =
            AvaloniaProperty.Register<ViewHeaderComponent, Control>(nameof(ParentControl));

        public Control ParentControl {
            get => GetValue(ParentControlProperty);
            set => SetValue(ParentControlProperty, value);
        }

        public ViewHeaderComponent() {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property != ParentControlProperty || MainPanel is null) return;

            var type = change.GetNewValue<Control>()?.GetType();

            if (type is null || Attribute.IsDefined(type, typeof(DontShowTitleAttribute))) {
                MainPanel.IsVisible = false;
                return;
            }

            MainPanel.IsVisible = true;

            var description = ViewCollection.GetDescription(type);
            HeaderTextBlock.Text = ViewCollection.GetName(type);
            DescriptionTextBlock.Text = description;
            DescriptionIcon.IsVisible = !string.IsNullOrEmpty(description);
        }
    }
}
