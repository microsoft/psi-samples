// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.LinuxWebcamWithAudioSample
{
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
            var window = new MainWindow();
            window.Destroyed += (sender, e) => Gtk.Application.Quit();
            window.ShowAll();
            Gtk.Application.Run();
        }
    }
}
