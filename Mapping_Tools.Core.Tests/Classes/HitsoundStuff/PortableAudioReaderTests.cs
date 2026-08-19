using System;
using System.IO;
using Mapping_Tools.Classes.HitsoundStuff;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;

namespace Mapping_Tools_Tests.Classes.HitsoundStuff {
    /// <summary>
    /// MediaFoundationReader always decoded to PCM. PortableAudioReader replaced it,
    /// and WaveFileReader does not decode. These tests hold the difference in place.
    /// </summary>
    [TestClass]
    public class PortableAudioReaderTests {
        private static string WriteWave(WaveFormat format, string name) {
            var path = Path.Combine(Path.GetTempPath(), $"mt_{name}_{Guid.NewGuid():N}.wav");
            using (var writer = new WaveFileWriter(path, format)) {
                // 100 ms of silence is enough. The format matters, not the samples.
                var bytes = new byte[format.AverageBytesPerSecond / 10];
                writer.Write(bytes, 0, bytes.Length);
            }
            return path;
        }

        [TestMethod]
        public void PlainPcmWaveOpens() {
            var path = WriteWave(new WaveFormat(44100, 16, 2), "pcm");
            try {
                using var stream = PortableAudioReader.Open(path);
                Assert.AreEqual(WaveFormatEncoding.Pcm, stream.WaveFormat.Encoding);
                Assert.AreEqual(44100, stream.WaveFormat.SampleRate);
                Assert.AreEqual(2, stream.WaveFormat.Channels);
            } finally {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void IeeeFloatWaveOpens() {
            var path = WriteWave(WaveFormat.CreateIeeeFloatWaveFormat(48000, 1), "float");
            try {
                using var stream = PortableAudioReader.Open(path);
                Assert.AreEqual(WaveFormatEncoding.IeeeFloat, stream.WaveFormat.Encoding);
            } finally {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A WAVE_FORMAT_EXTENSIBLE file holds plain PCM, but reports the encoding as
        /// Extensible. The sample provider downstream accepts only Pcm and IeeeFloat,
        /// so the reader must report the standard format instead.
        /// </summary>
        [TestMethod]
        public void ExtensibleWaveReportsStandardFormat() {
            var path = WriteWave(new WaveFormatExtensible(44100, 24, 2), "extensible");
            try {
                using var stream = PortableAudioReader.Open(path);
                Assert.AreNotEqual(WaveFormatEncoding.Extensible, stream.WaveFormat.Encoding,
                    "An Extensible encoding reaches the sample provider and is refused there.");
                Assert.AreEqual(WaveFormatEncoding.Pcm, stream.WaveFormat.Encoding);
                Assert.AreEqual(24, stream.WaveFormat.BitsPerSample);
                Assert.AreEqual(44100, stream.WaveFormat.SampleRate);
            } finally {
                File.Delete(path);
            }
        }

        /// <summary>
        /// An encoding that NAudio.Core cannot decode must say so. Before, it passed
        /// validation and then failed later with no reason given.
        /// </summary>
        [TestMethod]
        public void CompressedWaveThrowsWithAReason() {
            var path = WriteWave(WaveFormat.CreateALawFormat(8000, 1), "alaw");
            try {
                var e = Assert.ThrowsException<NotSupportedException>(
                    () => PortableAudioReader.Open(path));
                StringAssert.Contains(e.Message, "ALaw");
            } finally {
                File.Delete(path);
            }
        }
    }
}
