using System;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// The reason a value was refused, in a shape that shows well on a control.
    /// </summary>
    /// <remarks>
    /// Avalonia shows a data validation error by its <c>ToString</c>. A plain exception
    /// puts its type name in front of the message, which the user does not need.
    /// </remarks>
    public sealed class ValidationMessage : Exception {
        public ValidationMessage(string message) : base(message) { }

        public override string ToString() => Message;
    }
}
