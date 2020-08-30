// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.LinuxWebcamWithAudioSample
{
    using System;
    using System.IO;
    using System.Reflection;
    using Gdk;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Media;

    /// <summary>
    /// Webcam with audio sample program.
    /// </summary>
    public class MainWindow : Gtk.Window
    {
        private Pipeline pipeline;
        private Gtk.Image displayImage;
        private Gtk.Label displayText;
        private Gtk.LevelBar displayLevel;
        private byte[] imageData = new byte[640 * 480 * 3];

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
            : base("Webcam with Audio Sample")
        {
            // create the window widgets from the MainWindow.xml resource using the builder
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Psi.Samples.LinuxWebcamWithAudioSample.MainWindow.xml");
            using StreamReader reader = new StreamReader(stream);
            var builder = new Gtk.Builder();
            builder.AddFromString(reader.ReadToEnd());
            this.Add((Gtk.Widget)builder.GetObject("root"));

            // get the widgets which we will modify
            this.displayImage = (Gtk.Image)builder.GetObject("image");
            this.displayText = (Gtk.Label)builder.GetObject("value");
            this.displayLevel = (Gtk.LevelBar)builder.GetObject("level");

            // window event handlers
            this.Shown += this.MainWindow_Shown;
            this.DeleteEvent += this.MainWindow_DeleteEvent;
        }

        private void MainWindow_Shown(object sender, EventArgs e)
        {
            // Create the \psi pipeline
            this.pipeline = Pipeline.Create();

            // Create the webcam component
            var webcam = new MediaCapture(this.pipeline, 640, 480, "/dev/video0", PixelFormatId.YUYV);

            // Create the audio capture component
            var audio = new AudioCapture(this.pipeline, new AudioCaptureConfiguration { DeviceName = "plughw:0,0", Format = WaveFormat.Create16kHz1Channel16BitPcm() });

            // Create an acoustic features extractor component and pipe the audio to it
            var acousticFeatures = new AcousticFeaturesExtractor(this.pipeline);
            audio.PipeTo(acousticFeatures);

            // Fuse the webcam images with the audio log energy level
            var webcamWithAudioEnergy = webcam.Join(acousticFeatures.LogEnergy, RelativeTimeInterval.Past());

            // Overlay the audio energy on the webcam image and display it in the window.
            // The "Do" operator is executed on each fused webcam and audio energy sample.
            webcamWithAudioEnergy.Do(
                frame =>
                {
                    // Update the window with the latest frame
                    this.DrawFrame(frame);
                },
                DeliveryPolicy.LatestMessage);

            // Start the pipeline running
            this.pipeline.RunAsync();
        }

        private void DrawFrame((Shared<Image> Image, float AudioLevel) frame)
        {
            // copy the frame image to the pixel buffer
            var pixbuf = this.ImageToPixbuf(frame.Image);

            // clamp level to between 0 and 20
            var audioLevel = frame.AudioLevel < 0 ? 0 : frame.AudioLevel > 20 ? 20 : frame.AudioLevel;

            // redraw on the UI thread
            Gtk.Application.Invoke(
                (sender, e) =>
                {
                    this.displayImage.Pixbuf = pixbuf;
                    this.displayLevel.Value = audioLevel;
                    this.displayText.Text = audioLevel.ToString("0.0");
                });
        }

        private Pixbuf ImageToPixbuf(Shared<Image> image)
        {
            var length = image.Resource.Stride * image.Resource.Height;
            if (this.imageData.Length != length)
            {
                this.imageData = new byte[length];
            }

            image.Resource.CopyTo(this.imageData);
            return new Pixbuf(this.imageData, false, 8, image.Resource.Width, image.Resource.Height, image.Resource.Stride);
        }

        private void MainWindow_DeleteEvent(object o, Gtk.DeleteEventArgs args)
        {
            this.pipeline.Dispose();
        }
    }
}
