using System;
using System.Buffers;
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
            if (imageSource is not BitmapSource { IsFrozen: true } image)
            {
                return null;
            }

            try
            {
                // Normalize to a deterministic pixel format (32-bit premultiplied BGRA)
                BitmapSource normalized = image;
                if (normalized.Format != PixelFormats.Pbgra32)
                {
                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = normalized;
                    converted.DestinationFormat = PixelFormats.Pbgra32;
                    converted.EndInit();
                    converted.Freeze();

                    normalized = converted;
                }

                // Since we forced Pbgra32, we know it is exactly 4 bytes per pixel.
                const int bytesPerPixel = 4;

                var stride = normalized.PixelWidth * bytesPerPixel;
                var bufferSize = stride * normalized.PixelHeight;

                if (bufferSize <= 0)
                    return null;

                // Use ArrayPool to prevent Large Object Heap (LOH) allocations for big images
                var rentedPixelBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                try
                {
                    normalized.CopyPixels(rentedPixelBuffer, stride, 0);

                    // Slice the rented array to the exact buffer size (Rented arrays are often larger than the requested size)
                    var hashBytes = SHA1.HashData(rentedPixelBuffer.AsSpan(0, bufferSize));
                    return Convert.ToBase64String(hashBytes);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedPixelBuffer);
                }
            }
            catch
            {
                return null;
            }

        }
    }
}
