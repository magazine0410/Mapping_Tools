using Mapping_Tools.Annotations;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Components.Graph.Interpolation;
using Mapping_Tools.Components.Graph.Interpolation.Interpolators;

namespace Mapping_Tools.Components.Graph {
    /// <summary>
    /// Holds the data of one anchor of a graph.
    /// </summary>
    /// <remarks>
    /// This class was a WPF <c>Freezable</c> with dependency properties. It is now a
    /// plain class. See <see cref="GraphState"/>.
    /// </remarks>
    public class AnchorState : IGraphAnchor {
        private Vector2 pos = Vector2.Zero;
        private IGraphInterpolator interpolator = new LinearInterpolator();
        private double tension;

        /// <summary>True after <see cref="Freeze"/>. A frozen object refuses changes.</summary>
        public bool IsFrozen { get; private set; }

        public Vector2 Pos {
            get => pos;
            set { ThrowIfFrozen(); pos = value; }
        }

        [NotNull]
        public IGraphInterpolator Interpolator {
            get => interpolator;
            set { ThrowIfFrozen(); interpolator = value; }
        }

        public double Tension {
            get => tension;
            set { ThrowIfFrozen(); tension = value; }
        }

        /// <summary>Always true. A plain object can always freeze.</summary>
        public bool CanFreeze => true;

        /// <summary>Stops all further changes to this object.</summary>
        public void Freeze() => IsFrozen = true;

        /// <summary>Makes a copy that is not frozen.</summary>
        public AnchorState Clone() {
            return new AnchorState { pos = pos, interpolator = interpolator, tension = tension };
        }

        private void ThrowIfFrozen() {
            if (IsFrozen)
                throw new System.InvalidOperationException("This AnchorState is frozen. You cannot change it.");
        }
    }
}
