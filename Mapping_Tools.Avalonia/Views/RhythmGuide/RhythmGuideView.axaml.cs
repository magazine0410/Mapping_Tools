using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.RhythmGuide {
    [VerticalContentScroll]
    public partial class RhythmGuideView : SingleRunMappingTool, ISavable<RhythmGuideVm> {
        public static readonly string ToolName = "Rhythm Guide";
        public static readonly string ToolDescription =
            $@"Make a beatmap with circles from the rhythm of multiple maps, as a reference for hitsounding.{Environment.NewLine}Add the circles to an existing map or generate a new map.";

        public RhythmGuideView() {
            InitializeComponent();
            DataContext = new RhythmGuideVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public RhythmGuideVm ViewModel => (RhythmGuideVm)DataContext;

        private void Start_Click(object sender, RoutedEventArgs e) => RunTool();

        internal void RunTool() {
            if (!CanRun) return;
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            var args = ViewModel.GuideGeneratorArgs;
            if (args.Paths is null || args.Paths.Length == 0 ||
                string.IsNullOrWhiteSpace(args.ExportPath)) {
                CorePlatform.Dialogs.ShowMessage(
                    "Choose at least one source map and an output map.", "Missing beatmap");
                return;
            }
            if (args.ExportMode == global::Mapping_Tools.Classes.Tools.RhythmGuide.ExportMode.AddToMap) {
                BackupManager.SaveMapBackup(args.ExportPath);
            }
            BackgroundWorker.RunWorkerAsync(args);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = RunProgram(
                (global::Mapping_Tools.Classes.Tools.RhythmGuide.RhythmGuideGeneratorArgs)e.Argument,
                sender as BackgroundWorker);
        }

        internal static string RunProgram(
            global::Mapping_Tools.Classes.Tools.RhythmGuide.RhythmGuideGeneratorArgs args,
            BackgroundWorker worker) {
            global::Mapping_Tools.Classes.Tools.RhythmGuide.GenerateRhythmGuide(args);
            UpdateProgressBar(worker, 100);
            return args.ExportMode ==
                   global::Mapping_Tools.Classes.Tools.RhythmGuide.ExportMode.NewMap
                ? string.Empty
                : "Done!";
        }

        public RhythmGuideVm GetSaveData() => ViewModel;
        public void SetSaveData(RhythmGuideVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "rhythmguideproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Rhythm Guide Projects");
    }
}
