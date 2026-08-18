namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// Asks the user to choose a file or a folder.
    /// </summary>
    public interface IFileDialogService {
        /// <summary>Asks for a project file to open. Returns null if the user cancels.</summary>
        string LoadProjectDialog(string initialDirectory = null);

        /// <summary>Asks for a project file to save to. Returns null if the user cancels.</summary>
        string SaveProjectDialog(string initialDirectory = null);

        /// <summary>
        /// Gets the beatmap that the user works on now. Returns null if there is none.
        /// </summary>
        string GetCurrentBeatmap();
    }

    /// <summary>Chooses nothing. A host with no user interface uses this.</summary>
    public class NullFileDialogService : IFileDialogService {
        public string LoadProjectDialog(string initialDirectory = null) => null;
        public string SaveProjectDialog(string initialDirectory = null) => null;
        public string GetCurrentBeatmap() => null;
    }
}
