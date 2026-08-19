using System.IO;

namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>Plays a rendered audio file through the host operating system.</summary>
    public interface IAudioPlaybackService {
        bool IsAvailable { get; }

        /// <summary>
        /// Starts playback. When <paramref name="deleteWhenFinished"/> is true, the
        /// service owns the file and removes it after playback stops.
        /// </summary>
        void PlayFile(string path, bool deleteWhenFinished = false);
        void Stop();
    }

    public class NullAudioPlaybackService : IAudioPlaybackService {
        public bool IsAvailable => false;
        public void PlayFile(string path, bool deleteWhenFinished = false) {
            if (deleteWhenFinished && !string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
                File.Delete(path);
            }
        }
        public void Stop() { }
    }
}
