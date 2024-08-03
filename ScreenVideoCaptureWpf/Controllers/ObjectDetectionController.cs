using OpenCvSharp.Extensions;
using ScreenVideoCaptureWpf.Core.Extensions;
using SixLabors.ImageSharp.Drawing.Processing;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Extensions;

namespace ScreenVideoCaptureWpf.Controllers
{
    public class ObjectDetectionController
    {
        private readonly Yolo Yolo;
        //https://github.com/NickSwardh/YoloDotNet/tree/master/test/assets/Models
        public ObjectDetectionController()
        {
            var options = new YoloDotNet.Models.YoloOptions
            {
                OnnxModel = "D:\\Repos\\ScreenVideoCaptureWpf\\ScreenVideoCaptureWpf\\Assets\\ObjectDetectionModels\\yolov10s.onnx",
                ModelType = YoloDotNet.Enums.ModelType.ObjectDetection,
                Cuda = false,
                GpuId = 0,
                PrimeGpu = false,
            };
            Yolo = new Yolo(options);
        }

        public System.Drawing.Bitmap Mutate(System.Drawing.Bitmap bitmap)
        {
            var img = SKImage.FromEncodedData(bitmap.ToMat().ToBytes());
            var res = Yolo.RunObjectDetection(img, confidence: 0.25, iou: 0.7);

            return bitmap.Draw(res);
        }
    }
}
