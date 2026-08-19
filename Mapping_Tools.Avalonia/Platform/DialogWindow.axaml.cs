using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mapping_Tools.Avalonia.Platform {
    /// <summary>
    /// A plain message window. Avalonia has no MessageBox, so this replaces it.
    /// </summary>
    public partial class DialogWindow : Window {
        /// <summary>The text of the button that the user pressed, or null.</summary>
        public string Result { get; private set; }

        public DialogWindow() {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        /// <summary>
        /// Fills the window with a message and one button for each answer.
        /// </summary>
        public void Setup(string message, string title, IReadOnlyList<string> answers) {
            Title = string.IsNullOrEmpty(title) ? "Mapping Tools" : title;
            this.FindControl<SelectableTextBlock>("MessageText")!.Text = message;

            var panel = this.FindControl<StackPanel>("ButtonPanel")!;
            for (var i = 0; i < answers.Count; i++) {
                var answer = answers[i];
                var button = new Button { Content = answer, MinWidth = 88, IsDefault = i == 0 };
                button.Click += (_, _) => {
                    Result = answer;
                    Close();
                };
                panel.Children.Add(button);
            }
        }
    }
}
