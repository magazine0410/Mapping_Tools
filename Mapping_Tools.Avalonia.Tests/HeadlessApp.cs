using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    /// <summary>
    /// Starts the real application once, with no screen, so the tests see the real
    /// styles, the real resource keys and the real control templates.
    /// </summary>
    [TestClass]
    public static class HeadlessApp {
        private static int started;

        [AssemblyInitialize]
        public static void Start(TestContext context) {
            if (Interlocked.Exchange(ref started, 1) == 1) return;

            AppBuilder.Configure<App>()
                .UseSkia()
                // Skia draws for real, so a broken template shows up as a draw fault.
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();
        }

        /// <summary>
        /// Draws the window, and fails the test if nothing came out. This is what makes
        /// the templates run.
        /// </summary>
        public static void Draw(global::Avalonia.Controls.Window window) {
            var frame = window.CaptureRenderedFrame();
            Assert.IsNotNull(frame, "The window drew nothing.");
        }

        /// <summary>Puts a control in a window of its own, and shows it.</summary>
        public static global::Avalonia.Controls.Window Show(global::Avalonia.Controls.Control child) {
            var window = new global::Avalonia.Controls.Window { Width = 900, Height = 420, Content = child };
            window.Show();
            return window;
        }
    }
}
