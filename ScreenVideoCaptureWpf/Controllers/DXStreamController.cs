using System.Collections.Concurrent;
using System.Drawing;

namespace ScreenVideoCaptureWpf.Controllers
{
    public class OnScreenUpdatedEventArgs : EventArgs
    {
        public System.Drawing.Bitmap Bitmap { get; set; }

        internal OnScreenUpdatedEventArgs(Bitmap bitmap)
        {
            Bitmap = bitmap;
        }
    }

    public class DXStreamController
    {
        private enum State : int
        {
            Starting,
            Running,
            Stopping,
            Idle
        }

        public bool SkipFrames { get; set; }



        private static ConcurrentQueue<Bitmap> _bitmapQueue { get; set; }

        public int FrameNumber { get; private set; }


        private Thread _captureThread;
        private Thread _callbackThread;

        private volatile State _state;

        public DXStreamController()
        {
            SkipFrames = true;
            _bitmapQueue = null;
            _state = State.Idle;
        }

        private void VideoStreamTask(int adapterIndex, int displayIndex)
        {
            SharpDX.DXGI.OutputDuplicateFrameInformation duplicateFrameInformation;
            SharpDX.DXGI.Resource screenResource = null;

            try
            {
                using SharpDX.DXGI.Factory1 factory1 = new SharpDX.DXGI.Factory1();
                using SharpDX.DXGI.Adapter1 adapter1 = factory1.GetAdapter1(adapterIndex);
                using SharpDX.Direct3D11.Device device = new SharpDX.Direct3D11.Device(adapter1);
                using SharpDX.DXGI.Output output = adapter1.GetOutput(displayIndex);
                using SharpDX.DXGI.Output1 output1 = output.QueryInterface<SharpDX.DXGI.Output1>();

                int width = output.Description.DesktopBounds.Right;
                int height = output.Description.DesktopBounds.Bottom;
                System.Drawing.Rectangle bounds = new System.Drawing.Rectangle(0, 0, width, height);
                var texture2DDescription = new SharpDX.Direct3D11.Texture2DDescription
                {
                    CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read,
                    BindFlags = SharpDX.Direct3D11.BindFlags.None,
                    Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                    Width = width,
                    Height = height,
                    OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = SharpDX.Direct3D11.ResourceUsage.Staging
                };

                using SharpDX.Direct3D11.Texture2D texture2D = new SharpDX.Direct3D11.Texture2D(device, texture2DDescription);
                using SharpDX.DXGI.OutputDuplication outputDuplication = output1.DuplicateOutput(device);

                _state = State.Running;
                do
                {
                    try
                    {
                        SharpDX.Result nextFrameAcquired = outputDuplication.TryAcquireNextFrame(100, out duplicateFrameInformation, out screenResource);
                        if (nextFrameAcquired.Success)
                        {
                            FrameNumber++;

                            using SharpDX.Direct3D11.Texture2D screenTexture2D = screenResource.QueryInterface<SharpDX.Direct3D11.Texture2D>();
                            device.ImmediateContext.CopyResource(screenTexture2D, texture2D);
                            SharpDX.DataBox dataBox = device.ImmediateContext.MapSubresource(texture2D, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                            using Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                            System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(bounds, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
                            nint dataBoxPointer = dataBox.DataPointer;
                            nint bitmapDataPointer = bitmapData.Scan0;
                            for (int y = 0; y < height; y++)
                            {
                                SharpDX.Utilities.CopyMemory(bitmapDataPointer, dataBoxPointer, width * 4);
                                dataBoxPointer = IntPtr.Add(dataBoxPointer, dataBox.RowPitch);
                                bitmapDataPointer = IntPtr.Add(bitmapDataPointer, bitmapData.Stride);
                            }

                            bitmap.UnlockBits(bitmapData);
                            device.ImmediateContext.UnmapSubresource(screenTexture2D, 0);

                            while (SkipFrames && _bitmapQueue.Count > 1)
                            {
                                _bitmapQueue.TryDequeue(out Bitmap dequeuedBitmap);
                                dequeuedBitmap?.Dispose();
                            }
                            if (FrameNumber > 1)
                            {
                                _bitmapQueue.Enqueue(bitmap);
                            }
                        }
                        else
                        {
                            if (SharpDX.ResultDescriptor.Find(nextFrameAcquired).ApiCode != SharpDX.Result.WaitTimeout.ApiCode)
                            {
                                nextFrameAcquired.CheckError();
                            }
                        }
                    }
                    finally
                    {
                        screenResource?.Dispose();
                        outputDuplication?.ReleaseFrame();
                    }
                } while (_state == State.Running);
            }
            catch (Exception ex)
            {
                _state = State.Stopping;
            }
            finally
            {
                _callbackThread.Join();

                _captureThread = null;
                _callbackThread = null;
                _bitmapQueue = null;

                _state = State.Idle;
            }

        }

        public void Start()
        {
            if (_state == State.Idle)
            {
                _bitmapQueue = new ConcurrentQueue<Bitmap>();
                _captureThread = new Thread(() => VideoStreamTask(0, 0));
                _callbackThread = new Thread(OnCallback);

                _state = State.Starting;

                _captureThread.Start();
                _callbackThread.Start();
            }
        }

        public void Stop()
        {
            if (_state == State.Running)
            {
                _state = State.Stopping;
            }
        }

        private void OnCallback()
        {
            try
            {
                while (_state <= State.Running)
                {
                    while (_bitmapQueue.TryDequeue(out Bitmap bitmap))
                    {
                        try
                        {
                            OnScreenUpdated?.Invoke(this, new OnScreenUpdatedEventArgs(bitmap));
                        }
                        finally
                        {
                            bitmap.Dispose();
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _state = State.Stopping;
            }
        }

        public event EventHandler<OnScreenUpdatedEventArgs>? OnScreenUpdated;
    }
}
