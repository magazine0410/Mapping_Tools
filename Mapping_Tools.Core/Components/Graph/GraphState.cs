using System.Collections.Generic;
using System.Linq;

namespace Mapping_Tools.Components.Graph {
    /// <summary>
    /// Holds all the defining data of a graph. Use it for serialization, or to move
    /// the data of a graph between threads.
    /// </summary>
    /// <remarks>
    /// This class was a WPF <c>Freezable</c> with dependency properties. It is now a
    /// plain class, so that the core can use it on Linux. <see cref="Freeze"/> keeps
    /// the same guarantee: after a freeze, the object refuses all changes.
    /// </remarks>
    public class GraphState {
        private List<AnchorState> anchors;
        private double minX, minY, maxX = 1, maxY = 1;

        /// <summary>True after <see cref="Freeze"/>. A frozen object refuses changes.</summary>
        public bool IsFrozen { get; private set; }

        public List<AnchorState> Anchors {
            get => anchors;
            set { ThrowIfFrozen(); anchors = value; }
        }

        public double MinX {
            get => minX;
            set { ThrowIfFrozen(); minX = value; }
        }

        public double MinY {
            get => minY;
            set { ThrowIfFrozen(); minY = value; }
        }

        public double MaxX {
            get => maxX;
            set { ThrowIfFrozen(); maxX = value; }
        }

        public double MaxY {
            get => maxY;
            set { ThrowIfFrozen(); maxY = value; }
        }

        /// <summary>Always true. A plain object can always freeze.</summary>
        public bool CanFreeze => true;

        /// <summary>Stops all further changes to this object and to its anchors.</summary>
        public void Freeze() {
            if (IsFrozen) return;
            anchors?.ForEach(a => a.Freeze());
            IsFrozen = true;
        }

        /// <summary>Makes a copy that is not frozen.</summary>
        public GraphState Clone() {
            return new GraphState {
                anchors = anchors?.Select(a => a.Clone()).ToList(),
                minX = minX, minY = minY, maxX = maxX, maxY = maxY
            };
        }

        private void ThrowIfFrozen() {
            if (IsFrozen)
                throw new System.InvalidOperationException("This GraphState is frozen. You cannot change it.");
        }

        public double GetValue(double x) {
            return AnchorMath.GetValue(x, Anchors);
        }

        public double GetDerivative(double x) {
            return AnchorMath.GetDerivative(x, Anchors);
        }

        public double GetIntegral(double t1, double t2) {
            return AnchorMath.GetIntegral(t1, t2, Anchors);
        }
    }
}
