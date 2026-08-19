using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.ToolHelpers;
using Mapping_Tools.Components.Domain;
using Editor_Reader;
using HitObject = Mapping_Tools.Classes.BeatmapHelper.HitObject;

namespace Mapping_Tools.Platform {
    /// <summary>
    /// Gives the portable core the Windows and WPF services that it asks for.
    /// Call <see cref="Register"/> once, when the program starts.
    /// </summary>
    public static class WpfPlatformServices {
        public static void Register() {
            CorePlatform.Dialogs = new WpfDialogService();
            CorePlatform.Notifications = new WpfNotificationService();
            CorePlatform.Paths = new WpfAppPaths();
            CorePlatform.EditorReader = new WpfEditorReaderService();
            CorePlatform.Settings = new WpfCoreSettings();
            CorePlatform.FileDialogs = new WpfFileDialogService();
            CorePlatform.Shell = new WindowsShellService();
            CorePlatform.IsShiftDown = () =>
                Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            CommandRequery.Subscribe = h => CommandManager.RequerySuggested += h;
            CommandRequery.Unsubscribe = h => CommandManager.RequerySuggested -= h;
            CommandRequery.Invalidate = CommandManager.InvalidateRequerySuggested;

            ColourPointContextMenu.Register();
        }
    }

    public class WpfDialogService : IDialogService {
        public void ShowMessage(string message, string title = null) =>
            MessageBox.Show(message, title ?? string.Empty);

        public bool AskYesNo(string message, string title = null) =>
            MessageBox.Show(message, title ?? string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes;

        public bool? AskYesNoCancel(string message, string title = null) {
            var result = MessageBox.Show(message, title ?? string.Empty, MessageBoxButton.YesNoCancel);
            if (result == MessageBoxResult.Cancel) return null;
            return result == MessageBoxResult.Yes;
        }

        public bool ShowOkCancel(string message, string title = null) =>
            MessageBox.Show(message, title ?? string.Empty, MessageBoxButton.OKCancel) == MessageBoxResult.OK;
    }

    public class WpfNotificationService : INotificationService {
        public void Notify(string message) => MainWindow.MessageQueue?.Enqueue(message);
    }

    public class WpfAppPaths : IAppPaths {
        public string AppDataPath => MainWindow.AppDataPath;
        public string ExportPath => MainWindow.ExportPath;
        public string AppCommon => MainWindow.AppCommon;
    }

    public class WpfCoreSettings : ICoreSettings {
        public string OsuPath => SettingsManager.GetOsuPath();
        public string SongsPath => SettingsManager.GetSongsPath();
        public string BackupsPath => SettingsManager.GetBackupsPath();
        public bool MakeBackups => SettingsManager.GetMakeBackups();
        public int MaxBackupFiles => SettingsManager.Settings.MaxBackupFiles;
        public bool UseEditorReader => SettingsManager.Settings.UseEditorReader;
        public bool AutoReload => SettingsManager.Settings.AutoReload;
    }

    public class WindowsShellService : IShellService {
        public void OpenFolder(string path) =>
            System.Diagnostics.Process.Start("explorer.exe", path);
    }

    public class WpfFileDialogService : IFileDialogService {
        public string LoadProjectDialog(string initialDirectory = null) =>
            IOHelper.LoadProjectDialog(initialDirectory);

        public string SaveProjectDialog(string initialDirectory = null) =>
            IOHelper.SaveProjectDialog(initialDirectory);

        public string GetCurrentBeatmap() => IOHelper.GetCurrentBeatmap();
    }

    /// <summary>Reads the live osu! editor through EditorReader.</summary>
    public class WpfEditorReaderService : IEditorReaderService {
        public bool IsAvailable => SettingsManager.Settings.UseEditorReader;

        public string DontCoolSaveWhenMd5EqualsThisString {
            get => EditorReaderStuff.DontCoolSaveWhenMd5EqualsThisString;
            set => EditorReaderStuff.DontCoolSaveWhenMd5EqualsThisString = value;
        }

        public string GetMd5FromPath(string path) => EditorReaderStuff.GetMd5FromPath(path);

        public int GetEditorTime() => EditorReaderStuff.GetEditorTime();

        public object GetFullEditorReaderOrNot() => EditorReaderStuff.GetFullEditorReaderOrNot();

        public BeatmapEditor GetNewestVersionOrNot(string path) =>
            EditorReaderStuff.GetNewestVersionOrNot(path);

        public BeatmapEditor GetNewestVersionOrNot(string path, object fullReader) =>
            EditorReaderStuff.GetNewestVersionOrNot(path, fullReader as EditorReader);

        public BeatmapEditor GetNewestVersionOrNot(string path, out List<HitObject> selected,
            out Exception exception) =>
            EditorReaderStuff.GetNewestVersionOrNot(path, out selected, out exception);

        public BeatmapEditor GetNewestVersionOrNot(string path, object fullReader,
            out List<HitObject> selected, out Exception exception) =>
            EditorReaderStuff.GetNewestVersionOrNot(path, fullReader as EditorReader, out selected, out exception);

        public void ForceReloadEditor() => ListenerManager.ForceReloadEditor();
    }
}
