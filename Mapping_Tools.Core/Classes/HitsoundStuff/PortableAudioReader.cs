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
                    return OpenWave(path);
                case ".aif":
                case ".aiff":
                    return new AiffFileReader(path);
                default:
                    // Try a WAV container. Many osu! samples have the wrong extension.
                    try {
                        return OpenWave(path);
                    } catch (NotSupportedException) {
                        throw;
                    } catch (Exception e) {
                        throw new NotSupportedException($"Cannot open the audio file \"{path}\".", e);
                    }
            }
        }

        /// <summary>
        /// Opens a WAV container.
        /// </summary>
        /// <remarks>
        /// <c>WaveFileReader</c> does not decode. It gives back the format that the file
        /// holds. <c>MediaFoundationReader</c>, which this replaces, always decoded to
        /// PCM. Two things follow.
        /// <para>
        /// A file in WAVE_FORMAT_EXTENSIBLE holds plain PCM or float samples, but it
        /// reports the encoding as Extensible. The sample provider downstream accepts
        /// only Pcm and IeeeFloat, so it would refuse the file. This method reports the
        /// standard format instead, and the samples pass through unchanged.
        /// </para>
        /// <para>
        /// A file with true compression, such as ADPCM or a-law, needs a decoder that
        /// NAudio.Core does not have. This method throws, so the caller can tell the
        /// user. Before, such a file failed later and without a reason.
        /// </para>
        /// </remarks>
        private static WaveStream OpenWave(string path) {
            var reader = new WaveFileReader(path);
            var format = reader.WaveFormat;

            if (format.Encoding == WaveFormatEncoding.Extensible) {
                WaveFormat standard;
                try {
                    standard = ToStandardFormat(format);
                } catch (Exception e) {
                    reader.Dispose();
                    throw new NotSupportedException(
                        $"The WAV file \"{path}\" uses an extensible format that cannot become PCM.", e);
                }
                return new ReformattedWaveStream(reader, standard);
            }

            if (format.Encoding != WaveFormatEncoding.Pcm &&
                format.Encoding != WaveFormatEncoding.IeeeFloat) {
                reader.Dispose();
                throw new NotSupportedException(
                    $"The WAV file \"{path}\" uses the {format.Encoding} encoding. " +
                    "This host can decode only PCM and IEEE float.");
            }

            return reader;
        }

        /// <summary>
        /// Reads the real format out of a WAVE_FORMAT_EXTENSIBLE header.
        /// </summary>
        /// <remarks>
        /// <c>WaveFileReader</c> gives a <see cref="WaveFormatExtraData"/> here, not a
        /// <see cref="WaveFormatExtensible"/>. So this reads the SubFormat GUID out of
        /// the extra bytes. The layout after the standard header is: 2 bytes of valid
        /// bits, 4 bytes of channel mask, then the 16 byte GUID. The first field of that
        /// GUID holds the true format tag: 1 for PCM, 3 for IEEE float.
        /// </remarks>
        private static WaveFormat ToStandardFormat(WaveFormat format) {
            if (format is WaveFormatExtensible extensible) {
                return extensible.ToStandardWaveFormat();
            }

            if (format is not WaveFormatExtraData extra || extra.ExtraData is null || extra.ExtraData.Length < 22) {
                throw new NotSupportedException("The extensible header has no SubFormat GUID.");
            }

            var subFormat = new Guid(extra.ExtraData.AsSpan(6, 16).ToArray());
            var tag = BitConverter.ToInt32(subFormat.ToByteArray(), 0);

            return tag switch {
                1 => new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels),
                3 => WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels),
                _ => throw new NotSupportedException(
                    $"The SubFormat {subFormat} is not PCM and not IEEE float.")
            };
        }
    }

    /// <summary>
    /// Passes a wave stream through, but reports a different format.
    /// The bytes do not change. Only the description of them changes.
    /// </summary>
    public class ReformattedWaveStream : WaveStream {
        private readonly WaveStream source;
        private readonly WaveFormat waveFormat;

        public ReformattedWaveStream(WaveStream source, WaveFormat waveFormat) {
            this.source = source;
            this.waveFormat = waveFormat;
        }

        public override WaveFormat WaveFormat => waveFormat;

        public override long Length => source.Length;

        public override long Position {
            get => source.Position;
            set => source.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            source.Read(buffer, offset, count);

        protected override void Dispose(bool disposing) {
            if (disposing) source.Dispose();
            base.Dispose(disposing);
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
