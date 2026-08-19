using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Avalonia.Views.RhythmGuide;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.HitsoundPreviewHelper {
    public partial class HitsoundPreviewHelperView : SingleRunMappingTool,
        ISavable<HitsoundPreviewHelperVm> {
        private string[] pendingPaths = Array.Empty<string>();

        public static readonly string ToolName = "Hitsound Preview Helper";
        public static readonly string ToolDescription =
            "Assign preview hitsounds to every object according to the nearest configured playfield zone.";

        public HitsoundPreviewHelperView() {
            InitializeComponent();
            var vm = new HitsoundPreviewHelperVm();
            vm.RhythmGuideRequested = OpenRhythmGuide;
            DataContext = vm;
            ProjectManager.LoadProject(this, message: false);
            ViewModel.RhythmGuideRequested = OpenRhythmGuide;
        }

        public HitsoundPreviewHelperVm ViewModel =>
            (HitsoundPreviewHelperVm)DataContext;

        private void OpenRhythmGuide() {
            var window = new Window {
                Title = "Rhythm Guide",
                Width = 900,
                Height = 650,
                Content = new RhythmGuideView()
            };
            window.Show();
        }

        private void Start_Click(object sender, RoutedEventArgs e) =>
            RunTool(CorePlatform.FileDialogs.GetCurrentBeatmaps());

        internal void RunTool(string[] paths) {
            if (!CanRun) return;
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            if (paths is null || paths.Length == 0) {
                CorePlatform.Dialogs.ShowMessage("Open a beatmap first.", "No beatmap");
                return;
            }
            BackupManager.SaveMapBackup(paths);
            pendingPaths = paths;
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) =>
            e.Result = global::Mapping_Tools.Classes.Tools.HitsoundPreviewHelper.Place(
                pendingPaths, ((HitsoundPreviewHelperVm)e.Argument).Items.ToList(),
                sender as BackgroundWorker);

        public HitsoundPreviewHelperVm GetSaveData() => ViewModel;
        public void SetSaveData(HitsoundPreviewHelperVm saveData) {
            saveData.RhythmGuideRequested = OpenRhythmGuide;
            DataContext = saveData;
        }
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "hspreviewproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Hitsound Preview Projects");
    }
}
