namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// Supplies the folders that the program writes to.
    /// This replaces the static path properties of the main window.
    /// </summary>
    public interface IAppPaths {
        /// <summary>
        /// The folder for the configuration, the projects, and the backups.
        /// </summary>
        string AppDataPath { get; }

        /// <summary>
        /// The folder that tools export to.
        /// </summary>
        string ExportPath { get; }

        /// <summary>
        /// The folder that holds the data of all users of this machine.
        /// </summary>
        string AppCommon { get; }
    }
}
