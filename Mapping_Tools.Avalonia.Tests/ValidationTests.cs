using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Data;
using Mapping_Tools.Components;
using Mapping_Tools.Components.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    /// <summary>
    /// A bad value must stop at the control. It must never reach the tool, and the user
    /// must be told why.
    /// </summary>
    [TestClass]
    public class ValidationTests {
        private static ValidatedTextBox Bound(Holder source, params ValidationRule[] rules) {
            var box = new ValidatedTextBox {
                DataContext = source,
                Width = 220,
                UpdateOnLostFocus = false,
                Converter = new DoubleToStringConverter()
            };
            box.Rules.AddRange(rules);
            box.Bind(ValidatedTextBox.ValueProperty, new Binding("Bpm") { Mode = BindingMode.TwoWay });
            HeadlessApp.Show(box);
            return box;
        }

        [TestMethod]
        public void TheBoxShowsWhatTheSourceHolds() {
            var box = Bound(new Holder { Bpm = 180 });
            Assert.AreEqual("180", box.Text);
        }

        [TestMethod]
        public void AValueThatBreaksARuleStaysOutOfTheSource() {
            var source = new Holder { Bpm = 180 };
            var box = Bound(source, new IsGreaterRule { Value = 0 });

            box.Text = "-5";

            Assert.IsTrue(DataValidationErrors.GetHasErrors(box), "The box was not marked.");
            Assert.AreEqual(180, source.Bpm, 1e-9, "A bad value reached the source.");
        }

        [TestMethod]
        public void TheBoxShowsTheReasonWithoutTheNameOfTheException() {
            var box = Bound(new Holder { Bpm = 180 }, new IsGreaterRule { Value = 0 });

            box.Text = "-5";

            var shown = string.Join(" | ", DataValidationErrors.GetErrors(box) ?? Array.Empty<object>());
            Assert.AreEqual("Value must be greater than 0.", shown);
        }

        [TestMethod]
        public void TextThatIsNotANumberStaysOutOfTheSource() {
            var source = new Holder { Bpm = 180 };
            var box = Bound(source);

            box.Text = "not a number";

            Assert.IsTrue(DataValidationErrors.GetHasErrors(box), "The box was not marked.");
            Assert.AreEqual(180, source.Bpm, 1e-9);
        }

        [TestMethod]
        public void AGoodValueReachesTheSourceAndClearsTheMark() {
            var source = new Holder { Bpm = 180 };
            var box = Bound(source, new IsGreaterRule { Value = 0 });

            box.Text = "-5";
            box.Text = "240";

            Assert.IsFalse(DataValidationErrors.GetHasErrors(box), "The mark stayed after a good value.");
            Assert.AreEqual(240, source.Bpm, 1e-9);
        }

        [TestMethod]
        public void AChangeAtTheSourceReachesTheBox() {
            var source = new Holder { Bpm = 180 };
            var box = Bound(source);

            source.Bpm = 128;

            Assert.AreEqual("128", box.Text);
        }

        [TestMethod]
        public void TheBoxWaitsForTheFocusToLeaveWhenItIsAskedTo() {
            var source = new Holder { Bpm = 180 };
            var box = Bound(source);
            box.UpdateOnLostFocus = true;

            box.Text = "240";
            Assert.AreEqual(180, source.Bpm, 1e-9, "The write did not wait.");

            box.Commit();
            Assert.AreEqual(240, source.Bpm, 1e-9);
        }

        [TestMethod]
        public void EveryRuleReportsInTheWordsTheWpfViewsUse() {
            Assert.AreEqual("Field is required.", new NotEmptyRule().Validate("  ", null));
            Assert.IsNull(new NotEmptyRule().Validate("x", null));

            Assert.AreEqual("Field is not ASCII.", new IsAsciiRule().Validate("Aimer - 蝶々結び", null));
            Assert.IsNull(new IsAsciiRule().Validate("Aimer - Chouchou Musubi", null));

            var limit = new CharacterLimitRule { Limit = 3 };
            Assert.AreEqual("Field can not be over 3 characters long.", limit.Validate("abcd", null));
            Assert.IsNull(limit.Validate("abc", null));

            Assert.AreEqual("Value can not be less than 0.",
                new IsGreaterOrEqualRule { Value = 0 }.Validate("-1", null));
            Assert.AreEqual("Value can not be greater than 10.",
                new IsLessOrEqualRule { Value = 10 }.Validate("11", null));
            Assert.AreEqual("Value must be less than 10.",
                new IsLessRule { Value = 10 }.Validate("10", null));
            Assert.AreEqual("Double format error.",
                new IsGreaterRule { Value = 0 }.Validate("apple", null));

            Assert.IsNull(new ParsableDoubleListRule().Validate("1,2.5,3", null));
            Assert.AreEqual("Field cannot be parsed.", new ParsableDoubleListRule().Validate("1;2", null));
        }

        private class Holder : INotifyPropertyChanged {
            private double bpm;

            public double Bpm {
                get => bpm;
                set {
                    if (Math.Abs(bpm - value) < double.Epsilon) return;
                    bpm = value;
                    Raise();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void Raise([CallerMemberName] string name = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
