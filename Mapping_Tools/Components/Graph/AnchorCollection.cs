using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Components.Graph.Interpolation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace Mapping_Tools.Components.Graph {
    public sealed class AnchorCollection : ObservableCollection<Anchor> {
        public event DependencyPropertyChangedEventHandler AnchorsChanged;

        public AnchorCollection() {
            CollectionChanged += OnCollectionChanged;
        }

        public AnchorCollection(IEnumerable<Anchor> anchors) : base(anchors) {
            CollectionChanged += OnCollectionChanged;
            InitAllAnchors();
        }

        public AnchorCollection(List<Anchor> anchors) : base(anchors) {
            CollectionChanged += OnCollectionChanged;
            InitAllAnchors();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e == null) return;

            if (e.NewItems != null) {
                foreach (var newItem in e.NewItems) {
                    var newAnchor = (Anchor) newItem;
                    newAnchor.GraphStateChangedEvent += AnchorOnGraphStateChangedEvent;
                }
            }

            if (e.OldItems != null) {
                foreach (var oldItem in e.OldItems) {
                    var oldAnchor = (Anchor) oldItem;
                    oldAnchor.GraphStateChangedEvent -= AnchorOnGraphStateChangedEvent;
                }
            }

            UpdateAnchorNeighbors();
        }

        private void InitAllAnchors() {
            foreach (var anchor in this) {
                anchor.GraphStateChangedEvent += AnchorOnGraphStateChangedEvent;
            }
            UpdateAnchorNeighbors();
        }

        private void AnchorOnGraphStateChangedEvent(object sender, DependencyPropertyChangedEventArgs e) {
            AnchorsChanged?.Invoke(sender, e);
        }

        public void UpdateAnchorNeighbors() {
            Anchor previousAnchor = null;
            foreach (var anchor in this) {
                anchor.PreviousAnchor = previousAnchor;
                if (previousAnchor != null) {
                    previousAnchor.NextAnchor = anchor;
                }

                previousAnchor = anchor;
            }
        }

        #region GraphValueGettingStuff

        public double GetValue(double x) {
            return GetValue(x, this);
        }

        public double GetDerivative(double x) {
            return GetDerivative(x, this);
        }

        public double GetIntegral(double t1, double t2) {
            return GetIntegral(t1, t2, this);
        }

        public static double GetValue(double x, IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetValue(x, anchors);

        public static double GetDerivative(double x, IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetDerivative(x, anchors);

        public static double GetIntegral(double t1, double t2, IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetIntegral(t1, t2, anchors);

        public static double GetMaxValue(IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetMaxValue(anchors);

        public static double GetMaxDerivative(IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetMaxDerivative(anchors);

        public static double GetMaxIntegral(IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetMaxIntegral(anchors);

        public static double GetMinValue(IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetMinValue(anchors);

        public static double GetMinDerivative(IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetMinDerivative(anchors);

        public static double GetMinIntegral(IReadOnlyList<IGraphAnchor> anchors) => AnchorMath.GetMinIntegral(anchors);


        #endregion

        #region OtherValueGettingStuff
        
        public double GetDistanceTraveled() {
            double distance = 0;
            IGraphAnchor previousAnchor = null;
            foreach (var anchor in this) {
                if (previousAnchor != null) {
                    distance += Math.Abs(GetValue(anchor.Pos.X) - GetValue(previousAnchor.Pos.X));
                }

                previousAnchor = anchor;
            }

            return distance;
        }

        // Note: This method is not accurate for segments passing through zero
        public double GetIntegralDistanceTraveled() {
            double distance = 0;
            IGraphAnchor previousAnchor = null;
            foreach (var anchor in this) {
                if (previousAnchor != null) {
                    distance += Math.Abs(GetIntegral(previousAnchor.Pos.X, anchor.Pos.X));
                }

                previousAnchor = anchor;
            }

            return distance;
        }

        #endregion
    }
}