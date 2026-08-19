using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Avalonia.Platform {
    /// <summary>
    /// Gives the portable core the services of this host.
    /// Call <see cref="Register"/> once, when the program starts.
    /// </summary>
    public static class AvaloniaPlatformServices {
        public static void Register() {
            CorePlatform.Dialogs = new AvaloniaDialogService();
            CorePlatform.Notifications = new AvaloniaNotificationService();
            CorePlatform.Paths = new AvaloniaAppPaths();
            CorePlatform.FileDialogs = new AvaloniaFileDialogService();
            CorePlatform.Shell = new DesktopShellService();
            // The editor reader needs the memory of the osu! process. There is no
            // implementation for Linux, so the core reads the file on disk instead.
            CorePlatform.EditorReader = new NullEditorReaderService();
        }

        /// <summary>The main window, once it exists.</summary>
        internal static Window MainWindow =>
            global::Avalonia.Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
    }

    /// <summary>
    /// Shows a message window and waits for the answer.
    /// </summary>
    /// <remarks>
    /// The core asks these questions from inside tool code that runs on a worker
    /// thread, so the contract is synchronous. Avalonia dialogs are asynchronous and
    /// must open on the user interface thread. This class bridges the two:
    /// a worker thread blocks while the window runs on the interface thread, and a call
    /// that is already on the interface thread runs a nested dispatcher loop instead.
    /// </remarks>
    public class AvaloniaDialogService : IDialogService {
        private const string Ok = "OK";
        private const string Cancel = "Cancel";
        private const string Yes = "Yes";
        private const string No = "No";

        public void ShowMessage(string message, string title = null) =>
            Ask(message, title, new[] { Ok });

        public bool AskYesNo(string message, string title = null) =>
            Ask(message, title, new[] { Yes, No }) == Yes;

        public bool? AskYesNoCancel(string message, string title = null) {
            var answer = Ask(message, title, new[] { Yes, No, Cancel });
            if (answer == Cancel || answer is null) return null;
            return answer == Yes;
        }

        public bool ShowOkCancel(string message, string title = null) =>
            Ask(message, title, new[] { Ok, Cancel }) == Ok;

        private static string Ask(string message, string title, IReadOnlyList<string> answers) {
            if (Dispatcher.UIThread.CheckAccess()) {
                // Already on the interface thread. Blocking here would stop the window
                // from drawing, so run a nested loop until the dialog closes.
                string result = null;
                var frame = new DispatcherFrame();
                _ = ShowAsync(message, title, answers).ContinueWith(t => {
                    result = t.IsCompletedSuccessfully ? t.Result : null;
                    frame.Continue = false;
                }, TaskScheduler.FromCurrentSynchronizationContext());
                Dispatcher.UIThread.PushFrame(frame);
                return result;
            }

            // On a worker thread. Blocking is safe and is what the caller expects.
            return Dispatcher.UIThread.InvokeAsync(() => ShowAsync(message, title, answers))
                .GetAwaiter().GetResult();
        }

        private static async Task<string> ShowAsync(string message, string title,
            IReadOnlyList<string> answers) {
            var dialog = new DialogWindow();
            dialog.Setup(message, title, answers);

            var owner = AvaloniaPlatformServices.MainWindow;
            if (owner is not null) {
                await dialog.ShowDialog(owner);
            } else {
                // No main window yet, during start up or shut down.
                var closed = new TaskCompletionSource();
                dialog.Closed += (_, _) => closed.TrySetResult();
                dialog.Show();
                await closed.Task;
            }

            return dialog.Result;
        }
    }

    /// <summary>
    /// Shows a short message that does not stop the user.
    /// </summary>
    public class AvaloniaNotificationService : INotificationService {
        /// <summary>The shell sets this, so notifications reach the snackbar.</summary>
        public static Action<string> Sink { get; set; }

        public void Notify(string message) {
            var sink = Sink;
            if (sink is null) return;
            Dispatcher.UIThread.Post(() => sink(message));
        }
    }

    /// <summary>
    /// Uses the folders that the desktop of the user expects.
    /// </summary>
    public class AvaloniaAppPaths : IAppPaths {
        public string AppCommon =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public string AppDataPath => Path.Combine(AppCommon, "Mapping Tools");

        public string ExportPath => Path.Combine(AppDataPath, "Exports");
    }

    /// <summary>
    /// Opens a folder in the file manager of the user.
    /// </summary>
    public class DesktopShellService : IShellService {
        public void OpenFolder(string path) {
            if (string.IsNullOrEmpty(path)) return;
            try {
                var opener = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "explorer"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open"
                    : "xdg-open";
                Process.Start(new ProcessStartInfo(opener, path) { UseShellExecute = false });
            } catch (Exception e) {
                Debug.WriteLine($"Could not open the folder \"{path}\": {e.Message}");
            }
        }
    }

    /// <summary>
    /// Asks the user to choose a file, through the storage service of Avalonia.
    /// </summary>
    public class AvaloniaFileDialogService : IFileDialogService {
        private static readonly FilePickerFileType JsonProject = new("Mapping Tools project") {
            Patterns = new[] { "*.json" }
        };

        public string LoadProjectDialog(string initialDirectory = null) =>
            RunOnUiThread(async provider => {
                var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions {
                    Title = "Open a project",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { JsonProject },
                    SuggestedStartLocation = await FolderOrNull(provider, initialDirectory)
                });
                return files.Count > 0 ? files[0].TryGetLocalPath() : null;
            });

        public string SaveProjectDialog(string initialDirectory = null) =>
            RunOnUiThread(async provider => {
                var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions {
                    Title = "Save the project",
                    DefaultExtension = "json",
                    FileTypeChoices = new[] { JsonProject },
                    SuggestedStartLocation = await FolderOrNull(provider, initialDirectory)
                });
                return file?.TryGetLocalPath();
            });

        private static readonly FilePickerFileType OsuFiles = new("osu! files") {
            Patterns = new[] { "*.osu", "*.osb" }
        };

        private string[] currentBeatmaps = Array.Empty<string>();

        /// <summary>
        /// The beatmap the user works on: the first of the ones the shell holds. When
        /// the shell holds none, the user is asked to pick one.
        /// </summary>
        public string GetCurrentBeatmap() {
            if (currentBeatmaps.Length > 0) return currentBeatmaps[0];

            var picked = BeatmapFileDialog();
            if (picked.Length == 0) return null;

            SetCurrentBeatmaps(picked);
            return picked[0];
        }

        public string[] GetCurrentBeatmaps() => currentBeatmaps;

        public void SetCurrentBeatmaps(params string[] paths) {
            currentBeatmaps = paths ?? Array.Empty<string>();
            CurrentBeatmapsChanged?.Invoke(this, currentBeatmaps);
        }

        public event EventHandler<string[]> CurrentBeatmapsChanged;

        /// <summary>
        /// There is no osu! client to ask on Linux, because osu! stable itself runs in
        /// WINE there. The user picks the beatmap instead. See section 3.2 of
        /// LINUX_PORT.md.
        /// </summary>
        public string FetchBeatmapFromClient() {
            var picked = BeatmapFileDialog();
            return picked.Length > 0 ? picked[0] : string.Empty;
        }

        public string[] BeatmapFileDialog(bool multiselect = false) =>
            BeatmapFileDialog(CorePlatform.Settings.SongsPath, multiselect);

        public string[] BeatmapFileDialog(string initialDirectory, bool multiselect = false) =>
            RunOnUiThread(async provider => {
                var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions {
                    Title = multiselect ? "Select the beatmaps" : "Select the beatmap",
                    AllowMultiple = multiselect,
                    FileTypeFilter = new[] { OsuFiles },
                    SuggestedStartLocation = await FolderOrNull(provider, initialDirectory)
                });
                return files.Select(f => f.TryGetLocalPath())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
            }) ?? Array.Empty<string>();

        public string FolderDialog(string initialDirectory = null) =>
            RunOnUiThread(async provider => {
                var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                    Title = "Select the folder",
                    AllowMultiple = false,
                    SuggestedStartLocation = await FolderOrNull(provider, initialDirectory)
                });
                return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            }) ?? string.Empty;

        private static async Task<IStorageFolder> FolderOrNull(IStorageProvider provider, string path) {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return null;
            try {
                return await provider.TryGetFolderFromPathAsync(path);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Runs an Avalonia file picker, which is asynchronous, and waits for it. The
        /// core asks for its dialogs the synchronous way, so the wait has to happen
        /// somewhere. On the user interface thread it is a nested message loop, which
        /// keeps the window alive while the picker is up.
        /// </summary>
        private static T RunOnUiThread<T>(Func<IStorageProvider, Task<T>> pick) {
            var window = AvaloniaPlatformServices.MainWindow;
            if (window is null) return default;

            if (Dispatcher.UIThread.CheckAccess()) {
                T result = default;
                var frame = new DispatcherFrame();
                _ = pick(window.StorageProvider).ContinueWith(t => {
                    result = t.IsCompletedSuccessfully ? t.Result : default;
                    frame.Continue = false;
                }, TaskScheduler.FromCurrentSynchronizationContext());
                Dispatcher.UIThread.PushFrame(frame);
                return result;
            }

            return Dispatcher.UIThread.InvokeAsync(() => pick(window.StorageProvider))
                .GetAwaiter().GetResult();
        }
    }
}
