namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// Holds the platform services for the core.
    /// The host program sets these once, when it starts.
    /// The defaults do nothing, so tests and command-line hosts need no setup.
    /// </summary>
    /// <remarks>
    /// This is a service locator, not dependency injection. The core uses static
    /// classes in many places, so a locator keeps the change small. Replace it with
    /// real injection later, one class at a time.
    /// </remarks>
    public static class CorePlatform {
        private static IDialogService dialogs = new NullDialogService();
        private static INotificationService notifications = new NullNotificationService();
        private static IAppPaths paths = new NullAppPaths();
        private static IEditorReaderService editorReader = new NullEditorReaderService();
        private static ICoreSettings settings = new DefaultCoreSettings();
        private static IFileDialogService fileDialogs = new NullFileDialogService();
        private static IShellService shell = new NullShellService();

        public static IDialogService Dialogs {
            get => dialogs;
            set => dialogs = value ?? new NullDialogService();
        }

        public static INotificationService Notifications {
            get => notifications;
            set => notifications = value ?? new NullNotificationService();
        }

        public static IAppPaths Paths {
            get => paths;
            set => paths = value ?? new NullAppPaths();
        }

        public static IEditorReaderService EditorReader {
            get => editorReader;
            set => editorReader = value ?? new NullEditorReaderService();
        }

        public static ICoreSettings Settings {
            get => settings;
            set => settings = value ?? new DefaultCoreSettings();
        }

        public static IFileDialogService FileDialogs {
            get => fileDialogs;
            set => fileDialogs = value ?? new NullFileDialogService();
        }

        public static IShellService Shell {
            get => shell;
            set => shell = value ?? new NullShellService();
        }

        /// <summary>
        /// Tells the core if the user holds a shift key now.
        /// The host with the user interface sets this.
        /// </summary>
        public static System.Func<bool> IsShiftDown { get; set; } = () => false;
    }

    /// <summary>Answers nothing and shows nothing.</summary>
    public class NullDialogService : IDialogService {
        public void ShowMessage(string message, string title = null) { }
        public bool AskYesNo(string message, string title = null) => false;
        public bool? AskYesNoCancel(string message, string title = null) => null;
        public bool ShowOkCancel(string message, string title = null) => false;
    }

    /// <summary>Shows nothing.</summary>
    public class NullNotificationService : INotificationService {
        public void Notify(string message) { }
    }

    /// <summary>Uses a folder under the local application data of the user.</summary>
    public class NullAppPaths : IAppPaths {
        public string AppDataPath => System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "Mapping Tools");
        public string ExportPath => System.IO.Path.Combine(AppDataPath, "Exports");
        public string AppCommon => System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.LocalApplicationData);
    }
}
