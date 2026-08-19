using System;
using System.IO;
using Mapping_Tools.Classes.SystemTools.Platform;
using Newtonsoft.Json;

namespace Mapping_Tools.Avalonia.Platform {
    /// <summary>
    /// Keeps the settings that the core reads in a JSON file.
    /// </summary>
    /// <remarks>
    /// The WPF host has a much larger settings class, with hotkeys and window bounds.
    /// The core needs only these values, so this host keeps only these. It reads the
    /// same file name in the same folder, but it does not share the format.
    /// </remarks>
    public class JsonCoreSettings : ICoreSettings {
        [JsonIgnore] public string FilePath { get; }

        public string OsuPath { get; set; } = string.Empty;
        public string SongsPath { get; set; } = string.Empty;
        public string OsuConfigPath { get; set; } = string.Empty;
        public string BackupsPath { get; set; } = string.Empty;
        public bool MakeBackups { get; set; } = true;
        public int MaxBackupFiles { get; set; } = 1000;
        public bool UseEditorReader { get; set; }
        public bool AutoReload { get; set; }
        public bool DarkTheme { get; set; } = true;

        public JsonCoreSettings() {
            FilePath = Path.Combine(CorePlatform.Paths.AppDataPath, "config.avalonia.json");
        }

        public static JsonCoreSettings Load() {
            var settings = new JsonCoreSettings();
            try {
                if (File.Exists(settings.FilePath)) {
                    JsonConvert.PopulateObject(File.ReadAllText(settings.FilePath), settings);
                }
            } catch (Exception e) {
                Console.Error.WriteLine($"Could not read the settings: {e.Message}");
            }

            if (string.IsNullOrEmpty(settings.BackupsPath)) {
                settings.BackupsPath = Path.Combine(CorePlatform.Paths.AppDataPath, "Backups");
            }

            // The editor reader cannot work on Linux, so never ask the core to use it.
            settings.UseEditorReader = false;
            return settings;
        }

        public void Save() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            } catch (Exception e) {
                Console.Error.WriteLine($"Could not write the settings: {e.Message}");
            }
        }
    }
}
