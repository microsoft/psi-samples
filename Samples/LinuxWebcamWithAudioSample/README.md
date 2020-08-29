# Webcam With Audio Sample

This sample demonstrates how to capture an image stream from a webcam and an audio stream from a microphone. It also shows how to fuse two streams with different message rates into a single synchronized stream using the `Join` operator.

## Prerequisites

* A webcam that is accessible via a virtual device node such as `/dev/video0`.
* A microphone or other audio capture device.
* ALSA sound libraries installed (see [Audio Overview](https://github.com/microsoft/psi/wiki/Audio-Overview#troubleshooting-audio-on-linux)).

## Sample Overview

This sample uses Gtk (via the GtkSharp package) to display images and UI elements in a window. The majority of the code we will be concerned with is located in the `MainWindow_Shown` handler for the main window, located in the `MainWindow.cs` file. This is the event which will be raised when the main window is first shown and we will use it to initialize our \psi pipeline.

The design and styling of the UI elements are defined in the `MainWindow.xml` and `Style.css` files respectively, but an understanding of these is not required for the purposes of this sample.

## Capturing Video

To capture video, we will create a new pipeline in the `MainWindow_Shown` handler method and add a `MediaCapture` and an `AudioCapture` component to it.

```csharp
this.pipeline = Pipeline.Create();
var webcam = new MediaCapture(this.pipeline, 640, 480, "/dev/video0", PixelFormatId.YUYV);
```

Note that we store our pipeline in an instance variable, `this.pipeline`. This is typically the pattern used in \psi applications that have a UI (as opposed to a console application), where execution is usually event-driven.

The video from the webcam will be captured as a stream of images using the `MediaCapture` component, which is initialized with arguments specifying the image resolution and the virtual device node name. Note that you may have to change these values depending on your hardware and the resolutions it supports.

## Capturing Audio

Similarly, we will add an `AudioCapture` component to the pipeline to acquire an audio stream from the microphone.

```csharp
var audio = new AudioCapture(this.pipeline, new AudioCaptureConfiguration { Format = WaveFormat.Create16kHz1Channel16BitPcm() });
```

As configured, this will capture 16 kHz, 16-bit mono PCM audio from the ALSA plugin device `plughw:0,0`. You may need to modify the value of the `DeviceName` property depending on your audio hardware configuration. This is typically of the form "plughw:_c_,_d_" where _c_ is the soundcard index and _d_ is the device index (e.g. "plughw:0,0", "plughw:1,0", etc.). You can list the available capture devices using the `arecord -L` command.

To learn more about working with audio in \psi, see the [Audio Overview](https://github.com/microsoft/psi/wiki/Audio-Overview) wiki page.

## Audio Processing

Having captured an audio stream from a microphone, we perform some simple processing to extract the log energy level of the audio. This is done by piping the audio stream into an `AudioFeaturesExtractor` component which extracts a variety of audio features from a raw audio stream.

## Links
* [Audio Overview](https://github.com/microsoft/psi/wiki/Audio-Overview)