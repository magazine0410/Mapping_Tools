using System;
using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;
using NLayer;

namespace Mapping_Tools.Classes.HitsoundStuff {
    /// <summary>
    /// Opens an audio file on any operating system.
    /// </summary>
    /// <remarks>
    /// This replaces <c>MediaFoundationReader</c>, which is a Windows component.
    /// Ogg Vorbis uses NAudio.Vorbis. MP3 uses NLayer. WAV and AIFF use NAudio.
    /// </remarks>
    public static class PortableAudioReader {
        /// <summary>
        /// Opens an audio file and gives back a stream of samples.
        /// </summary>
        /// <exception cref="NotSupportedException">The file type is not known.</exception>
        public static WaveStream Open(string path) {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();

            switch (extension) {
                case ".ogg":
                    return new VorbisWaveReader(path);
                case ".mp3":
                    return new Mp3WaveStream(path);
                case ".wav":
                case ".wave":
                    return new WaveFileReader(path);
                case ".aif":
                case ".aiff":
                    return new AiffFileReader(path);
                default:
                    // Try a WAV container. Many osu! samples have the wrong extension.
                    try {
                        return new WaveFileReader(path);
                    } catch (Exception e) {
                        throw new NotSupportedException($"Cannot open the audio file \"{path}\".", e);
                    }
            }
        }
    }

    /// <summary>
    /// Reads an MP3 file with NLayer, which works on all operating systems.
    /// </summary>
    public class Mp3WaveStream : WaveStream {
        private readonly MpegFile mpegFile;
        private readonly WaveFormat waveFormat;

        public Mp3WaveStream(string path) {
            mpegFile = new MpegFile(path);
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(mpegFile.SampleRate, mpegFile.Channels);
        }

        public override WaveFormat WaveFormat => waveFormat;

        public override long Length => mpegFile.Length;

        public override long Position {
            get => mpegFile.Position;
            set => mpegFile.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return mpegFile.ReadSamples(buffer, offset, count);
        }

        protected override void Dispose(bool disposing) {
            if (disposing) mpegFile?.Dispose();
            base.Dispose(disposing);
        }
    }
}
