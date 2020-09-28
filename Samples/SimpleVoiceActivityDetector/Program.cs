// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace SimpleVoiceActivityDetector
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;

    /// <summary>
    /// Simple voice activity detector sample.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            using (var p = Pipeline.Create())
            {
                // Create the microphone, acoustic feature extractor, and connect them
                var microphone = new AudioCapture(p, WaveFormat.Create16kHz1Channel16BitPcm());

                // Comment out the line above, and uncomment the next two lines to run the application by
                // replaying over existing data, rather than live.
                ////var inputStore = PsiStore.Open(p, "SimpleVAD", Path.Combine(Directory.GetCurrentDirectory(), "Stores", "SimpleVAD.0000"));
                ////var microphone = inputStore.OpenStream<AudioBuffer>("Audio");
                var acousticFeaturesExtractor = new AcousticFeaturesExtractor(p);
                microphone.PipeTo(acousticFeaturesExtractor);

                // Display the log energy
                acousticFeaturesExtractor.LogEnergy
                    .Sample(TimeSpan.FromSeconds(0.2))
                    .Do(logEnergy => Console.WriteLine($"LogEnergy = {logEnergy}"));

                // Create a voice-activity stream by thresholding the log energy
                var vad = acousticFeaturesExtractor.LogEnergy
                    .Select(l => l > 7);

                // Create filtered signal by aggregating over historical buffers
                var vadWithHistory = acousticFeaturesExtractor.LogEnergy
                    .Window(RelativeTimeInterval.Future(TimeSpan.FromMilliseconds(300)))
                    .Aggregate(false, (previous, buffer) => (!previous && buffer.All(v => v > 7)) || (previous && !buffer.All(v => v < 7)));

                // Write the microphone output, VAD streams, and some acoustic features to the store
                var store = PsiStore.Create(p, "SimpleVAD", Path.Combine(Directory.GetCurrentDirectory(), "Stores"));
                microphone.Write("Audio", store);
                vad.Write("VAD", store);
                vadWithHistory.Write("VADFiltered", store);
                acousticFeaturesExtractor.LogEnergy.Write("LogEnergy", store);
                acousticFeaturesExtractor.ZeroCrossingRate.Write("ZeroCrossingRate", store);

                p.RunAsync();
                Console.ReadKey();
            }
        }
    }
}
