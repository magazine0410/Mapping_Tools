namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// Asks the operating system to show something to the user.
    /// </summary>
    /// <remarks>
    /// The core must not start <c>explorer.exe</c>. That program is on Windows only,
    /// and the call throws on Linux.
    /// </remarks>
    public interface IShellService {
        /// <summary>
        /// Opens a folder in the file manager of the user.
        /// </summary>
        void OpenFolder(string path);
    }

    /// <summary>Opens nothing. A host with no user interface uses this.</summary>
    public class NullShellService : IShellService {
        public void OpenFolder(string path) { }
    }
}
