using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools.ComboColourStudio;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.ComboColourStudio {
    public partial class ComboColourStudioView : SingleRunMappingTool,
        ISavable<ComboColourProject> {
        public static readonly string ToolName = "Combo Colour Studio";
        public static readonly string ToolDescription =
            $@"Customize combo colours and colour haxing in a beatmap.{Environment.NewLine}Define colour points like timing points, then choose the combo-colour sequence used after each point.";

        public ComboColourStudioView() {
            InitializeComponent();
            DataContext = new ComboColourStudioVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public ComboColourStudioVm ViewModel => (ComboColourStudioVm)DataContext;

        private void ImportColours_Click(object sender, RoutedEventArgs e) {
            var paths = CorePlatform.FileDialogs.BeatmapFileDialog();
            if (paths.Length > 0) ViewModel.Project.ImportComboColoursFromBeatmap(paths[0]);
        }

        private void ImportColourHax_Click(object sender, RoutedEventArgs e) {
            var paths = CorePlatform.FileDialogs.BeatmapFileDialog();
            if (paths.Length > 0) ViewModel.Project.ImportColourHaxFromBeatmap(paths[0]);
        }

        private void AddSequenceColour_Click(object sender, RoutedEventArgs e) {
            if (sender is not Button { DataContext: ColourPoint point } button) return;
            var menu = new ContextMenu();
            foreach (var colour in ViewModel.Project.ComboColours) {
                var item = new MenuItem { Header = colour.Name };
                item.Click += (_, _) => point.ColourSequence.Add(colour);
                menu.Items.Add(item);
            }
            menu.Open(button);
        }

        private void Start_Click(object sender, RoutedEventArgs e) =>
            RunTool(CorePlatform.FileDialogs.GetCurrentBeatmaps());

        internal void RunTool(string[] paths) {
            if (!CanRun) return;
            if (paths is null || paths.Length == 0) {
                CorePlatform.Dialogs.ShowMessage("Open a beatmap first.", "No beatmap");
                return;
            }
            BackupManager.SaveMapBackup(paths);
            ViewModel.ExportPath = string.Join('|', paths);
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = RunProgram((ComboColourStudioVm)e.Argument,
                sender as BackgroundWorker);
        }

        internal static string RunProgram(ComboColourStudioVm args,
            BackgroundWorker worker) {
            var paths = (args.ExportPath ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries);
            int mapsDone = 0;
            var points = args.Project.ColourPoints.OrderBy(o => o.Time).ToList();
            var colours = args.Project.ComboColours.OrderBy(o => o.Name).ToList();
            if (colours.Count == 0) {
                throw new InvalidOperationException("Add at least one combo colour first.");
            }
            var reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();

            foreach (var path in paths) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader);
                var beatmap = editor.Beatmap;
                beatmap.ComboColours = new List<ComboColour>(args.Project.ComboColours);

                if (beatmap.HitObjects.Count > 0 && points.Count > 0) {
                    int lastPointIndex = -1;
                    var lastPoint = points[0];
                    int lastColourIndex = 0;
                    var exceptions = new List<ColourPoint>();
                    foreach (var newCombo in beatmap.HitObjects
                                 .Where(o => o.ActualNewCombo && !o.IsSpinner)) {
                        int comboLength = GetComboLength(newCombo, beatmap.HitObjects);
                        var point = GetColourPoint(points, newCombo.Time, exceptions,
                            comboLength <= args.Project.MaxBurstLength);
                        var sequence = point.ColourSequence.ToList();
                        if (point.Mode == ColourPointMode.Burst) exceptions.Add(point);

                        lastPointIndex = lastPointIndex == -1 || lastPoint.Equals(point)
                            ? lastPointIndex
                            : sequence.FindIndex(o => o.Name == colours[lastColourIndex].Name);
                        int pointIndex = lastPointIndex == -1 || sequence.Count == 0
                            ? 0
                            : lastPoint.Equals(point)
                                ? MathHelper.Mod(lastPointIndex + 1, sequence.Count)
                                : lastPointIndex == 0 && sequence.Count > 1 ? 1 : 0;
                        int colourIndex = sequence.Count == 0
                            ? MathHelper.Mod(lastColourIndex + 1, colours.Count)
                            : colours.FindIndex(o => o.Name == sequence[pointIndex].Name);
                        if (colourIndex < 0) {
                            throw new InvalidOperationException(
                                $"Colour {sequence[pointIndex].Name} at {point.Time} is not in the combo palette.");
                        }
                        int increase = MathHelper.Mod(colourIndex - lastColourIndex,
                            args.Project.ComboColours.Count);
                        newCombo.ComboSkip = MathHelper.Mod(increase - 1,
                            args.Project.ComboColours.Count);
                        if (!newCombo.NewCombo && newCombo.ComboSkip != 0) {
                            newCombo.NewCombo = true;
                        }
                        lastPointIndex = pointIndex;
                        lastPoint = point;
                        lastColourIndex = colourIndex;
                    }
                }

                editor.SaveFile();
                mapsDone++;
                if (worker is { WorkerReportsProgress: true }) {
                    worker.ReportProgress(mapsDone * 100 / paths.Length);
                }
            }
            return $"Successfully exported colours to {mapsDone} " +
                   $"{(mapsDone == 1 ? "beatmap" : "beatmaps")}.";
        }

        private static int GetComboLength(HitObject start, List<HitObject> hitObjects) {
            int index = hitObjects.IndexOf(start);
            if (index < 0) return 0;
            int count = 1;
            while (++index < hitObjects.Count) {
                if (hitObjects[index].NewCombo) return count;
                count++;
            }
            return count;
        }

        private static ColourPoint GetColourPoint(IReadOnlyList<ColourPoint> points,
            double time, IReadOnlyCollection<ColourPoint> exceptions, bool includeBurst) =>
            points.Except(exceptions).LastOrDefault(o => o.Time <= time + 5 &&
                (o.Mode != ColourPointMode.Burst || o.Time >= time - 5 && includeBurst)) ??
            points.Except(exceptions).FirstOrDefault(o => o.Mode != ColourPointMode.Burst) ??
            points[0];

        public ComboColourProject GetSaveData() => ViewModel.Project;
        public void SetSaveData(ComboColourProject saveData) => ViewModel.Project = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "combocolourproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Combo Colour Studio Projects");
    }
}
