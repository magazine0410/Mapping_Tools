using Avalonia.Controls;

namespace Mapping_Tools.Components.Dialogs {
    /// <summary>Shows one message, and asks the user to accept it.</summary>
    public partial class MessageDialog : UserControl {
        public MessageDialog() : this(string.Empty) { }

        public MessageDialog(string message) {
            InitializeComponent();
            MessageTextBlock.Text = message;
        }
    }
}
