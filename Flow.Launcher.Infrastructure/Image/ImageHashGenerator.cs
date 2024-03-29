﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Infrastructure.Image
{
    public interface IImageHashGenerator
    {
        string GetHashFromImage(ImageSource image);
    }
    public class ImageHashGenerator : IImageHashGenerator
    {
        public string GetHashFromImage(ImageSource imageSource)
        {
            if (imageSource is not BitmapSource image)
            {
                return null;
            }

            try
            {
                using var outStream = new MemoryStream();
                var enc = new JpegBitmapEncoder();
                var bitmapFrame = BitmapFrame.Create(image);
                bitmapFrame.Freeze();
                enc.Frames.Add(bitmapFrame);
                enc.Save(outStream);
                var byteArray = outStream.GetBuffer();
                using var sha1 = SHA1.Create();
                var hash = Convert.ToBase64String(sha1.ComputeHash(byteArray));
                return hash;
            }
            catch
            {
                return null;
            }

        }
    }
}
