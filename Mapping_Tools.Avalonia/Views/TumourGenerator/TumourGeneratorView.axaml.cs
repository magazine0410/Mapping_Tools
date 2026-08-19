using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools.TumourGenerating.Options;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.TumourGenerator {
    public partial class TumourGeneratorView : SingleRunMappingTool,
        ISavable<TumourGeneratorVm> {
        public static readonly string ToolName = "Tumour Generator 2";
        public static readonly string ToolDescription =
            "Generate configurable repeating shapes along sliders, with multiple composable layers and a live preview.";

        public TumourGeneratorView() {
            InitializeComponent();
            SetSaveData(new TumourGeneratorVm());
            ProjectManager.LoadProject(this, message: false);
        }

        public TumourGeneratorVm ViewModel => (TumourGeneratorVm)DataContext;

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
            e.Result = global::Mapping_Tools.Classes.Tools.TumourGeneratorRunner.Generate(
                (TumourGeneratorVm)e.Argument, sender as BackgroundWorker);

        public TumourGeneratorVm GetSaveData() {
            foreach (var layer in ViewModel.TumourLayers) layer.Freeze();
            return ViewModel;
        }

        public void SetSaveData(TumourGeneratorVm saveData) {
            if (saveData.TumourLayers.Count == 0) {
                saveData.TumourLayers.Add(TumourLayer.GetDefaultLayer());
            }
            if (!CorePlatform.EditorReader.IsAvailable &&
                saveData.ImportModeSetting == TumourGeneratorVm.ImportMode.Selected) {
                saveData.ImportModeSetting = TumourGeneratorVm.ImportMode.Everything;
            }
            saveData.CurrentLayerIndex = 0;
            saveData.CurrentLayer = saveData.TumourLayers[0];
            DataContext = saveData;
            saveData.RegeneratePreview();
        }

        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "tumourgeneratorproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Tumour Generator Projects");
    }
}
