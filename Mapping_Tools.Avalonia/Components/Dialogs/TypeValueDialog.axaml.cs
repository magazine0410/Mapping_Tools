using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mapping_Tools.Components.Dialogs {
    /// <summary>Asks the user for one number.</summary>
    /// <remarks>
    /// <c>ValueBox</c> is declared by the Avalonia name generator, from the x:Name in
    /// the XAML. The caller reads its text after the dialog closes.
    /// </remarks>
    public partial class TypeValueDialog : UserControl {
        public TypeValueDialog() : this(0) { }

        public TypeValueDialog(double initialValue) {
            InitializeComponent();
            ValueBox.Text = initialValue.ToString(CultureInfo.InvariantCulture);
        }

        protected override void OnLoaded(RoutedEventArgs e) {
            base.OnLoaded(e);
            ValueBox.Focus();
            ValueBox.SelectAll();
        }
    }
}
