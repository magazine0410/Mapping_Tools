using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.HitsoundStudio {
    [VerticalContentScroll]
    public partial class HitsoundStudioView : SingleRunMappingTool,
        ISavable<HitsoundStudioVm> {
        public static readonly string ToolName = "Hitsound Studio";
        public static readonly string ToolDescription =
            "Build hitsound layers from beatmaps, storyboard samples, MIDI, or manual times, then export an osu! hitsound difficulty and its samples.";

        public HitsoundStudioView() {
            InitializeComponent();
            DataContext = new HitsoundStudioVm();
            ProjectManager.LoadProject(this, message: false);
            Verbose = true;
        }

        public HitsoundStudioVm ViewModel => (HitsoundStudioVm)DataContext;

        private void BrowseBaseBeatmap_Click(object sender, RoutedEventArgs e) {
            var paths = CorePlatform.FileDialogs.BeatmapFileDialog();
            if (paths.Length > 0) ViewModel.BaseBeatmap = paths[0];
        }

        private void BrowseExportFolder_Click(object sender, RoutedEventArgs e) {
            string path = CorePlatform.FileDialogs.FolderDialog(ViewModel.ExportFolder);
            if (!string.IsNullOrWhiteSpace(path)) ViewModel.ExportFolder = path;
        }

        private async void BrowseSample_Click(object sender, RoutedEventArgs e) {
            string path = await PickFile("Select an audio sample or soundfont",
                "Audio", "*.wav", "*.ogg", "*.mp3", "*.sf2");
            if (!string.IsNullOrWhiteSpace(path) && ViewModel.SelectedLayer is not null) {
                ViewModel.SelectedLayer.SampleArgs.Path = path;
            }
        }

        private void PreviewSample_Click(object sender, RoutedEventArgs e) =>
            ViewModel.PreviewLayerCommand.Execute(null);

        private async void BrowseSource_Click(object sender, RoutedEventArgs e) {
            string path = await PickFile("Select an import source", "Beatmap or MIDI",
                "*.osu", "*.osb", "*.mid", "*.midi");
            if (!string.IsNullOrWhiteSpace(path) && ViewModel.SelectedLayer is not null) {
                ViewModel.SelectedLayer.ImportArgs.Path = path;
            }
        }

        private async Task<string> PickFile(string title, string typeName,
            params string[] patterns) {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return string.Empty;
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[] {
                    new FilePickerFileType(typeName) { Patterns = patterns }
                }
            });
            return files.Count == 0 ? string.Empty : files[0].TryGetLocalPath() ?? string.Empty;
        }

        private void ImportLayers_Click(object sender, RoutedEventArgs e) =>
            ViewModel.ImportLayersCommand.Execute(null);

        private void ReloadLayer_Click(object sender, RoutedEventArgs e) =>
            ViewModel.ReloadLayerCommand.Execute(null);

        private void Start_Click(object sender, RoutedEventArgs e) => RunTool();

        internal void RunTool() {
            if (!CanRun) return;
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            if (ViewModel.HitsoundLayers.Count == 0) {
                CorePlatform.Dialogs.ShowMessage("Add at least one hitsound layer.",
                    "No layers");
                return;
            }
            Directory.CreateDirectory(ViewModel.ExportFolder);
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) =>
            e.Result = global::Mapping_Tools.Classes.Tools.HitsoundStudioRunner.Export(
                (HitsoundStudioVm)e.Argument, sender as BackgroundWorker);

        public HitsoundStudioVm GetSaveData() => ViewModel;
        public void SetSaveData(HitsoundStudioVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "hsstudioproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Hitsound Studio Projects");
    }
}
