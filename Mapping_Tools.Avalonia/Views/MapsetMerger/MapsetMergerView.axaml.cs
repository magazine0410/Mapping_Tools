using System;
using System.ComponentModel;
using System.IO;
using Avalonia.Interactivity;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.MapsetMerger {
    public partial class MapsetMergerView : SingleRunMappingTool,
        ISavable<MapsetMergerVm> {
        public static readonly string ToolName = "Mapset Merger";
        public static readonly string ToolDescription =
            "Combine multiple mapsets into one and automatically resolve file conflicts.";

        public MapsetMergerView() {
            InitializeComponent();
            DataContext = new MapsetMergerVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public MapsetMergerVm ViewModel => (MapsetMergerVm)DataContext;
        private void Start_Click(object sender, RoutedEventArgs e) => RunTool();

        internal void RunTool() {
            if (!CanRun) return;
            if (ViewModel.Mapsets.Count == 0 ||
                string.IsNullOrWhiteSpace(ViewModel.ExportPath)) {
                CorePlatform.Dialogs.ShowMessage(
                    "Add at least one mapset and choose an export folder.",
                    "Nothing to merge");
                return;
            }
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = global::Mapping_Tools.Classes.Tools.MapsetMerger.Merge(
                (MapsetMergerVm)e.Argument, sender as BackgroundWorker);
        }

        public MapsetMergerVm GetSaveData() => ViewModel;
        public void SetSaveData(MapsetMergerVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "mapsetmergerproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Mapset Merger Projects");
    }
}
