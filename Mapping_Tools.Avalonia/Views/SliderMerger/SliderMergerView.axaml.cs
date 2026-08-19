using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.SliderMerger {
    [VerticalContentScroll]
    public partial class SliderMergerView : SingleRunMappingTool,
        ISavable<SliderMergerVm> {
        public static readonly string ToolName = "Slider Merger";
        public static readonly string ToolDescription =
            "Merge adjacent sliders and circles into one slider when their endpoints are within the configured distance.";

        public SliderMergerView() {
            InitializeComponent();
            var vm = new SliderMergerVm();
            if (!CorePlatform.EditorReader.IsAvailable) {
                vm.ImportModeSetting = SliderMergerVm.ImportMode.Everything;
            }
            DataContext = vm;
            ProjectManager.LoadProject(this, message: false);
        }

        public SliderMergerVm ViewModel => (SliderMergerVm)DataContext;
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
            ViewModel.Paths = paths;
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) =>
            e.Result = global::Mapping_Tools.Classes.Tools.SliderMerger.Merge(
                (SliderMergerVm)e.Argument, sender as BackgroundWorker);

        public SliderMergerVm GetSaveData() => ViewModel;
        public void SetSaveData(SliderMergerVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "slidermergerproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Slider Merger Projects");
    }
}
