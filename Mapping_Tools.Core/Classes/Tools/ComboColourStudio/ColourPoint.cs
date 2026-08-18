using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mapping_Tools.Annotations;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Components.Domain;
using Newtonsoft.Json;

namespace Mapping_Tools.Classes.Tools.ComboColourStudio {
    public class ColourPoint : BindableBase, ICloneable {
        private double time;
        private ObservableCollection<SpecialColour> colourSequence;
        private ColourPointMode mode;
        private bool isSelected;
        private ComboColourProject parentProject;

        /// <summary>
        /// Opens the menu that adds a colour to the sequence.
        /// The host with the user interface sets this. The first argument is this
        /// colour point. The second argument is the control that the user clicked.
        /// </summary>
        /// <remarks>
        /// This was a WPF context menu, built here in the model. The host now builds it.
        /// </remarks>
        public static System.Action<ColourPoint, object> ShowAddColourMenu { get; set; }

        public ColourPoint() : this(0, new ObservableCollection<SpecialColour>(), ColourPointMode.Normal, null) {}

        public ColourPoint(double time, IEnumerable<SpecialColour> colourSequence, ColourPointMode mode, ComboColourProject parentProject) {
            Time = time;
            ColourSequence = new ObservableCollection<SpecialColour>(colourSequence);
            Mode = mode;
            ParentProject = parentProject;

            
            AddCommand = new CommandImplementation(sender => ShowAddColourMenu?.Invoke(this, sender));

            RemoveCommand = new CommandImplementation(item => {
                if (ColourSequence.Count == 0) return;
                if (item == null) {
                    ColourSequence.RemoveAt(ColourSequence.Count - 1);
                } else {
                    ColourSequence.Remove(item as SpecialColour);
                }
            });
        }

        public double Time {
            get => time;
            set => Set(ref time, value);
        }

        public ObservableCollection<SpecialColour> ColourSequence {
            get => colourSequence;
            set => Set(ref colourSequence, value);
        }

        public ColourPointMode Mode {
            get => mode;
            set => Set(ref mode, value);
        }

        [JsonIgnore]
        public bool IsSelected {
            get => isSelected;
            set => Set(ref isSelected, value);
        }

        [CanBeNull]
        [JsonIgnore]
        public ComboColourProject ParentProject {
            get => parentProject;
            set => Set(ref parentProject, value);
        }

        [JsonIgnore]
        public IEnumerable<ColourPointMode> ColourPointModes => Enum.GetValues(typeof(ColourPointMode)).Cast<ColourPointMode>();
        [JsonIgnore]
        public CommandImplementation AddCommand { get; }
        [JsonIgnore]
        public CommandImplementation RemoveCommand { get; }

        public object Clone() {
            var colours = new SpecialColour[ColourSequence.Count];
            for (int i = 0; i < ColourSequence.Count; i++) {
                colours[i] = (SpecialColour)ColourSequence[i].Clone();
            }
            return new ColourPoint(Time, colours, Mode, ParentProject);
        }
    }
}