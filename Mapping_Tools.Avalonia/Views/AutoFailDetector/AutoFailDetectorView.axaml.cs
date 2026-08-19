using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.AutoFailDetector {
    [VerticalContentScroll]
    public partial class AutoFailDetectorView : SingleRunMappingTool {
        public static readonly string ToolName = "Auto-fail Detector";
        public static readonly string ToolDescription =
            $@"Detects incorrect object loading that can prevent osu! from calculating scores.{Environment.NewLine}Auto-fail is usually caused by overlapping objects in 2B patterns. AR and OD overrides simulate difficulty mods.";

        private AutoFailAnalysis pendingAnalysis;
        private AutoFailAnalysis lastAnalysis;

        public AutoFailDetectorView() {
            InitializeComponent();
            DataContext = new AutoFailDetectorVm();
            Verbose = true;
        }

        public AutoFailDetectorVm ViewModel => (AutoFailDetectorVm)DataContext;
        public bool HasAnalysis => lastAnalysis is not null;
        public string AnalysisSummary => lastAnalysis?.Message ?? string.Empty;
        public string UnloadingSummary => DescribeTimes("Unloading", lastAnalysis?.Unloading);
        public string PotentialSummary => DescribeTimes("Potential", lastAnalysis?.Potential);
        public string DisruptorSummary => DescribeTimes("Disruptors", lastAnalysis?.Disruptors);

        private static string DescribeTimes(string label, IReadOnlyCollection<double> values) =>
            values is null || values.Count == 0
                ? $"{label}: none"
                : $"{label}: {string.Join(", ", values.Select(o => Math.Round(o)))} ms";

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
            ViewModel.Paths = paths;
            ViewModel.Quick = quick;
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            pendingAnalysis = Analyze((AutoFailDetectorVm)e.Argument,
                sender as BackgroundWorker);
            e.Result = pendingAnalysis.Message;
        }

        protected override void BackgroundWorker_RunWorkerCompleted(object sender,
            RunWorkerCompletedEventArgs e) {
            if (e.Error is null) {
                lastAnalysis = pendingAnalysis;
                RaisePropertyChanged(nameof(HasAnalysis));
                RaisePropertyChanged(nameof(AnalysisSummary));
                RaisePropertyChanged(nameof(UnloadingSummary));
                RaisePropertyChanged(nameof(PotentialSummary));
                RaisePropertyChanged(nameof(DisruptorSummary));
            }
            base.BackgroundWorker_RunWorkerCompleted(sender, e);
        }

        internal static AutoFailAnalysis Analyze(AutoFailDetectorVm args,
            BackgroundWorker worker) {
            var reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();
            var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(args.Paths[0], reader);
            var beatmap = editor.Beatmap;
            double ar = args.ApproachRateOverride == -1
                ? beatmap.Difficulty["ApproachRate"].DoubleValue
                : args.ApproachRateOverride;
            int approachTime = (int)Beatmap.GetApproachTime(ar);
            double od = args.OverallDifficultyOverride == -1
                ? beatmap.Difficulty["OverallDifficulty"].DoubleValue
                : args.OverallDifficultyOverride;
            int window50 = (int)Math.Ceiling(200 - 10 * od);
            var detector = new global::Mapping_Tools.Classes.Tools.AutoFailDetector(
                beatmap.HitObjects, (int)beatmap.GetMapStartTime(),
                (int)beatmap.GetMapEndTime(), (int)beatmap.GetAutoFailCheckTime(),
                approachTime, window50, args.PhysicsUpdateLeniency);
            bool autoFail = detector.DetectAutoFail();
            if (worker is { WorkerReportsProgress: true }) worker.ReportProgress(33);

            if (args.GetAutoFailFix && detector.AutoFailFixDialogue(args.AutoPlaceFix)) {
                editor.SaveFile();
            }
            if (worker is { WorkerReportsProgress: true }) worker.ReportProgress(100);

            string message = autoFail
                ? $"{detector.UnloadingObjects.Count} unloading objects detected and " +
                  $"{detector.PotentialUnloadingObjects.Count} potential unloading objects detected."
                : detector.PotentialUnloadingObjects.Count > 0
                    ? $"No auto-fail, but {detector.PotentialUnloadingObjects.Count} " +
                      "potential unloading objects detected."
                    : "No auto-fail detected.";
            return new AutoFailAnalysis(message,
                args.ShowUnloadingObjects ? detector.UnloadingObjects : new List<double>(),
                args.ShowPotentialUnloadingObjects
                    ? detector.PotentialUnloadingObjects
                    : new List<double>(),
                args.ShowPotentialDisruptors ? detector.Disruptors : new List<double>());
        }
    }

    internal sealed record AutoFailAnalysis(string Message, IReadOnlyList<double> Unloading,
        IReadOnlyList<double> Potential, IReadOnlyList<double> Disruptors);
}
