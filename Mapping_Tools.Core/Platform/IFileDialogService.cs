using System;

namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// Asks the user to choose a file or a folder, and holds the beatmaps that the user
    /// works on.
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

        /// <summary>
        /// The beatmaps that the host holds now. The array is never null, but it can be
        /// empty.
        /// </summary>
        string[] GetCurrentBeatmaps();

        /// <summary>
        /// Puts these beatmaps in the host, and raises
        /// <see cref="CurrentBeatmapsChanged"/>.
        /// </summary>
        void SetCurrentBeatmaps(params string[] paths);

        /// <summary>Raised after <see cref="SetCurrentBeatmaps"/> changes the list.</summary>
        event EventHandler<string[]> CurrentBeatmapsChanged;

        /// <summary>
        /// Asks the running osu! which beatmap it has open.
        /// </summary>
        /// <remarks>
        /// Only a Windows host can read the memory of osu!. A host that cannot ask
        /// gives the user a file dialog instead, or returns an empty string.
        /// <para>
        /// The Windows host throws when it cannot read the client, and the message says
        /// why: usually there is no Songs path in Preferences. Callers catch it and
        /// show it. Do not swallow it here, or the user gets silence instead of the
        /// reason.
        /// </para>
        /// </remarks>
        /// <returns>The path, or an empty string when there is no answer.</returns>
        string FetchBeatmapFromClient();

        /// <summary>Asks the user for one beatmap file, or more.</summary>
        /// <param name="multiselect">True lets the user pick more than one.</param>
        /// <returns>The paths. The array is empty when the user cancels.</returns>
        string[] BeatmapFileDialog(bool multiselect = false);

        /// <summary>Asks the user for one beatmap file, or more, starting in a folder.</summary>
        /// <returns>The paths. The array is empty when the user cancels.</returns>
        string[] BeatmapFileDialog(string initialDirectory, bool multiselect = false);

        /// <summary>Asks the user for a folder.</summary>
        /// <returns>The path, or an empty string when the user cancels.</returns>
        string FolderDialog(string initialDirectory = null);
    }

    /// <summary>
    /// Chooses nothing, and remembers the beatmaps it is given. A host with no user
    /// interface uses this.
    /// </summary>
    public class NullFileDialogService : IFileDialogService {
        private string[] currentBeatmaps = Array.Empty<string>();

        public string LoadProjectDialog(string initialDirectory = null) => null;
        public string SaveProjectDialog(string initialDirectory = null) => null;

        public string GetCurrentBeatmap() =>
            currentBeatmaps.Length > 0 ? currentBeatmaps[0] : null;

        public string[] GetCurrentBeatmaps() => currentBeatmaps;

        public void SetCurrentBeatmaps(params string[] paths) {
            currentBeatmaps = paths ?? Array.Empty<string>();
            CurrentBeatmapsChanged?.Invoke(this, currentBeatmaps);
        }

        public event EventHandler<string[]> CurrentBeatmapsChanged;

        public string FetchBeatmapFromClient() => string.Empty;

        public string[] BeatmapFileDialog(bool multiselect = false) => Array.Empty<string>();

        public string[] BeatmapFileDialog(string initialDirectory, bool multiselect = false) =>
            Array.Empty<string>();

        public string FolderDialog(string initialDirectory = null) => string.Empty;
    }
}
