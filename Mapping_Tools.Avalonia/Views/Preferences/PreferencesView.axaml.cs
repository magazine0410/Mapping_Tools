using System;
using System.IO;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Mapping_Tools.Avalonia.Platform;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Avalonia.Views.Preferences {
    [VerticalContentScroll]
    public partial class PreferencesView : MappingTool {
        public static readonly string ToolName = "Preferences";
        public static readonly string ToolDescription =
            "Configure beatmap folders, backups, and the application theme.";

        public PreferencesView() : this(CorePlatform.Settings as JsonCoreSettings ??
            new JsonCoreSettings {
                OsuPath = CorePlatform.Settings.OsuPath,
                SongsPath = CorePlatform.Settings.SongsPath,
                BackupsPath = CorePlatform.Settings.BackupsPath,
                MakeBackups = CorePlatform.Settings.MakeBackups,
                MaxBackupFiles = CorePlatform.Settings.MaxBackupFiles
            }) { }

        internal PreferencesView(JsonCoreSettings settings) {
            InitializeComponent();
            DataContext = new PreferencesModel(settings);
        }

        public PreferencesModel ViewModel => (PreferencesModel)DataContext;

        private void BrowseOsu_Click(object sender, RoutedEventArgs e) =>
            PickFolder(path => ViewModel.OsuPath = path, ViewModel.OsuPath);
        private void BrowseSongs_Click(object sender, RoutedEventArgs e) =>
            PickFolder(path => ViewModel.SongsPath = path, ViewModel.SongsPath);
        private void BrowseBackups_Click(object sender, RoutedEventArgs e) =>
            PickFolder(path => ViewModel.BackupsPath = path, ViewModel.BackupsPath);

        private static void PickFolder(Action<string> assign, string initialDirectory) {
            string path = CorePlatform.FileDialogs.FolderDialog(initialDirectory);
            if (!string.IsNullOrWhiteSpace(path)) assign(path);
        }

    }

    public sealed class PreferencesModel : BindableBase {
        private readonly JsonCoreSettings settings;
        private readonly Action save;

        internal PreferencesModel(JsonCoreSettings settings, Action save = null) {
            this.settings = settings;
            this.save = save ?? settings.Save;
        }

        public string OsuPath {
            get => settings.OsuPath;
            set => Update(settings.OsuPath, value, v => settings.OsuPath = v);
        }
        public string SongsPath {
            get => settings.SongsPath;
            set => Update(settings.SongsPath, value, v => settings.SongsPath = v);
        }
        public string BackupsPath {
            get => settings.BackupsPath;
            set => Update(settings.BackupsPath, value, v => {
                settings.BackupsPath = v;
                if (!string.IsNullOrWhiteSpace(v)) Directory.CreateDirectory(v);
            });
        }
        public string OsuConfigPath {
            get => settings.OsuConfigPath;
            set => Update(settings.OsuConfigPath, value, v => settings.OsuConfigPath = v);
        }
        public bool MakeBackups {
            get => settings.MakeBackups;
            set => Update(settings.MakeBackups, value, v => settings.MakeBackups = v);
        }
        public int MaxBackupFiles {
            get => settings.MaxBackupFiles;
            set => Update(settings.MaxBackupFiles, Math.Max(1, value),
                v => settings.MaxBackupFiles = v);
        }
        public bool DarkTheme {
            get => settings.DarkTheme;
            set => Update(settings.DarkTheme, value, v => {
                settings.DarkTheme = v;
                if (Application.Current is not null) {
                    Application.Current.RequestedThemeVariant = v
                        ? ThemeVariant.Dark
                        : ThemeVariant.Light;
                }
            });
        }

        private void Update<T>(T oldValue, T newValue, Action<T> assign,
            [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "") {
            if (Equals(oldValue, newValue)) return;
            assign(newValue);
            save();
            RaisePropertyChanged(propertyName);
        }
    }
}
