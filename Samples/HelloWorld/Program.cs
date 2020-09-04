// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace HelloWorld
{
    using System;
    using Microsoft.Psi;

    class Program
    {
        static void Main(string[] args)
        {
            // Create a pipeline
            var p = Pipeline.Create();

            // Create a timer component that produces a message every second
            var timer = Timers.Timer(p, TimeSpan.FromSeconds(1));

            // For each message created by the timer, print "Hello world!" and keep track of the count
            int count = 0;
            timer.Do(t =>
            {
                Console.WriteLine($"{count++}: Hello world!");
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