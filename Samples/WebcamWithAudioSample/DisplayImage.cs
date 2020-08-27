// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.WebcamWithAudioSample
{
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using Microsoft.Psi;
    using Microsoft.Psi.Imaging;

    /// <summary>
    /// DisplayImage is a helper class that is used to bind a WPF <image/> to a Psi image.
    /// </summary>
    public class DisplayImage : INotifyPropertyChanged
    {
        private WriteableBitmap image;

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the WriteableBitmap that will be displayed in a WPF control.
        /// </summary>
        public WriteableBitmap Image
        {
            get => this.image;

            set
            {
                this.image = value;
                this.OnPropertyChanged(nameof(this.Image));
            }
        }

        /// <summary>
        /// Updates the image.
        /// </summary>
        /// <param name="psiImage">The updated image.</param>
        public void UpdateImage(Shared<Image> psiImage)
        {
            if (this.Image == null ||
                this.Image.PixelWidth != psiImage.Resource.Width ||
                this.Image.PixelHeight != psiImage.Resource.Height ||
                this.Image.BackBufferStride != psiImage.Resource.Stride)
            {
                this.Image = new WriteableBitmap(
                    psiImage.Resource.Width,
                    psiImage.Resource.Height,
                    300,
                    300,
                    psiImage.Resource.PixelFormat.ToWindowsMediaPixelFormat(),
                    null);
            }

            this.Image.Lock();
            this.Image.WritePixels(
                new Int32Rect(
                    0,
                    0,
                    psiImage.Resource.Width,
                    psiImage.Resource.Height),
                psiImage.Resource.ImageData,
                psiImage.Resource.Stride * psiImage.Resource.Height,
                psiImage.Resource.Stride,
                0,
                0);
            this.Image.Unlock();
        }

        /// <summary>
        /// Helper function for firing an event when the image property changes.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        private void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
