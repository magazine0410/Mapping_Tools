using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mapping_Tools.Classes;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Components.Domain;
using Newtonsoft.Json;

namespace Mapping_Tools.Viewmodels {
    public class HitsoundPreviewHelperVm : BindableBase {
        private ObservableCollection<HitsoundZone> items = new();
        private bool? isAllItemsSelected = false;

        public HitsoundPreviewHelperVm() {
            RhythmGuideCommand = new CommandImplementation(_ =>
                RhythmGuideRequested?.Invoke());
            AddCommand = new CommandImplementation(_ => Items.Add(new HitsoundZone {
                Name = $"Zone {Items.Count + 1}"
            }));
            CopyCommand = new CommandImplementation(_ => {
                foreach (var item in Items.Where(o => o.IsSelected).ToList()) {
                    var copy = item.Copy();
                    copy.IsSelected = false;
                    copy.Name += " (Copy)";
                    Items.Add(copy);
                }
            });
            RemoveCommand = new CommandImplementation(_ =>
                Items.RemoveAll(o => o.IsSelected));
        }

        public ObservableCollection<HitsoundZone> Items {
            get => items;
            set => Set(ref items, value ?? new ObservableCollection<HitsoundZone>());
        }

        [JsonIgnore]
        public bool? IsAllItemsSelected {
            get => isAllItemsSelected;
            set {
                if (!Set(ref isAllItemsSelected, value)) return;
                if (value.HasValue) {
                    foreach (var item in Items) item.IsSelected = value.Value;
                }
            }
        }

        [JsonIgnore] public Action RhythmGuideRequested { get; set; }
        [JsonIgnore] public CommandImplementation RhythmGuideCommand { get; }
        [JsonIgnore] public CommandImplementation AddCommand { get; }
        [JsonIgnore] public CommandImplementation CopyCommand { get; }
        [JsonIgnore] public CommandImplementation RemoveCommand { get; }
    }
}
