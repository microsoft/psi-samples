// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.WebcamWithAudioSample
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Media;

    /// <summary>
    /// Webcam with audio sample program.
    /// </summary>
    public partial class MainWindow
    {
        private Pipeline pipeline;
        private WriteableBitmap bitmap;
        private IntPtr bitmapPtr;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();
            this.Loaded += this.MainWindow_Loaded;
            this.Closing += this.MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Create the \psi pipeline
            this.pipeline = Pipeline.Create("WebcamWithAudioSample");

            // Create the webcam component
            var webcam = new MediaCapture(this.pipeline, 640, 480, 30);

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
                    // Update the window image with the latest frame
                    this.DrawFrame(frame);
                },
                DeliveryPolicy.LatestMessage);

            // Start the pipeline running
            this.pipeline.RunAsync();
        }

        private void DrawFrame((Shared<Image> Image, float AudioLevel) frame)
        {
            // copy the frame image to the display bitmap
            this.UpdateBitmap(frame.Image);

            // clamp level to between 0 and 20
            var audioLevel = frame.AudioLevel < 0 ? 0 : frame.AudioLevel > 20 ? 20 : frame.AudioLevel;

            // redraw on the UI thread
            this.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    this.UpdateDisplayImage();
                    this.displayLevel.Value = audioLevel;
                    this.displayText.Text = audioLevel.ToString("0.0");
                }));
        }

        private void UpdateBitmap(Shared<Image> image)
        {
            // create a new bitmap if necessary
            if (this.bitmap == null)
            {
                // WriteableBitmap must be created on the UI thread
                this.Dispatcher.Invoke(() =>
                {
                    this.bitmap = new WriteableBitmap(
                        image.Resource.Width,
                        image.Resource.Height,
                        300,
                        300,
                        image.Resource.PixelFormat.ToWindowsMediaPixelFormat(),
                        null);

                    this.image.Source = this.bitmap;
                    this.bitmapPtr = this.bitmap.BackBuffer;
                });
            }

            // update the display bitmap's back buffer
            image.Resource.CopyTo(this.bitmapPtr, image.Resource.Width, image.Resource.Height, image.Resource.Stride, image.Resource.PixelFormat);
        }

        private void UpdateDisplayImage()
        {
            // invalidate the entire area of the bitmap to cause the display image to be redrawn
            this.bitmap.Lock();
            this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight));
            this.bitmap.Unlock();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.pipeline.Dispose();
        }
    }
}
