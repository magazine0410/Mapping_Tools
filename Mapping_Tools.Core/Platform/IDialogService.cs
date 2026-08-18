namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// Asks the user a question, or tells the user something.
    /// The core has no user interface, so the host program supplies this.
    /// </summary>
    public interface IDialogService {
        /// <summary>
        /// Shows a message. There is only one button.
        /// </summary>
        void ShowMessage(string message, string title = null);

        /// <summary>
        /// Asks a question. The user can answer yes or no.
        /// </summary>
        bool AskYesNo(string message, string title = null);

        /// <summary>
        /// Asks a question. The user can answer yes, no, or cancel.
        /// Returns null if the user cancels.
        /// </summary>
        bool? AskYesNoCancel(string message, string title = null);

        /// <summary>
        /// Shows a message. The user can accept or cancel.
        /// Returns true if the user accepts.
        /// </summary>
        bool ShowOkCancel(string message, string title = null);
    }
}
