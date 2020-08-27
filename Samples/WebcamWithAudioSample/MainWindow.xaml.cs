// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.WebcamWithAudioSample
{
    using System.ComponentModel;
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Media;

    /// <summary>
    /// Webcam with audio sample program.
    /// </summary>
    public partial class MainWindow
    {
        private Pipeline pipeline;
        private DisplayImage displayImage = new DisplayImage();

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();
            this.Loaded += this.MainWindow_Loaded;
            this.Closing += this.MainWindow_Closing;
            this.DataContext = this.displayImage;
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
                    var image = frame.Item1;
                    var audioLevel = frame.Item2;

                    this.DrawAudioLevel(image, audioLevel);

                    // Update the window image with the Psi image
                    this.Dispatcher.Invoke(() => this.displayImage.UpdateImage(image));
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

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // Dispose the pipeline on a background thread so we don't block the UI thread while the pipeline is shutting down
            Task.Run(this.pipeline.Dispose);

            // We only want to exit once we know that the pipeline has finished shutting down. We will cancel this closing
            // event and register a handler to close the window when the PipelineCompleted event is raised by the pipeline.
            e.Cancel = true;
            this.pipeline.PipelineCompleted += (s, e) => this.Dispatcher.Invoke(this.Close);
            this.Closing -= this.MainWindow_Closing;
        }
    }
}
