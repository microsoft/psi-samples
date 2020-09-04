// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.


namespace SimpleVoiceActivityDetector
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;

    class Program
    {
        static void Main(string[] args)
        {
            var p = Pipeline.Create();
            var store = PsiStore.Create(p, "SimpleVAD", Path.Combine(Directory.GetCurrentDirectory(), "Stores"));

            // Create the microphone, acoustic feature extractor, and connect them
            var microphone = new AudioCapture(p, WaveFormat.Create16kHz1Channel16BitPcm());
            var acousticFeaturesExtractor = new AcousticFeaturesExtractor(p);
            microphone.PipeTo(acousticFeaturesExtractor);

            // Display the log energy
            acousticFeaturesExtractor.LogEnergy
                .Sample(TimeSpan.FromSeconds(0.2))
                .Do(logEnergy => Console.WriteLine($"LogEnergy = {logEnergy}"));

            // Create a voice-activity stream by thresholding the log energy
            var vad = acousticFeaturesExtractor.LogEnergy
                .Select(l => l > 7)
                .Write("VAD", store);

            // Create filtered signal by aggregating over historical buffers
            var vadWithHistory = acousticFeaturesExtractor.LogEnergy
                .Window(RelativeTimeInterval.Future(TimeSpan.FromMilliseconds(300)))
                .Aggregate(false, (previous, buffer) => (!previous && buffer.All(v => v > 7)) || (previous && !buffer.All(v => v < 7)))
                .Write("VADFiltered", store);

            // Write the microphone output and some acoustic features to the store
            microphone.Write("Audio", store);
            acousticFeaturesExtractor.LogEnergy.Write("LogEnergy", store);
            acousticFeaturesExtractor.ZeroCrossingRate.Write("ZeroCrossingRate", store);

            p.RunAsync();
            Console.ReadKey();
            p.Dispose();
        }
    }
}
