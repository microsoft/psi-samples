// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.LinuxWebcamWithAudioSample
{
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Webcam with audio sample program.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            Gtk.Application.Init();
            InitializeCssStyles();
            var window = new MainWindow();
            window.Destroyed += (sender, e) => Gtk.Application.Quit();
            window.ShowAll();
            Gtk.Application.Run();
        }

        private static void InitializeCssStyles()
        {
            var styleProvider = new Gtk.CssProvider();
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Psi.Samples.LinuxWebcamWithAudioSample.Styles.css");
            using StreamReader reader = new StreamReader(stream);

            styleProvider.LoadFromData(reader.ReadToEnd());
            Gtk.StyleContext.AddProviderForScreen(Gdk.Display.Default.DefaultScreen, styleProvider, Gtk.StyleProviderPriority.Application);
        }
    }
}
