// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace HoloLensSample
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using MathNet.Numerics.LinearAlgebra.Double;
    using MathNet.Spatial.Euclidean;
    using MathNet.Spatial.Units;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.MixedReality;
    using Microsoft.Psi.MixedReality.MediaCapture;
    using Microsoft.Psi.MixedReality.ResearchMode;
    using Microsoft.Psi.MixedReality.StereoKit;
    using StereoKit;
    using Windows.Storage;
    using Color = System.Drawing.Color;
    using Microphone = Microsoft.Psi.MixedReality.StereoKit.Microphone;

    /// <summary>
    /// HoloLens samples.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            // Initialize StereoKit
            if (!SK.Initialize(
                new SKSettings
                {
                    appName = "HoloLensSample",
                    assetsFolder = "Assets",
                }))
            {
                throw new Exception("StereoKit failed to initialize.");
            }

            // Initialize MixedReality statics
            MixedReality.Initialize();

            var demos = new (string Name, Func<bool, Pipeline> Run)[]
            {
                ("Movable Marker Demo", MovableMarkerDemo),
                ("Bees Demo!", BeesDemo),
                ("Scene Understanding Demo", SceneUnderstandingDemo),
            };

            bool persistStreamsToStore = false;
            Pipeline pipeline = null;
            var starting = false;
            var stopping = false;
            var demo = string.Empty;
            Exception exception = null;
            var windowCoordinateSystem = default(CoordinateSystem);
            var windowPose = default(Pose);

            while (SK.Step(() =>
            {
                try
                {
                    // Position the window near the head at the start
                    var headPose = Input.Head.ToCoordinateSystem();

                    if (windowCoordinateSystem == null)
                    {
                        // Project forward 0.7 meters from the initial head pose (in the XY plane).
                        var forwardDirection = headPose.XAxis.ProjectOn(new MathNet.Spatial.Euclidean.Plane(headPose.Origin, UnitVector3D.ZAxis)).Direction;
                        var windowOrigin = headPose.Origin + forwardDirection.ScaleBy(0.7);
                        windowCoordinateSystem = LookAtPoint(windowOrigin, headPose.Origin);
                    }
                    else
                    {
                        // Update to point toward the head
                        windowCoordinateSystem = windowPose.ToCoordinateSystem();
                        windowCoordinateSystem = LookAtPoint(windowCoordinateSystem.Origin, headPose.Origin);
                    }

                    windowPose = windowCoordinateSystem.ToStereoKitPose();
                    UI.WindowBegin("Psi Demos", ref windowPose, new Vec2(30 * U.cm, 0));

                    if (exception != null)
                    {
                        UI.Label($"Exception: {exception.Message}");
                    }
                    else if (starting)
                    {
                        UI.Label($"Starting {demo}...");
                    }
                    else if (stopping)
                    {
                        UI.Label($"Stopping {demo}...");
                    }
                    else
                    {
                        if (pipeline == null)
                        {
                            UI.Label("Choose a demo to run");
                            foreach (var (name, run) in demos)
                            {
                                if (UI.Button(name))
                                {
                                    demo = name;
                                    starting = true;
                                    Task.Run(() =>
                                    {
                                        pipeline = run(persistStreamsToStore);
                                        pipeline.RunAsync();
                                        starting = false;
                                    });
                                }

                                UI.SameLine();
                            }

                            UI.NextLine();
                        }
                        else
                        {
                            UI.Label($"Running {demo}");
                            if (UI.Button($"Stop"))
                            {
                                stopping = true;
                                Task.Run(() =>
                                {
                                    pipeline.Dispose();
                                    pipeline = null;
                                    stopping = false;
                                });
                            }

                            UI.SameLine();
                        }
                    }

                    if (UI.Button("Exit"))
                    {
                        SK.Quit();
                    }

                    UI.WindowEnd();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }))
            {
            }

            SK.Shutdown();
        }

        /// <summary>
        /// Scene understanding demo.
        /// </summary>
        /// <param name="persistStreamsToStore">A value indicating whether to persist a store with several sensor streams.</param>
        /// <returns>Pipeline.</returns>
        public static Pipeline SceneUnderstandingDemo(bool persistStreamsToStore)
        {
            var pipeline = Pipeline.Create(nameof(SceneUnderstandingDemo));

            var sceneUnderstanding = new SceneUnderstanding(pipeline, new SceneUnderstandingConfiguration()
            {
                ComputePlacementRectangles = true,
                InitialPlacementRectangleSize = (0.5, 1.0),
            });

            var worldMeshes = sceneUnderstanding.Select(s => s.World.Meshes.ToArray());
            var wallPlacementRectangles = sceneUnderstanding
                .Select(s => s.Wall.PlacementRectangles)
                .Select(rects => rects.Where(r => r.HasValue).Select(r => r.Value).ToArray());

            worldMeshes.Parallel(
                s => s.PipeTo(new Mesh3DRenderer(s.Out.Pipeline, Color.White, true)),
                name: "RenderWorldMeshes");

            wallPlacementRectangles.Parallel(
                s => s.PipeTo(new Rectangle3DRenderer(s.Out.Pipeline, Color.Red)),
                name: "RenderPlacementRectangles");

            if (persistStreamsToStore)
            {
                // Optionally persist sensor streams to visualize in PsiStudio, along with scene understanding info.
                var store = CreateStoreWithSourceStreams(pipeline, nameof(SceneUnderstandingDemo));
                sceneUnderstanding.Write("SceneUnderstanding", store, true);
            }

            return pipeline;
        }

        /// <summary>
        /// Movable marker demo.
        /// </summary>
        /// <param name="persistStreamsToStore">A value indicating whether to persist a store with several sensor streams.</param>
        /// <returns>Pipeline.</returns>
        public static Pipeline MovableMarkerDemo(bool persistStreamsToStore)
        {
            var pipeline = Pipeline.Create(nameof(MovableMarkerDemo));

            // Instantiate the marker renderer (starting pose of 1 meter forward, 30cm down).
            var markerScale = 0.4f;
            var initialMarkerPose = CoordinateSystem.Translation(new Vector3D(1, 0, -0.3));
            var markerMesh = MeshRenderer.CreateMeshFromEmbeddedResource("HoloLensSample.Assets.Marker.Marker.glb");
            var markerRenderer = new MeshRenderer(pipeline, markerMesh, initialMarkerPose, new Vector3D(markerScale, markerScale, markerScale), Color.LightBlue);

            // handle to move marker
            var handleBounds = new Vector3D(
                markerScale * markerMesh.Bounds.dimensions.x,
                markerScale * markerMesh.Bounds.dimensions.y,
                markerScale * markerMesh.Bounds.dimensions.z);
            var handle = new Handle(pipeline, initialMarkerPose, handleBounds);

            // slowly spin the marker
            var spin = Generators
                .Range(pipeline, 0, int.MaxValue, TimeSpan.FromMilliseconds(10))
                .Select(i => CoordinateSystem.Yaw(Angle.FromDegrees(i * 0.5)));

            // combine spinning with user-driven movement
            var markerPose = spin.Join(handle, RelativeTimeInterval.Infinite)
                .Select(m => m.Item1.TransformBy(m.Item2));

            markerPose.PipeTo(markerRenderer.Pose);

            if (persistStreamsToStore)
            {
                // Optionally persist sensor streams to visualize in PsiStudio, along with the marker pose.
                var store = CreateStoreWithSourceStreams(pipeline, nameof(MovableMarkerDemo));
                markerPose.Write("MarkerPose", store);
            }

            return pipeline;
        }

        /// <summary>
        /// Bees circling your head demo.
        /// </summary>
        /// <param name="persistStreamsToStore">A value indicating whether to persist a store with several sensor streams.</param>
        /// <returns>Pipeline.</returns>
        public static Pipeline BeesDemo(bool persistStreamsToStore)
        {
            var pipeline = Pipeline.Create(nameof(BeesDemo));

            // Load bee audio from a wav file, triggered to play every two seconds.
            using var beeWave = Assembly.GetCallingAssembly().GetManifestResourceStream("HoloLensSample.Assets.Sounds.Bees.wav");
            var beeAudio = new WaveStreamSampleSource(pipeline, beeWave);
            var repeat = Generators.Repeat(pipeline, true, TimeSpan.FromSeconds(2));
            repeat.PipeTo(beeAudio);

            // Send the audio to a spatial sound rendering component.
            var beeSpatialSound = new SpatialSound(pipeline, default, 2);
            beeAudio.PipeTo(beeSpatialSound);

            // Calculate the pose of the bee that flies in a 1 meter radius circle around the user's head.
            var oneMeterForward = CoordinateSystem.Translation(new Vector3D(1, 0, 0));
            var zeroRotation = DenseMatrix.CreateIdentity(3);
            var headPose = new HeadSensor(pipeline);
            var beePose = headPose.Select((head, env) =>
            {
                // Fly 1 degree around the user's head every 20 ms.
                var timeElapsed = (env.OriginatingTime - pipeline.StartTime).TotalMilliseconds;
                var degrees = Angle.FromDegrees(timeElapsed / 20.0);

                // Ignore the user's head rotation.
                head = head.SetRotationSubMatrix(zeroRotation);
                return oneMeterForward.RotateCoordSysAroundVector(UnitVector3D.ZAxis, degrees).TransformBy(head);
            });

            // Render the bee as a sphere.
            var sphere = new MeshRenderer(pipeline, Mesh.GenerateSphere(0.1f), Color.Yellow);
            beePose.PipeTo(sphere.Pose);

            // Finally, pass the position (Point3D) of the bee to the spatial audio component.
            var beePosition = beePose.Select(b => b.Origin);
            beePosition.PipeTo(beeSpatialSound.PositionInput);

            if (persistStreamsToStore)
            {
                // Optionally persist sensor streams to visualize in PsiStudio, along with the bee streams.
                var store = CreateStoreWithSourceStreams(pipeline, nameof(BeesDemo), headPose);
                beePosition.Write("Bee.Position", store);
                beeAudio.Write("Bee.Audio", store);
            }

            return pipeline;
        }

        private static PsiExporter CreateStoreWithSourceStreams(Pipeline pipeline, string storeName, HeadSensor head = null)
        {
            // Create a Psi store in \LocalAppData\HoloLensSample\LocalState
            // To visualize in PsiStudio, the store can be copied to another machine via the device portal.
            var store = PsiStore.Create(pipeline, storeName, ApplicationData.Current.LocalFolder.Path);

            // Head, hands, and eyes
            head ??= new HeadSensor(pipeline);
            var eyes = new EyesSensor(pipeline);
            var hands = new HandsSensor(pipeline);

            head.Write("Head", store);
            eyes.Write("Eyes", store);
            hands.Left.Write("Hands.Left", store);
            hands.Right.Write("Hands.Right", store);

            // Microphone audio
            var audio = new Microphone(pipeline);
            audio.Write("Audio", store);

            // PhotoVideo camera (video and mixed reality preview)
            var camera = new PhotoVideoCamera(
                pipeline,
                new PhotoVideoCameraConfiguration
                {
                    VideoStreamSettings = new () { FrameRate = 15, ImageWidth = 896, ImageHeight = 504 },
                    PreviewStreamSettings = new () { FrameRate = 15, ImageWidth = 896, ImageHeight = 504, MixedRealityCapture = new () },
                });

            camera.VideoEncodedImageCameraView.Write("VideoEncodedImageCameraView", store, true);
            camera.PreviewEncodedImageCameraView.Write("PreviewEncodedImageCameraView", store, true);

            // Depth camera (long throw)
            var depthCamera = new DepthCamera(pipeline);
            depthCamera.DepthImageCameraView.Write("DepthImageCameraView", store, true);

            return store;
        }

        private static CoordinateSystem LookAtPoint(Point3D sourcePoint, Point3D targetPoint)
        {
            var forward = (targetPoint - sourcePoint).Normalize();
            var left = UnitVector3D.ZAxis.CrossProduct(forward);
            var up = forward.CrossProduct(left);
            return new CoordinateSystem(sourcePoint, forward, left, up);
        }
    }
}
