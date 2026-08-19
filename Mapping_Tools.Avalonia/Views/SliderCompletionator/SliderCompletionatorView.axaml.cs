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

namespace Mapping_Tools.Avalonia.Views.SliderCompletionator {
    [VerticalContentScroll]
    public partial class SliderCompletionatorView : SingleRunMappingTool,
        ISavable<SliderCompletionatorVm> {
        public static readonly string ToolName = "Slider Completionator";
        public static readonly string ToolDescription =
            "Change slider length or duration and calculate the remaining slider-velocity variable.";

        public SliderCompletionatorView() {
            InitializeComponent();
            var vm = new SliderCompletionatorVm();
            if (!CorePlatform.EditorReader.IsAvailable) {
                vm.ImportModeSetting = SliderCompletionatorVm.ImportMode.Everything;
            }
            DataContext = vm;
            ProjectManager.LoadProject(this, message: false);
        }

        public SliderCompletionatorVm ViewModel => (SliderCompletionatorVm)DataContext;
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
            e.Result = global::Mapping_Tools.Classes.Tools.SliderCompletionator.Complete(
                (SliderCompletionatorVm)e.Argument, sender as BackgroundWorker);

        public SliderCompletionatorVm GetSaveData() => ViewModel;
        public void SetSaveData(SliderCompletionatorVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "slidercompletionatorproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Slider Completionator Projects");
    }
}
