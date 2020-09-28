// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace WhatIsThat
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
    using Microsoft.Azure.Kinect.Sensor;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.AzureKinect;
    using Microsoft.Psi.CognitiveServices.Speech;
    using Microsoft.Psi.CognitiveServices.Vision;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Speech;

    /// <summary>
    /// Sample program that attempts to detect which object a person is pointing to.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            // Create the pipeline and the output store.
            var p = Pipeline.Create();
            var store = PsiStore.Create(p, nameof(WhatIsThat), Path.Combine(Directory.GetCurrentDirectory(), "Stores"));

            // Create a microphone component and configure it to do audio pre-processing for speech reco.
            var audio = new AudioCapture(p, new AudioCaptureConfiguration()
            {
                OptimizeForSpeech = true,
                Format = WaveFormat.Create16kHz1Channel16BitPcm(),
            });

            // Write the audio stream to the store.
            audio.Write("Sources.Audio", store);

            // Create the Azure Kinect sensor component, and configure it to enable body tracking.
            var azureKinect = new AzureKinectSensor(p, new AzureKinectSensorConfiguration()
            {
                ColorResolution = ColorResolution.R720p,
                CameraFPS = FPS.FPS15,
                BodyTrackerConfiguration = new AzureKinectBodyTrackerConfiguration()
                {
                    TemporalSmoothing = 0.4f,
                },
            });

            // Encode and persist the images and various other streams from the Azure Kinect sensor.
            var encodedColorImage = azureKinect.ColorImage.EncodeJpeg(90, DeliveryPolicy.LatestMessage)
                .Write("Sources.ColorImage", store);
            var encodedDepthImage = azureKinect.DepthImage.EncodePng(DeliveryPolicy.LatestMessage)
                .Write("Sources.DepthImage", store);
            var calibrationInfo = azureKinect.DepthDeviceCalibrationInfo
                .Write("Sources.CalibrationInfo", store);
            var bodies = azureKinect.Bodies
                .Write("Sources.Bodies", store);
            encodedDepthImage.Pair(calibrationInfo.Select(ci => ci.DepthIntrinsics))
                .Write("Sources.DepthWithCalibration", store);

            // Get the closest body.
            var closestBody = azureKinect.Bodies.GetClosestBody();

            // Compute the elbow angle, the hand tip delta, and whether the person is pointing.
            var elbowAngle = closestBody.GetElbowAngle().Write("Pointing.ElbowAngle", store);
            var handTipDelta = closestBody.GetHandTipDelta().Write("Pointing.HandTipDelta", store);
            var isPointing = closestBody.IsPointing().Write("Pointing.IsPointing", store);

            // Compute the pointing line.
            var pointingLine = closestBody.GetPointingLine(azureKinect.DepthImage, calibrationInfo)
                .Write("Pointing.PointingLine", store);

            // Compute the cropped image by projecting the end point into the camera color image.
            var croppedImage = pointingLine
                .NullableSelect(line => line.EndPoint)
                .GetProjectedCroppedImage(azureKinect.ColorImage, calibrationInfo)
                .Write("Pointing.TargetImage", store);

            // Create an Azure image analyzer and configure it to detect objects.
            var imageAnalyzer = new ImageAnalyzer(p, new ImageAnalyzerConfiguration()
            {
                SubscriptionKey = Constants.AzureVisionSubscriptionKey,
                Region = Constants.AzureRegion,
                VisualFeatures = new VisualFeatureTypes[] { VisualFeatureTypes.Objects },
            });

            // Pass the cropped images, if available, to the image analyzer.
            croppedImage.Where(pi => pi != null && pi.Resource != null).PipeTo(imageAnalyzer, DeliveryPolicy.LatestMessage);

            // Extract the detected objects from the image analysis results.
            imageAnalyzer
                .Where(r => r != null)
                .ExtractDetectedObjects()
                .Write("Pointing.VisionResults", store);

            // Create a voice activity detector and send the audio to it.
            var voiceActivityDetector = new SystemVoiceActivityDetector(p);
            audio.PipeTo(voiceActivityDetector);
            voiceActivityDetector.Write("Audio.VoiceActivityDetection", store);

            // Create an Azure Speech Recognition component.
            var azureSpeechReco = new AzureSpeechRecognizer(p, new AzureSpeechRecognizerConfiguration()
            {
                SubscriptionKey = Constants.AzureSpeechSubscriptionKey,
                Region = Constants.AzureRegion,
            });

            // Connect the segmented audio to the speech recognizer.
            audio.Join(voiceActivityDetector).PipeTo(azureSpeechReco);
            azureSpeechReco.Write("Audio.Speech.Results", store);

            // Run the pipeline asynchronously, until the user presses a key.
            p.RunAsync();
            Console.ReadKey();
            p.Dispose();
            Console.WriteLine("All done.");
        }
    }
}
