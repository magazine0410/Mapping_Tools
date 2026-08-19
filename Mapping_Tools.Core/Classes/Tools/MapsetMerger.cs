using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.BeatmapHelper.Events;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Classes.Tools {
    /// <summary>Combines mapsets while isolating their referenced assets.</summary>
    public static class MapsetMerger {
        private const int MaxMapsetMaps = 200;
        private static readonly Regex HitsoundSampleFilename = new(
            "^(normal|soft|drum)-(hit(normal|whistle|finish|clap)|slidertick|sliderslide|sliderwhistle)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly string[] AudioExtensions = [".wav", ".mp3", ".ogg"];
        private static readonly string[] ExplicitAudioExtensions = [".wav", ".ogg", ".mp3"];
        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg"];
        private static readonly string[] VideoExtensions = [".mp4", ".avi"];

        public static string Merge(MapsetMergerVm args, BackgroundWorker worker = null) {
            Directory.CreateDirectory(args.ExportPath);
            int mapsetsMerged = 0;
            int indexStart = 1;
            ResolveDuplicateNames(args.Mapsets);
            var usedDifficultyNames = new HashSet<string>();

            foreach (var mapset in args.Mapsets) {
                string subfolder = mapset.Name;
                string prefix = mapset.Name + " - ";
                var beatmaps = LoadBeatmaps(mapset).ToList();
                var storyboards = LoadStoryboards(mapset).ToList();
                var indices = new Dictionary<int, int>();
                var usedHitsoundFiles = new HashSet<string>();
                var usedOtherAudio = new HashSet<string>();
                var usedImages = new HashSet<string>();
                var usedVideos = new HashSet<string>();

                StoryBoard sharedStoryboard = null;
                if (args.MoveSbToBeatmap) {
                    sharedStoryboard = storyboards.FirstOrDefault()?.Item2;
                    if (sharedStoryboard is not null) {
                        UpdateReferences(sharedStoryboard, subfolder, usedOtherAudio,
                            usedImages, usedVideos);
                    }
                } else {
                    foreach (var (sourcePath, storyboard) in storyboards) {
                        UpdateReferences(storyboard, subfolder, usedOtherAudio,
                            usedImages, usedVideos);
                        Editor.SaveFile(Path.Combine(args.ExportPath,
                            prefix + Path.GetFileName(sourcePath)), storyboard.GetLines());
                    }
                }

                foreach (var (_, beatmap) in beatmaps) {
                    UpdateReferences(beatmap, subfolder, ref indexStart, indices,
                        usedHitsoundFiles, usedOtherAudio, usedImages, usedVideos);
                    if (sharedStoryboard is not null) {
                        beatmap.StoryBoard.StoryboardLayerBackground =
                            sharedStoryboard.StoryboardLayerBackground;
                        beatmap.StoryBoard.StoryboardLayerForeground =
                            sharedStoryboard.StoryboardLayerForeground;
                        beatmap.StoryBoard.StoryboardLayerFail =
                            sharedStoryboard.StoryboardLayerFail;
                        beatmap.StoryBoard.StoryboardLayerPass =
                            sharedStoryboard.StoryboardLayerPass;
                        beatmap.StoryBoard.StoryboardLayerOverlay =
                            sharedStoryboard.StoryboardLayerOverlay;
                        beatmap.StoryBoard.StoryboardSoundSamples =
                            sharedStoryboard.StoryboardSoundSamples;
                    }

                    string difficultyName = beatmap.Metadata["Version"].Value;
                    if (usedDifficultyNames.Contains(difficultyName)) {
                        difficultyName = prefix + difficultyName;
                    }
                    usedDifficultyNames.Add(difficultyName);
                    beatmap.Metadata["Version"].Value = difficultyName;
                    Editor.SaveFile(Path.Combine(args.ExportPath, beatmap.GetFileName()),
                        beatmap.GetLines());
                }

                foreach (var filename in usedHitsoundFiles) {
                    var source = FindAssetFile(filename, mapset.Path, AudioExtensions);
                    if (source is null) continue;
                    string extension = Path.GetExtension(source);
                    string name = Path.GetFileNameWithoutExtension(source);
                    var match = HitsoundSampleFilename.Match(name);
                    if (!match.Success) continue;
                    string remainder = name[(match.Index + match.Length)..];
                    int oldIndex = 1;
                    if (!string.IsNullOrWhiteSpace(remainder) &&
                        !FileFormatHelper.TryParseInt(remainder, out oldIndex)) continue;
                    if (!indices.TryGetValue(oldIndex, out int newIndex)) newIndex = oldIndex;
                    string newFilename = match.Value +
                                         (newIndex == 1 ? string.Empty : newIndex) + extension;
                    File.Copy(source, Path.Combine(args.ExportPath, newFilename), true);
                }

                foreach (var filename in usedOtherAudio) {
                    SaveAsset(filename, mapset.Path, subfolder, args.ExportPath,
                        ExplicitAudioExtensions);
                }
                foreach (var filename in usedImages) {
                    SaveAsset(filename, mapset.Path, subfolder, args.ExportPath,
                        ImageExtensions);
                }
                foreach (var filename in usedVideos) {
                    SaveAsset(filename, mapset.Path, subfolder, args.ExportPath,
                        VideoExtensions, true);
                }

                mapsetsMerged++;
                if (worker is { WorkerReportsProgress: true }) {
                    worker.ReportProgress(mapsetsMerged * 100 / args.Mapsets.Count);
                }
            }

            return $"Successfully merged {mapsetsMerged} " +
                   $"{(mapsetsMerged == 1 ? "mapset" : "mapsets")}.";
        }

        private static void SaveAsset(string filename, string sourceFolder,
            string subfolder, string exportFolder, string[] extensions,
            bool requireExtension = false) {
            var source = FindAssetFile(filename, sourceFolder, extensions,
                requireExtension);
            if (source is null) return;
            string destination = Path.Combine(exportFolder, subfolder,
                Path.ChangeExtension(filename, null) + Path.GetExtension(source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
        }

        private static string FindAssetFile(string filename, string folder,
            string[] extensions, bool requireExtension = false) {
            string path = Path.Combine(folder, filename);
            string originalExtension = Path.GetExtension(filename);
            if (!string.IsNullOrEmpty(originalExtension) || requireExtension) {
                return !string.IsNullOrEmpty(originalExtension) &&
                       extensions.Contains(originalExtension,
                           StringComparer.OrdinalIgnoreCase) && File.Exists(path)
                    ? path
                    : null;
            }
            string withoutExtension = Path.ChangeExtension(path, null);
            return extensions.Select(extension => withoutExtension + extension)
                .FirstOrDefault(File.Exists);
        }

        private static void UpdateReferences(StoryBoard storyboard, string subfolder,
            HashSet<string> audio, HashSet<string> images, HashSet<string> videos) {
            UpdateReferences(storyboard.BackgroundAndVideoEvents
                .Concat(storyboard.StoryboardSoundSamples)
                .Concat(storyboard.StoryboardLayerFail)
                .Concat(storyboard.StoryboardLayerPass)
                .Concat(storyboard.StoryboardLayerBackground)
                .Concat(storyboard.StoryboardLayerForeground)
                .Concat(storyboard.StoryboardLayerOverlay), subfolder, audio, images,
                videos);
        }

        private static void UpdateReferences(IEnumerable<Event> events, string subfolder,
            HashSet<string> audio, HashSet<string> images, HashSet<string> videos) {
            foreach (var ev in events) {
                switch (ev) {
                    case StoryboardSoundSample sample:
                        audio.Add(sample.FilePath);
                        sample.FilePath = Path.Combine(subfolder, sample.FilePath);
                        break;
                    case Animation animation:
                        for (int index = 0; index < animation.FrameCount; index++) {
                            images.Add(Path.GetFileNameWithoutExtension(animation.FilePath) + index);
                        }
                        animation.FilePath = Path.Combine(subfolder, animation.FilePath!);
                        break;
                    case Sprite sprite:
                        images.Add(sprite.FilePath);
                        sprite.FilePath = Path.Combine(subfolder, sprite.FilePath);
                        break;
                    case Background background:
                        images.Add(background.Filename);
                        background.Filename = Path.Combine(subfolder, background.Filename);
                        break;
                    case Video video:
                        videos.Add(video.Filename);
                        video.Filename = Path.Combine(subfolder, video.Filename);
                        break;
                }
                if (ev.ChildEvents.Count > 0) {
                    UpdateReferences(ev.ChildEvents, subfolder, audio, images, videos);
                }
            }
        }

        private static void UpdateReferences(Beatmap beatmap, string subfolder,
            ref int indexStart, Dictionary<int, int> indices,
            HashSet<string> hitsounds, HashSet<string> audio,
            HashSet<string> images, HashSet<string> videos) {
            var mode = (GameMode)beatmap.General["Mode"].IntValue;
            double tickRate = beatmap.Difficulty["SliderTickRate"].DoubleValue;
            string audioFilename = beatmap.General["AudioFilename"].Value.Trim();
            audio.Add(audioFilename);
            beatmap.General["AudioFilename"].Value = " " +
                                                       Path.Combine(subfolder,
                                                           audioFilename);
            foreach (var hitObject in beatmap.HitObjects) {
                hitsounds.UnionWith(hitObject.GetPlayingBodyFilenames(tickRate, false));
            }
            foreach (var timelineObject in beatmap.GetTimeline().TimelineObjects) {
                foreach (var filename in timelineObject.GetPlayingFilenames(mode, false)) {
                    if (!string.IsNullOrEmpty(filename) &&
                        filename == timelineObject.Filename) {
                        audio.Add(filename);
                        timelineObject.Filename = Path.Combine(subfolder, filename);
                        timelineObject.HitsoundsToOrigin();
                    } else {
                        hitsounds.Add(filename);
                    }
                }
            }
            foreach (var hitObject in beatmap.HitObjects.Where(o => o.CustomIndex != 0)) {
                if (!indices.ContainsKey(hitObject.CustomIndex)) {
                    indices[hitObject.CustomIndex] = indexStart++;
                }
                hitObject.CustomIndex = indices[hitObject.CustomIndex];
            }
            foreach (var point in beatmap.BeatmapTiming.Where(o => o.SampleIndex != 0)) {
                if (!indices.ContainsKey(point.SampleIndex)) {
                    indices[point.SampleIndex] = indexStart++;
                }
                point.SampleIndex = indices[point.SampleIndex];
            }
            UpdateReferences(beatmap.StoryBoard, subfolder, audio, images, videos);
        }

        private static IEnumerable<Tuple<string, Beatmap>> LoadBeatmaps(
            MapsetMergerVm.MapsetItem mapset) {
            var paths = Directory.GetFiles(mapset.Path, "*.osu",
                SearchOption.AllDirectories);
            if (paths.Length > MaxMapsetMaps) {
                throw new InvalidOperationException(
                    "Beatmap limit exceeded in mapset: " + mapset.Name);
            }
            return paths.Select(path => Tuple.Create(path,
                new BeatmapEditor(path).Beatmap));
        }

        private static IEnumerable<Tuple<string, StoryBoard>> LoadStoryboards(
            MapsetMergerVm.MapsetItem mapset) {
            var paths = Directory.GetFiles(mapset.Path, "*.osb",
                SearchOption.AllDirectories);
            if (paths.Length > MaxMapsetMaps) {
                throw new InvalidOperationException(
                    "Storyboard limit exceeded in mapset: " + mapset.Name);
            }
            return paths.Select(path => Tuple.Create(path,
                new StoryboardEditor(path).StoryBoard));
        }

        private static void ResolveDuplicateNames(
            ICollection<MapsetMergerVm.MapsetItem> mapsets) {
            var names = new HashSet<string>();
            foreach (var mapset in mapsets) {
                string original = mapset.Name;
                int suffix = 0;
                while (!names.Add(mapset.Name)) mapset.Name = original + ++suffix;
            }
        }
    }
}
