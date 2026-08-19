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

namespace Mapping_Tools.Avalonia.Views.MetadataManager {
    [VerticalContentScroll]
    public partial class MetadataManagerView : SingleRunMappingTool,
        ISavable<MetadataManagerVm> {
        public static readonly string ToolName = "Metadata Manager";
        public static readonly string ToolDescription =
            "Import one beatmap's metadata, edit it once, and export it to multiple difficulties.";

        public MetadataManagerView() {
            InitializeComponent();
            DataContext = new MetadataManagerVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public MetadataManagerVm ViewModel => (MetadataManagerVm)DataContext;

        private void Start_Click(object sender, RoutedEventArgs e) => RunTool();
        internal void RunTool() {
            if (!CanRun) return;
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            var paths = (ViewModel.ExportPath ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (paths.Length == 0) {
                CorePlatform.Dialogs.ShowMessage("Choose at least one export beatmap.",
                    "No beatmap");
                return;
            }
            BackupManager.SaveMapBackup(paths);
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) =>
            e.Result = global::Mapping_Tools.Classes.Tools.MetadataManager.Apply(
                (MetadataManagerVm)e.Argument, sender as BackgroundWorker);

        public MetadataManagerVm GetSaveData() => ViewModel;
        public void SetSaveData(MetadataManagerVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "metadataproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Metadata Manager Projects");
    }
}
