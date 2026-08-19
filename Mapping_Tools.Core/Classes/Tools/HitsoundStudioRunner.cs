using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Classes.Tools {
    public static class HitsoundStudioRunner {
        public static string Preview(HitsoundLayer layer) {
            if (layer?.SampleArgs is null ||
                string.IsNullOrWhiteSpace(layer.SampleArgs.Path)) {
                throw new InvalidOperationException("Choose a sample for this layer first.");
            }
            if (!CorePlatform.Audio.IsAvailable) {
                throw new InvalidOperationException("Audio playback is not available on this system.");
            }
            string directory = Path.Combine(CorePlatform.Paths.AppDataPath, "Audio Previews");
            Directory.CreateDirectory(directory);
            string name = $"preview-{Guid.NewGuid():N}";
            if (!HitsoundExporter.ExportSample(layer.SampleArgs, name, directory,
                    format: HitsoundExporter.SampleExportFormat.WavePcm)) {
                throw new InvalidDataException("The sample could not be rendered.");
            }
            string path = Path.Combine(directory, name + ".wav");
            CorePlatform.Audio.PlayFile(path, deleteWhenFinished: true);
            return "Playing sample preview.";
        }

        public static string Export(HitsoundStudioVm args,
            BackgroundWorker worker = null) {
            if (args.HitsoundLayers.Count == 0) {
                throw new InvalidOperationException("Add at least one hitsound layer first.");
            }
            if ((args.ExportMap || args.HitsoundExportModeSetting ==
                    HitsoundStudioVm.HitsoundExportMode.Midi) &&
                string.IsNullOrWhiteSpace(args.BaseBeatmap)) {
                throw new InvalidOperationException("Choose a base beatmap first.");
            }
            if (args.UsePreviousSampleSchema && args.PreviousSampleSchema is null) {
                throw new InvalidOperationException(
                    "A previous sample schema is not available. Export once without reusing it first.");
            }
            Directory.CreateDirectory(args.ExportFolder);
            bool validateFiles = args.SingleSampleExportFormat !=
                                 HitsoundExporter.SampleExportFormat.MidiChords &&
                                 args.MixedSampleExportFormat !=
                                 HitsoundExporter.SampleExportFormat.MidiChords;
            var comparer = new SampleGeneratingArgsComparer(validateFiles);
            string result = string.Empty;

            if (args.HitsoundExportModeSetting ==
                HitsoundStudioVm.HitsoundExportMode.Standard) {
                var packages = HitsoundConverter.ZipLayers(args.HitsoundLayers,
                    args.DefaultSample, args.ZipLayersLeniency, validateFiles);
                Progress(worker, 10);
                HitsoundConverter.BalanceVolumes(packages, 0, false);
                var sampleArgs = new HashSet<SampleGeneratingArgs>(comparer);
                foreach (var package in packages) {
                    sampleArgs.UnionWith(package.Samples.Select(o => o.SampleArgs));
                }
                var loaded = SampleImporter.ImportSamples(sampleArgs, comparer);
                Progress(worker, 30);
                var complete = HitsoundConverter.GetCompleteHitsounds(packages, loaded,
                    args.UsePreviousSampleSchema
                        ? args.PreviousSampleSchema.GetCustomIndices()
                        : null,
                    args.AllowGrowthPreviousSampleSchema, args.FirstCustomIndex,
                    validateFiles, comparer);
                SaveSchema(args, new SampleSchema(complete.CustomIndices));
                if (args.ShowResults) {
                    int samples = complete.CustomIndices.SelectMany(o => o.Samples.Values)
                        .Count(group => group.Any(o =>
                            SampleImporter.ValidateSampleArgs(o, loaded, validateFiles)));
                    int greenlines = 0;
                    int lastIndex = -1;
                    foreach (var hit in complete.Hitsounds
                                 .Where(hit => hit.CustomIndex != lastIndex)) {
                        lastIndex = hit.CustomIndex;
                        greenlines++;
                    }
                    result = $"Number of sample indices: {complete.CustomIndices.Count}, " +
                             $"Number of samples: {samples}, Number of greenlines: {greenlines}";
                }
                ClearExportIfRequested(args, args.ExportSamples || args.ExportMap);
                Progress(worker, 70);
                if (args.ExportMap) {
                    HitsoundExporter.ExportHitsounds(complete.Hitsounds,
                        args.BaseBeatmap, args.ExportFolder, args.HitsoundDiffName,
                        args.HitsoundExportGameMode, true, false);
                }
                if (args.ExportSamples) {
                    HitsoundExporter.ExportCustomIndices(complete.CustomIndices,
                        args.ExportFolder, loaded, args.SingleSampleExportFormat,
                        args.MixedSampleExportFormat, comparer);
                }
            } else if (args.HitsoundExportModeSetting ==
                       HitsoundStudioVm.HitsoundExportMode.Coinciding ||
                       args.HitsoundExportModeSetting ==
                       HitsoundStudioVm.HitsoundExportMode.Storyboard) {
                var packages = HitsoundConverter.ZipLayers(args.HitsoundLayers,
                    args.DefaultSample, 0, false);
                HitsoundConverter.BalanceVolumes(packages, 0, false, true);
                Dictionary<SampleGeneratingArgs, SampleSoundGenerator> loaded = null;
                Dictionary<SampleGeneratingArgs, string> names = args.UsePreviousSampleSchema
                    ? args.PreviousSampleSchema?.GetSampleNames(comparer)
                    : null;
                Dictionary<SampleGeneratingArgs, Vector2> positions = null;
                bool coinciding = args.HitsoundExportModeSetting ==
                                   HitsoundStudioVm.HitsoundExportMode.Coinciding;
                var hitsounds = HitsoundConverter.GetHitsounds(packages, ref loaded,
                    ref names, ref positions,
                    coinciding && args.HitsoundExportGameMode == GameMode.Mania,
                    coinciding && args.AddCoincidingRegularHitsounds,
                    args.AllowGrowthPreviousSampleSchema, validateFiles, comparer);
                SaveSchema(args, new SampleSchema(names));
                if (args.ShowResults) {
                    result = $"Number of sample indices: 0, Number of samples: " +
                             $"{loaded.Count}, Number of greenlines: 0";
                }
                ClearExportIfRequested(args, args.ExportSamples || args.ExportMap);
                Progress(worker, 60);
                if (args.ExportMap) {
                    HitsoundExporter.ExportHitsounds(hitsounds, args.BaseBeatmap,
                        args.ExportFolder, args.HitsoundDiffName,
                        args.HitsoundExportGameMode, false, !coinciding);
                }
                if (args.ExportSamples) {
                    HitsoundExporter.ExportLoadedSamples(loaded, args.ExportFolder,
                        names, args.SingleSampleExportFormat, comparer);
                }
            } else {
                var packages = HitsoundConverter.ZipLayers(args.HitsoundLayers,
                    args.DefaultSample, 0, false);
                var beatmap = CorePlatform.EditorReader
                    .GetNewestVersionOrNot(args.BaseBeatmap).Beatmap;
                if (args.ShowResults) {
                    result = $"Number of notes: {packages.SelectMany(o => o.Samples).Count()}, " +
                             $"Number of volume changes: " +
                             $"{(args.AddGreenLineVolumeToMidi ? beatmap.BeatmapTiming.TimingPoints.Count : 0)}";
                }
                ClearExportIfRequested(args, args.ExportMap);
                if (args.ExportMap) {
                    MidiExporter.ExportAsMidi(packages, beatmap,
                        Path.Combine(args.ExportFolder, args.HitsoundDiffName + ".mid"),
                        args.AddGreenLineVolumeToMidi);
                }
            }
            Progress(worker, 100);
            if (args.ExportMap || args.ExportSamples) {
                CorePlatform.Shell.OpenFolder(args.ExportFolder);
            }
            return result;
        }

        private static void SaveSchema(HitsoundStudioVm args, SampleSchema schema) {
            if (!args.UsePreviousSampleSchema || args.PreviousSampleSchema is null) {
                args.PreviousSampleSchema = schema;
            } else if (args.AllowGrowthPreviousSampleSchema) {
                args.PreviousSampleSchema.MergeWith(schema);
            }
        }

        /// <summary>
        /// Empties the export folder, but only when this run is going to write
        /// something back into it.
        /// </summary>
        /// <param name="willWriteSomething">
        /// True when the branch that calls this is about to write a file. The MIDI
        /// branch writes only the beatmap, so it must not pass ExportSamples: doing so
        /// deletes the folder and then writes nothing.
        /// </param>
        private static void ClearExportIfRequested(HitsoundStudioVm args, bool willWriteSomething) {
            if (!args.DeleteAllInExportFirst || !willWriteSomething) {
                return;
            }
            foreach (string path in Directory.GetFiles(args.ExportFolder)) File.Delete(path);
        }

        private static void Progress(BackgroundWorker worker, int value) {
            if (worker is { WorkerReportsProgress: true }) worker.ReportProgress(value);
        }
    }
}
