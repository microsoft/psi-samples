# Webcam With Audio Sample

This sample demonstrates how to capture an image stream from a webcam and an audio stream from a microphone. It also shows how to fuse two streams with different message rates into a single synchronized stream using the `Join` operator.

## Prerequisites

* A webcam or other image capture device.
* A microphone or other audio capture device.

## Sample Overview

This sample uses WPF to display images and UI elements in a window. The majority of the code we will be concerned with is located in the `MainWindow_Loaded` handler for the main window, located in the `MainWindow.xaml.cs` file. This is the event which will be raised when the main window is first loaded and we will use it to initialize our \psi pipeline.

The design the UI itself is defined in the `MainWindow.xaml.xml` file, but an understanding of this is not required for the purposes of this walkthrough. For this sample, get started by creating a new project with the WPF Application.

## Capturing Video

To capture video, we will first create a new pipeline in the `MainWindow_Loaded` handler method and add a `MediaCapture` component to it.

```csharp
this.pipeline = Pipeline.Create();
var webcam = new MediaCapture(this.pipeline, 640, 480, 30);
```

The video from the webcam will be captured as a stream of images using the `MediaCapture` component, which is initialized with arguments specifying the image resolution and frame rate. Note that you may have to change these values to specify a supported capture resolution and framerate for your webcam.

## Capturing Audio

Similarly, we will add an `AudioCapture` component to the pipeline to acquire an audio stream from the microphone.

```csharp
var audio = new AudioCapture(this.pipeline, WaveFormat.Create16kHz1Channel16BitPcm());
```

As configured, this will capture 16 kHz, 16-bit mono PCM audio from the default audio input device. To learn more about working with audio in \psi, see the [Audio Overview](https://github.com/microsoft/psi/wiki/Audio-Overview) wiki page.

## Audio Processing

Having captured an audio stream from the microphone, we now want to extract the log energy level of the audio signal. This is done by piping the audio stream into an `AudioFeaturesExtractor` component which computes a variety of audio features from the raw audio stream. We will only be using the `LogEnergy` stream in this sample. More information about this component can be found in the [Audio Overview](https://github.com/microsoft/psi/wiki/Audio-Overview#acoustic-feature-operators).

```csharp
var acousticFeatures = new AcousticFeaturesExtractor(this.pipeline);
audio.PipeTo(acousticFeatures);
```

## Synchronizing the Streams

As the video and audio streams originate from different devices and are captured at different rates, we will need to synchronize the video and computed audio energy streams in order to correlate the information we will be displaying in each frame. Of course, we could simply display each stream independently of the other as messages arrive, but consider the hypothetical case where the images drastically lag behind the audio. We would then be associating audio information with image frames from the past. In order to avoid this, we will use the `Join` operator to synchronize the streams.

```csharp
var webcamWithAudioEnergy = webcam.Join(acousticFeatures.LogEnergy, RelativeTimeInterval.Past());
```

Our ultimate goal is to display each webcam frame image overlaid with the correlated audio energy at the originating time of the frame. We therefore take the `webcam` stream and `Join` it with the `LogEnergy` stream. The `Join` operator takes a `RelativeTimeInterval` which specifies a window within which to locate a message on the `LogEnergy` stream which is closest in originating time to each webcam image. Here, we simply use a relative time interval of `Past` which will match the most recent message in originating time that occurs at or before the originating time of the webcam image. Note that we cannot do an exact join because the two streams will almost certainly not have messages with identical originating times. There are other versions of the `Join` operator that provide different ways of joining streams. For a more in-depth look at the topic of stream fusion and the `Join` operator, refer to the tutorial on [Stream Fusion and Merging](https://github.com/microsoft/psi/wiki/Stream-Fusion-and-Merging).

## Displaying the Frames

Having joined the two streams, we now have a single `webcamWithAudioEnergy` stream of type `(Shared<Image>, float)`, the first item of each message being the frame image and the second being the closest computed log energy level for that frame. We can now display both pieces of information by passing the frames to the `DrawFrame` method, which implements the necessary functionality to render the image and the audio energy information over it. This is done within the `Do` operator on the `webcamWithAudioEnergy` stream, which will apply the method to each message on the stream.

```csharp
webcamWithAudioEnergy.Do(
    frame =>
    {
        this.DrawFrame(frame);
    },
    DeliveryPolicy.LatestMessage);
```

The `DrawFrame` method copies the frame image data to a `WriteableBitmap` object which represents the displayed image. The displayed image is then updated. The audio level is rendered as a horizontal bar and text.

Note that the second argument to the `Do` operator specifies a `DeliveryPolicy` to apply to messages being delivered to the `Do` operator. By default, \psi streams will queue messages until components are able to receive and process them. We refer to this as a lossless or `Unlimited` delivery policy, where no messages are dropped and queues are allowed to grow.

In this case however, we are only concerned with displaying the latest image in real time, so we do not want to queue and draw each frame if the UI cannot keep up, as this will lead to ever increasing memory usage and the displayed images lagging further behind. The `LatestMessages` delivery policy allows frames to be dropped if they arrive at a rate that is faster than the `DrawFrame` method in the `Do` operator can draw them. For more information on delivery policies in \psi, see the [Delivery Policies](https://github.com/microsoft/psi/wiki/Delivery-Policies) tutorial.

## Pipeline Start and Stop

Finally, all that remains is to run the pipeline to begin capturing and displaying frames. Since we are starting the pipeline from within a window handler method, we use the `RunAsync` method to start the pipeline in the background and return control immediately to the UI.

```csharp
this.pipeline.RunAsync();
```

Once the pipeline has started running, it will continue capturing video and audio and displaying the frames in the window as previously described until the `this.pipeline` object is disposed. We therefore need to call the `Pipeline.Dispose` method in the window's `Closing` handler, which will be invoked when the user closes the window.

```csharp
this.pipeline.Dispose();
```

## Links
* [Audio Overview](https://github.com/microsoft/psi/wiki/Audio-Overview)
* [Stream Fusion and Merging](https://github.com/microsoft/psi/wiki/Stream-Fusion-and-Merging)
* [Delivery Policies](https://github.com/microsoft/psi/wiki/Delivery-Policies)
