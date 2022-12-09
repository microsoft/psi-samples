// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace WhatIsThat
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MathNet.Spatial.Euclidean;
    using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
    using Microsoft.Azure.Kinect.BodyTracking;
    using Microsoft.Psi;
    using Microsoft.Psi.AzureKinect;
    using Microsoft.Psi.Calibration;
    using Microsoft.Psi.Imaging;

    /// <summary>
    /// Static class containing custom stream operators and extension methods.
    /// </summary>
    internal static class Operators
    {
        // Change this constant to false to detect pointing with the left hand.
        private const bool UseRightHand = true;

        private const JointId ShoulderJointId = UseRightHand ? JointId.ShoulderRight : JointId.ShoulderLeft;
        private const JointId ElbowJointId = UseRightHand ? JointId.ElbowRight : JointId.ElbowLeft;
        private const JointId HandTipJointId = UseRightHand ? JointId.HandTipRight : JointId.HandTipLeft;

        /// <summary>
        /// Computes a stream containing the closest body to the camera, from a specified stream of bodies.
        /// </summary>
        /// <param name="source">The stream of bodies.</param>
        /// <returns>A stream containing the closest body to the camera.</returns>
        internal static IProducer<AzureKinectBody> GetClosestBody(this IProducer<List<AzureKinectBody>> source)
        {
            return source.Select(bodies =>
            {
                var minDistance = double.MaxValue;
                var closestBody = default(AzureKinectBody);

                // Find the body containing the closest joint.
                foreach (var body in bodies)
                {
                    var distance = body.Joints.Values.Min(j => j.Pose.Origin.Z);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestBody = body;
                    }
                }

                return closestBody;
            });
        }

        /// <summary>
        /// Gets the elbow angle for a specified body.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The elbow angle.</returns>
        internal static double GetElbowAngle(this AzureKinectBody body)
        {
            if (body != null)
            {
                var shoulder = body.Joints[ShoulderJointId].Pose.Origin;
                var elbow = body.Joints[ElbowJointId].Pose.Origin;
                var handTip = body.Joints[HandTipJointId].Pose.Origin;
                return (shoulder - elbow).AngleTo(handTip - elbow).Degrees;
            }
            else
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Computes a stream containing the elbow angle, from a specified stream of body.
        /// </summary>
        /// <param name="source">The stream of body.</param>
        /// <returns>The stream of elbow angle.</returns>
        internal static IProducer<double> GetElbowAngle(this IProducer<AzureKinectBody> source) => source.Select(body => body.GetElbowAngle());

        /// <summary>
        /// Gets the height differential between the hand tip and the chest for a specified body.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The height differential between the hand tip and the chest.</returns>
        internal static double GetHandTipDelta(this AzureKinectBody body)
        {
            if (body != null && body.Joints.ContainsKey(HandTipJointId) && body.Joints.ContainsKey(JointId.SpineChest))
            {
                var handTip = body.Joints[HandTipJointId].Pose.Origin;
                var chest = body.Joints[JointId.SpineChest].Pose.Origin;
                return handTip.Z - chest.Z;
            }
            else
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Computes a stream containing the height differential between the hand tip and the chest,
        /// from a specified stream of body.
        /// </summary>
        /// <param name="source">The stream of body.</param>
        /// <returns>The stream containing the height differential between the hand tip and the chest.</returns>
        internal static IProducer<double> GetHandTipDelta(this IProducer<AzureKinectBody> source) => source.Select(body => body.GetHandTipDelta());

        /// <summary>
        /// Gets a value indicating whether the body is pointing.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>True if the body is pointing, false otherwise.</returns>
        internal static bool IsPointing(this AzureKinectBody body) => body != null && body.GetElbowAngle() > 120 && body.GetHandTipDelta() > -0.1;

        /// <summary>
        /// Computes a stream indicating whether the body is pointing, from a specified stream of body.
        /// </summary>
        /// <param name="source">The stream of body.</param>
        /// <returns>A stream with boolean values indicating whether the body is pointing.</returns>
        internal static IProducer<bool> IsPointing(this IProducer<AzureKinectBody> source) => source.Select(body => body.IsPointing());

        /// <summary>
        /// Computes a stream with the pointing line.
        /// </summary>
        /// <param name="body">The stream of body.</param>
        /// <param name="depthImage">The stream of depth images.</param>
        /// <param name="calibrationInfo">The stream with calibration information.</param>
        /// <returns>A stream containing the pointing line.</returns>
        internal static IProducer<Line3D?> GetPointingLine(
            this IProducer<AzureKinectBody> body,
            IProducer<Shared<DepthImage>> depthImage,
            IProducer<IDepthDeviceCalibrationInfo> calibrationInfo)
        {
            return body
                .Join(depthImage, RelativeTimeInterval.Past())
                .Pair(calibrationInfo)
                .Select(tuple =>
                {
                    (var b, var di, var ci) = tuple;
                    if (ci != null && b != null && b.IsPointing())
                    {
                        var head = b.Joints[JointId.Head].Pose.Origin;
                        var handTip = b.Joints[HandTipJointId].Pose.Origin;

                        // We construct a line that follows the direction from the head to the hand tip,
                        // and intersect this line with the mesh. The start point of the line is set
                        // by advancing 20 cm in the space forward from the hand-tip, in the pointing
                        // direction. The end point of the line is set by advancing 30 cm in the space
                        // forward from the hand-tip, in the pointing direction. The
                        // CalibrationExtensions.ComputeRayIntersection API advances forward on this line,
                        // from the start point, until it hits the mesh. We do not start the line right
                        // at the hand-tip to avoid having an intersection with the mesh around the hand.
                        var direction = (handTip - head).Normalize();
                        var startPoint = handTip + direction.ScaleBy(0.2);
                        var ray = new Ray3D(startPoint, direction);

                        var intersection = di.Resource.ComputeRayIntersection(ci.DepthIntrinsics, ray);
                        return intersection.HasValue ?
                            new Line3D(startPoint, intersection.Value) :
                            default;
                    }
                    else
                    {
                        return default(Line3D?);
                    }
                });
        }

        /// <summary>
        /// Computes a stream with the cropped image around a specified point.
        /// </summary>
        /// <param name="point3D">The stream of 3D points.</param>
        /// <param name="image">The stream of images.</param>
        /// <param name="calibrationInfo">The stream with calibration information.</param>
        /// <returns>A stream containing the cropped image.</returns>
        internal static IProducer<Shared<Image>> GetProjectedCroppedImage(
            this IProducer<Point3D?> point3D,
            IProducer<Shared<Image>> image,
            IProducer<IDepthDeviceCalibrationInfo> calibrationInfo)
        {
            return point3D
                .Join(image, RelativeTimeInterval.Past())
                .Pair(calibrationInfo)
                .Select(tuple =>
                {
                    (var p, var si, var ci) = tuple;
                    if (p.HasValue && ci.TryGetPixelPosition(p.Value, out var point, false))
                    {
                        var croppedWidth = Math.Min(si.Resource.Width, 200);
                        var croppedHeight = Math.Min(si.Resource.Height, 200);
                        var x = Math.Min(Math.Max(0, (int)point.X - 100), si.Resource.Width - croppedWidth);
                        var y = Math.Min(Math.Max(0, (int)point.Y - 100), si.Resource.Height - croppedHeight);
                        var cropped = ImagePool.GetOrCreate(croppedWidth, croppedHeight, si.Resource.PixelFormat);
                        si.Resource.Crop(cropped.Resource, x, y, croppedWidth, croppedHeight);
                        return cropped;
                    }
                    else
                    {
                        return null;
                    }
                });
        }

        /// <summary>
        /// Computes a stream containing the labeled rectangles for the detected objects.
        /// </summary>
        /// <param name="source">The stream of image analysis results.</param>
        /// <returns>A stream with the labeled rectangles.</returns>
        internal static IProducer<List<Tuple<System.Drawing.Rectangle, string>>> ExtractDetectedObjects(this IProducer<ImageAnalysis> source)
        {
            return source.Select(
                r => r.Objects.Select(
                    o => Tuple.Create(
                        new System.Drawing.Rectangle(
                            o.Rectangle.X,
                            o.Rectangle.Y,
                            o.Rectangle.W,
                            o.Rectangle.H), o.ObjectProperty)).ToList());
        }
    }
}