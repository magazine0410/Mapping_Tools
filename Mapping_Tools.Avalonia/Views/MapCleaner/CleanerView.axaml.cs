using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools.MapCleanerStuff;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.MapCleaner {
    /// <summary>
    /// Map Cleaner, on Avalonia.
    /// </summary>
    /// <remarks>
    /// The run logic is the same as the WPF view. What is missing is the timeline that
    /// the WPF view draws under the tool, which shows which timing points were added,
    /// changed and removed. That control is not ported yet. See section 3.1 of
    /// LINUX_PORT.md.
    /// <para>
    /// Quick Run is left out as well. It needs the editor reader, which cannot work on
    /// Linux. See section 3.2.
    /// </para>
    /// </remarks>
    [VerticalContentScroll]
    [HorizontalContentScroll]
    public partial class CleanerView : SingleRunMappingTool, ISavable<MapCleanerVm> {
        public static readonly string ToolName = "Map Cleaner";

        public static readonly string ToolDescription =
            $@"It cleans the current map of useless greenlines and it also lets you do some other stuff regarding the whole map.{Environment.NewLine}Map cleaner cleans useless greenline stuff by storing all the influences of the timingpoints and then removing all the timingpoints and then rebuilding all the timingpoints in a good way. This means the greenlines automatically get resnapped to the objects that use them.";

        public CleanerView() {
            InitializeComponent();
            DataContext = new MapCleanerVm();
            ProjectManager.LoadProject(this, message: false);

            // It's important to see the results of map cleaner.
            Verbose = true;
        }

        public MapCleanerVm ViewModel => (MapCleanerVm)DataContext;

        private void Start_Click(object sender, RoutedEventArgs e) {
            RunTool(CorePlatform.FileDialogs.GetCurrentBeatmaps());
        }

        internal void RunTool(string[] paths, bool quick = false) {
            if (!CanRun) return;

            if (paths is null || paths.Length == 0) {
                CorePlatform.Dialogs.ShowMessage(
                    "Open a beatmap first, with File then Open beatmap.", "No beatmap");
                return;
            }

            // The box writes back when it loses the focus. A click on the run button
            // does not always take the focus away, so ask for the write here.
            BeatDivisorsBox.Commit();

            // The result is on purpose not read. A failed backup already tells the user
            // why, and then the run goes on. That is what the WPF host does, and the two
            // hosts must not disagree about it. See PORT_ROADMAP.md.
            BackupManager.SaveMapBackup(paths);

            ViewModel.Paths = paths;
            ViewModel.Quick = quick;

            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = RunProgram((MapCleanerVm)e.Argument, sender as BackgroundWorker);
        }

        /// <summary>
        /// Cleans every beatmap that was given, and says what it did.
        /// </summary>
        internal static string RunProgram(MapCleanerVm args, BackgroundWorker worker) {
            var result = new MapCleanerResult();
            var reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();

            foreach (var path in args.Paths) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader);

                int oldTimingPointsCount = editor.Beatmap.BeatmapTiming.TimingPoints.Count;

                // The full name: "Classes" alone hits StyledElement.Classes, and
                // "MapCleaner" alone hits the namespace of this view.
                result.Add(global::Mapping_Tools.Classes.Tools.MapCleanerStuff.MapCleaner.CleanMap(
                    editor, args.MapCleanerArgs, worker));

                result.TimingPointsRemoved +=
                    oldTimingPointsCount - editor.Beatmap.BeatmapTiming.TimingPoints.Count;

                editor.SaveFile();
            }

            return args.Quick ? string.Empty : Describe(result, args.MapCleanerArgs);
        }

        /// <summary>Says what the run did, in the plural or the singular.</summary>
        private static string Describe(MapCleanerResult result, MapCleanerArgs args) {
            int lines = Math.Abs(result.TimingPointsRemoved);

            var message =
                $"Successfully {(result.TimingPointsRemoved < 0 ? "added" : "removed")} " +
                $"{lines} {(lines == 1 ? "greenline" : "greenlines")}";

            if (args.ResnapObjects) {
                message += $" and resnapped {result.ObjectsResnapped} " +
                           $"{(result.ObjectsResnapped == 1 ? "object" : "objects")}";
            }

            if (args.RemoveUnusedSamples) {
                message += $" and removed {result.SamplesRemoved} unused " +
                           $"{(result.SamplesRemoved == 1 ? "sample" : "samples")}";
            }

            return message + "!";
        }

        public MapCleanerVm GetSaveData() => ViewModel;

        public void SetSaveData(MapCleanerVm saveData) => DataContext = saveData;

        public string AutoSavePath =>
            Path.Combine(CorePlatform.Paths.AppDataPath, "mapcleanerproject.json");

        public string DefaultSaveFolder =>
            Path.Combine(CorePlatform.Paths.AppDataPath, "Map Cleaner Projects");
    }
}
