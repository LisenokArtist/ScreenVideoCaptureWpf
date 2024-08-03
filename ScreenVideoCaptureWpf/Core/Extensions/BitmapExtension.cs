using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace ScreenVideoCaptureWpf.Core.Extensions
{
    public static class BitmapExtension
    {
        public static BitmapImage ToImageSource(this Bitmap bitmap)
        {
            using MemoryStream memory = new MemoryStream();
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
            memory.Position = 0;
            BitmapImage bitmapimage = new BitmapImage();
            bitmapimage.BeginInit();
            bitmapimage.StreamSource = memory;
            bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapimage.EndInit();

            return bitmapimage;
        }

        public static System.Drawing.Bitmap ToBitmap<TPixel>(this Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
        {
            using var memoryStream = new MemoryStream();
            var imageEncoder = image.Configuration.ImageFormatsManager.GetEncoder(PngFormat.Instance);
            image.Save(memoryStream, imageEncoder);

            memoryStream.Seek(0, SeekOrigin.Begin);

            return new System.Drawing.Bitmap(memoryStream);
        }

        public static System.Drawing.Bitmap Draw(
            this System.Drawing.Bitmap bitmap, 
            IEnumerable<YoloDotNet.Models.ObjectDetection> predictions,
            Font font = null)
        {
            font ??= new Font("Tahoma", 8);

            using Graphics graphics = Graphics.FromImage(bitmap);
            using Pen pen = new Pen(System.Drawing.Color.FromKnownColor(KnownColor.Red), 2);

            foreach (var p in predictions)
            {
                graphics.DrawPolygon(pen, new System.Drawing.Point[] {
                    new System.Drawing.Point(p.BoundingBox.Left, p.BoundingBox.Top),
                    new System.Drawing.Point(p.BoundingBox.Right, p.BoundingBox.Top),
                    new System.Drawing.Point(p.BoundingBox.Right, p.BoundingBox.Bottom),
                    new System.Drawing.Point(p.BoundingBox.Left, p.BoundingBox.Bottom)});
                graphics.DrawString($"{p.Label.Name} ({p.Confidence})", font, Brushes.Red, new System.Drawing.Point(p.BoundingBox.Left, p.BoundingBox.Top));
            }

            return bitmap;
        }
    }
}