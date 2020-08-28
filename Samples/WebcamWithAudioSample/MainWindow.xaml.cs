// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.WebcamWithAudioSample
{
    using System.ComponentModel;
    using System.Threading.Tasks;
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
                    this.Dispatcher.Invoke(() => this.DrawFrame(frame));
                },
                DeliveryPolicy.LatestMessage);

            // Start the pipeline running
            this.pipeline.RunAsync();
        }

        private void DrawFrame((Shared<Image> Image, float AudioLevel) frame)
        {
            // create a new bitmap if necessary
            if (this.bitmap == null ||
                this.bitmap.PixelWidth != frame.Image.Resource.Width ||
                this.bitmap.PixelHeight != frame.Image.Resource.Height ||
                this.bitmap.BackBufferStride != frame.Image.Resource.Stride)
            {
                this.bitmap = new WriteableBitmap(
                    frame.Image.Resource.Width,
                    frame.Image.Resource.Height,
                    300,
                    300,
                    frame.Image.Resource.PixelFormat.ToWindowsMediaPixelFormat(),
                    null);

                this.image.Source = this.bitmap;
            }

            // update the display bitmap in-place
            this.bitmap.WritePixels(
                new Int32Rect(
                    0,
                    0,
                    frame.Image.Resource.Width,
                    frame.Image.Resource.Height),
                frame.Image.Resource.ImageData,
                frame.Image.Resource.Stride * frame.Image.Resource.Height,
                frame.Image.Resource.Stride,
                0,
                0);

            // clamp level to between 0 and 20
            var audioLevel = frame.AudioLevel < 0 ? 0 : frame.AudioLevel > 20 ? 20 : frame.AudioLevel;
            this.level.Value = audioLevel;
            this.value.Text = audioLevel.ToString("0.0");
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // We only want to exit once we know that the pipeline has finished shutting down. We will cancel this closing
            // event and register a handler to close the window when the PipelineCompleted event is raised by the pipeline.
            e.Cancel = true;
            this.Closing -= this.MainWindow_Closing;
            this.pipeline.PipelineCompleted += (s, e) => this.Dispatcher.Invoke(this.Close);

            // Dispose the pipeline on a background thread so we don't block the UI thread while the pipeline is shutting down
            Task.Run(this.pipeline.Dispose);
        }
    }
}
