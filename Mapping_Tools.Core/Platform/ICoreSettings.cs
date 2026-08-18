namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// The settings that the core reads. The host owns the settings file.
    /// </summary>
    public interface ICoreSettings {
        string OsuPath { get; }
        string SongsPath { get; }
        string BackupsPath { get; }
        bool MakeBackups { get; }
        int MaxBackupFiles { get; }
        bool UseEditorReader { get; }
        bool AutoReload { get; }
    }

    /// <summary>Safe values for a host with no settings file.</summary>
    public class DefaultCoreSettings : ICoreSettings {
        public string OsuPath { get; set; } = string.Empty;
        public string SongsPath { get; set; } = string.Empty;
        public string BackupsPath { get; set; } =
            System.IO.Path.Combine(new NullAppPaths().AppDataPath, "Backups");
        public bool MakeBackups { get; set; } = true;
        public int MaxBackupFiles { get; set; } = 1000;
        public bool UseEditorReader { get; set; }
        public bool AutoReload { get; set; }
    }
}
