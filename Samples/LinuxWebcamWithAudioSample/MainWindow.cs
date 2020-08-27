// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.LinuxWebcamWithAudioSample
{
    using System;
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
        private byte[] imageData = new byte[640 * 480 * 3];

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
            : base("Webcam with Audio Sample")
        {
            this.displayImage = new Gtk.Image();
            this.Add(this.displayImage);
            this.Shown += this.MainWindow_Shown;
            this.DeleteEvent += this.MainWindow_DeleteEvent;
        }

        private void MainWindow_Shown(object sender, EventArgs e)
        {
            // Create the \psi pipeline
            this.pipeline = Pipeline.Create();

            // Create the webcam component
            var webcam = new MediaCapture(this.pipeline, 640, 480);

            // Create the audio capture component
            var audio = new AudioCapture(this.pipeline, WaveFormat.Create16kHz1Channel16BitPcm());

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
                    var image = frame.Item1;
                    var audioLevel = frame.Item2;

                    this.DrawAudioLevel(image, audioLevel);

                    // Update the window image with the Psi image
                    var pixbuf = this.ImageToPixbuf(image);
                    Gtk.Application.Invoke((sender, e) => { this.displayImage.Pixbuf = pixbuf; });
                },
                DeliveryPolicy.LatestMessage);

            // Start the pipeline running
            this.pipeline.RunAsync();
        }

        private void DrawAudioLevel(Shared<Image> image, float audioLevel)
        {
            // clamp level to between 0 and 20
            audioLevel = audioLevel < 0 ? 0 : audioLevel > 20 ? 20 : audioLevel;

            // draw a green bar and text representing the audio level
            var rect = new System.Drawing.Rectangle(0, 10, (int)(audioLevel * image.Resource.Width / 20), 20);
            image.Resource.DrawRectangle(rect, System.Drawing.Color.Green, 20);
            image.Resource.DrawText($"Audio Level: {audioLevel:0.0}", new System.Drawing.Point(0, 0), System.Drawing.Color.White);
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
