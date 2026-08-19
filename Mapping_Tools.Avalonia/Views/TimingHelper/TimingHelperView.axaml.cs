using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.TimingHelper {
    [VerticalContentScroll]
    [HorizontalContentScroll]
    public partial class TimingHelperView : SingleRunMappingTool, ISavable<TimingHelperVm> {
        public static readonly string ToolName = "Timing Helper";
        public static readonly string ToolDescription =
            $@"Timing Helper speeds up timing by placing redlines for you.{Environment.NewLine}Place markers exactly on sounds as hit objects, bookmarks, greenlines or redlines. Timing Helper adjusts BPM and adds redlines to snap every marker.";

        public TimingHelperView() {
            InitializeComponent();
            DataContext = new TimingHelperVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public TimingHelperVm ViewModel => (TimingHelperVm)DataContext;

        private void Start_Click(object sender, RoutedEventArgs e) =>
            RunTool(CorePlatform.FileDialogs.GetCurrentBeatmaps());

        internal void RunTool(string[] paths, bool quick = false) {
            if (!CanRun) return;
            if (paths is null || paths.Length == 0) {
                CorePlatform.Dialogs.ShowMessage("Open a beatmap first.", "No beatmap");
                return;
            }
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            BackupManager.SaveMapBackup(paths);
            ViewModel.Paths = paths;
            ViewModel.Quick = quick;
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = RunProgram((TimingHelperVm)e.Argument, sender as BackgroundWorker);
        }

        internal static string RunProgram(TimingHelperVm args, BackgroundWorker worker) {
            int redlinesAdded = 0;
            var reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();

            foreach (string path in args.Paths ?? Array.Empty<string>()) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader);
                var beatmap = editor.Beatmap;
                var timing = beatmap.BeatmapTiming;
                var markers = new List<Marker>();

                if (args.Objects) markers.AddRange(beatmap.HitObjects.Select(o => new Marker(o.Time)));
                if (args.Bookmarks) markers.AddRange(beatmap.GetBookmarks().Select(o => new Marker(o)));
                if (args.Greenlines) markers.AddRange(timing.TimingPoints.Where(o => !o.Uninherited).Select(o => new Marker(o.Offset)));
                if (args.Redlines) markers.AddRange(timing.TimingPoints.Where(o => o.Uninherited).Select(o => new Marker(o.Offset)));

                UpdateProgressBar(worker, 20);
                markers = markers.OrderBy(o => o.Time).ToList();

                if (!timing.TimingPoints.Any(o => o.Uninherited)) {
                    timing.Add(new TimingPoint(0, 1000, 4, SampleSet.Soft, 0, 100,
                        true, false, false));
                }

                markers = markers.Where((marker, index) => index == 0 ||
                    Math.Abs(marker.Time - markers[index - 1].Time) >=
                    args.Leniency + Precision.DoubleEpsilon).ToList();

                foreach (var marker in markers) {
                    var redline = timing.GetRedlineAtTime(marker.Time - 1);
                    var resnapped = timing.Resnap(marker.Time, args.BeatDivisors, false,
                        tp: redline);
                    var beatsFromRedline = (resnapped - redline.Offset) / redline.MpB;

                    if (MathHelper.ApproximatelyEquivalent(beatsFromRedline, 0, 0.0001)) {
                        beatsFromRedline = args.BeatDivisors.Min(o => o.GetValue());
                    }
                    if (marker.Time == redline.Offset) beatsFromRedline = 0;

                    var beatsFromLast = beatsFromRedline;
                    var earlier = markers.Where(o => o.Time < marker.Time &&
                        o.Time > redline.Offset).ToList();
                    if (earlier.Count > 0) {
                        var lastTime = earlier.Last().Time;
                        var lastResnapped = timing.Resnap(lastTime, args.BeatDivisors, false);
                        beatsFromLast = (resnapped - lastResnapped) / redline.MpB;
                        if (MathHelper.ApproximatelyEquivalent(beatsFromLast, 0, 0.0001)) {
                            beatsFromLast = args.BeatDivisors.Min(o => o.GetValue());
                        }
                        if (lastTime == marker.Time) beatsFromLast = 0;
                    }
                    marker.BeatsFromLastMarker = args.BeatsBetween == -1
                        ? beatsFromLast
                        : args.BeatsBetween;
                }

                if (!args.Redlines) {
                    var first = timing.TimingPoints.FirstOrDefault(o => o.Uninherited);
                    timing.RemoveAll(o => o.Uninherited && o != first);
                }

                UpdateProgressBar(worker, 40);

                for (int index = 0; index < markers.Count; index++) {
                    var marker = markers[index];
                    var redline = timing.GetRedlineAtTime(marker.Time - 1);
                    if (marker.BeatsFromLastMarker == 0) continue;

                    var before = markers.Where(o => o.Time < marker.Time &&
                        o.Time > redline.Offset).ToList();
                    before.Add(marker);

                    double mpb = 0;
                    double beats = 0;
                    foreach (var previous in before) {
                        beats += previous.BeatsFromLastMarker;
                        mpb += GetMpB(previous.Time - redline.Offset, beats, 0);
                    }
                    mpb /= before.Count;

                    if (CheckMpB(mpb, before, redline, args)) {
                        redline.MpB = HumanRoundMpB(mpb, before, redline, args);
                    } else {
                        before.Remove(marker);
                        var lastTime = before.Last().Time;
                        var added = redline.Copy();
                        var hitsounds = timing.GetTimingPointAtTime(lastTime + 5);
                        added.Offset = lastTime;
                        added.OmitFirstBarLine = args.OmitBarline;
                        added.Kiai = hitsounds.Kiai;
                        added.SampleIndex = hitsounds.SampleIndex;
                        added.SampleSet = hitsounds.SampleSet;
                        added.Volume = hitsounds.Volume;
                        added.MpB = GetMpB(marker.Time - lastTime,
                            marker.BeatsFromLastMarker, args.Leniency);
                        timing.Add(added);
                        redlinesAdded++;
                    }

                    UpdateProgressBar(worker, index * 60 / markers.Count + 40);
                }

                editor.SaveFile();
            }

            UpdateProgressBar(worker, 100);
            return args.Quick ? string.Empty : $"Successfully added {redlinesAdded} redlines!";
        }

        private static bool CheckMpB(double candidate, IEnumerable<Marker> markers,
            TimingPoint redline, TimingHelperVm args) {
            double old = redline.MpB;
            double beats = 0;
            foreach (var marker in markers) {
                beats += marker.BeatsFromLastMarker;
                redline.MpB = candidate;
                var snapped = redline.Offset + redline.MpB * beats;
                var snappedBeats = (snapped - redline.Offset) / redline.MpB;
                redline.MpB = old;
                if (!MathHelper.ApproximatelyEquivalent(snappedBeats, beats, 0.1) ||
                    !IsSnapped(marker.Time, snapped, args.Leniency)) return false;
            }
            return true;
        }

        private static double HumanRoundMpB(double mpb, IReadOnlyCollection<Marker> markers,
            TimingPoint redline, TimingHelperVm args) {
            double bpm = 60000 / mpb;
            foreach (var scale in new[] { 1d, 2d, 10d, 100d, 1000d }) {
                var candidate = 60000 / (Math.Round(bpm * scale) / scale);
                if (CheckMpB(candidate, markers, redline, args)) return candidate;
            }
            return mpb;
        }

        private static double GetMpB(double time, double beats, double leniency) {
            double mpb = time / beats;
            double bpm = 60000 / mpb;
            foreach (var scale in new[] { 1d, 2d, 10d, 100d, 1000d }) {
                var candidate = 60000 / (Math.Round(bpm * scale) / scale);
                if (IsSnapped(time, candidate * beats, leniency)) return candidate;
            }
            return mpb;
        }

        private static bool IsSnapped(double time, double snapped, double leniency = 3) =>
            Math.Abs(snapped - time) <= leniency;

        private sealed class Marker {
            public Marker(double time) => Time = time;
            public double Time { get; }
            public double BeatsFromLastMarker { get; set; }
        }

        public TimingHelperVm GetSaveData() => ViewModel;
        public void SetSaveData(TimingHelperVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "timinghelperproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Timing Helper Projects");
    }
}
