// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace HelloWorld
{
    using System;
    using Microsoft.Psi;

    /// <summary>
    /// Hello world sample.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            // Create a pipeline
            var p = Pipeline.Create();

            // Create a timer component that produces a message every second
            var timer = Timers.Timer(p, TimeSpan.FromSeconds(1));

            // For each message created by the timer, print "Hello world!"
            // along with the message's originating time.
            timer.Do((t, e) =>
            {
                Console.WriteLine($"{e.OriginatingTime}: Hello world!");
            });

            // Run the pipeline, but don't block here
            p.RunAsync();

            // Wait for the user to hit a key before closing the pipeline
            Console.ReadKey();

            // Close the pipeline
            p.Dispose();
        }
    }
}