using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Mapping_Tools.Components {
    /// <summary>
    /// Puts a dialog over its own content, and waits for an answer.
    /// </summary>
    /// <remarks>
    /// This takes the place of <c>materialDesign:DialogHost</c>. The two calls that the
    /// views make are kept: <c>host.ShowDialog(content)</c> gives a task that ends with
    /// the answer, and a button inside the dialog closes it with
    /// <c>Command="{x:Static c:DialogHost.CloseDialogCommand}"</c> and the answer as
    /// its <c>CommandParameter</c>.
    /// <para>
    /// A close acts on the dialog that opened last. The views never open two at once,
    /// and a real modal dialog would not let them.
    /// </para>
    /// </remarks>
    [TemplatePart("PART_DialogOverlay", typeof(Control))]
    public class DialogHost : ContentControl {
        private static readonly Stack<DialogHost> OpenHosts = new();

        /// <summary>What the dialog shows.</summary>
        public static readonly StyledProperty<object> DialogContentProperty =
            AvaloniaProperty.Register<DialogHost, object>(nameof(DialogContent));

        /// <summary>True while a dialog is up.</summary>
        public static readonly StyledProperty<bool> IsOpenProperty =
            AvaloniaProperty.Register<DialogHost, bool>(nameof(IsOpen));

        public object DialogContent {
            get => GetValue(DialogContentProperty);
            set => SetValue(DialogContentProperty, value);
        }

        public bool IsOpen {
            get => GetValue(IsOpenProperty);
            private set => SetValue(IsOpenProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(DialogHost);

        /// <summary>
        /// The host of the main window. A control with no host of its own shows its
        /// dialogs here. The main window sets it.
        /// </summary>
        public static DialogHost Root { get; set; }

        private TaskCompletionSource<object> pending;

        /// <summary>
        /// Shows <paramref name="content"/>, and gives the answer that the dialog closes
        /// with. A second call while a dialog is up cancels the first with null.
        /// </summary>
        public Task<object> ShowDialog(object content) {
            pending?.TrySetResult(null);

            pending = new TaskCompletionSource<object>();
            DialogContent = content;
            IsOpen = true;
            OpenHosts.Push(this);
            return pending.Task;
        }

        /// <summary>Takes the dialog down, and gives <paramref name="result"/> as the answer.</summary>
        public void Close(object result) {
            if (!IsOpen) return;

            IsOpen = false;
            DialogContent = null;

            // The stack can hold this host deeper down if a close came out of order.
            if (OpenHosts.Count > 0 && ReferenceEquals(OpenHosts.Peek(), this)) {
                OpenHosts.Pop();
            }

            var completion = pending;
            pending = null;
            completion?.TrySetResult(result);
        }

        /// <summary>Shows a dialog on the host of the main window.</summary>
        public static Task<object> Show(object content) {
            if (Root is null) {
                throw new InvalidOperationException(
                    "No DialogHost is set as the root. The main window must set DialogHost.Root.");
            }
            return Root.ShowDialog(content);
        }

        /// <summary>
        /// Closes the dialog that opened last. The command parameter becomes the answer.
        /// </summary>
        public static ICommand CloseDialogCommand { get; } = new CloseCommand();

        /// <summary>Shows the command parameter as a dialog, on the root host.</summary>
        public static ICommand OpenDialogCommand { get; } = new OpenCommand();

        private sealed class CloseCommand : ICommand {
            public event EventHandler CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object parameter) => OpenHosts.Count > 0;

            public void Execute(object parameter) {
                if (OpenHosts.Count == 0) return;
                OpenHosts.Peek().Close(parameter);
            }
        }

        private sealed class OpenCommand : ICommand {
            public event EventHandler CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object parameter) => Root is not null;

            public void Execute(object parameter) {
                if (Root is null) return;
                _ = Root.ShowDialog(parameter);
            }
        }
    }
}
