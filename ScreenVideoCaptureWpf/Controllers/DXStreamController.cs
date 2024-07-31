using ScreenVideoCaptureWpf.Core.Enums;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Drawing;
using ScreenVideoCaptureWpf.Core.Extensions;
using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using ScreenVideoCaptureWpf.Core.EventArgs;

namespace ScreenVideoCaptureWpf.Controllers
{

    public class DXStreamController
    {
        #region Events
        public event EventHandler<OnFrameUpdatedEventArgs> OnScreenUpdated;
        public event EventHandler<OnCaptureStopEventArgs> OnCaptureStop;
        #endregion

        #region Collections
        public ObservableCollection<SharpDX.DXGI.Adapter> AdaptersCollection { get; set; } = new ObservableCollection<SharpDX.DXGI.Adapter>();
        #endregion

        #region Properties
        public bool SkipFirstFrame { get; set; }
        public bool SkipFrames { get; set; }
        public bool PreserveBitmap { get; set; }
        public bool IsActive => _captureState != CaptureState.Idle;
        public bool IsNotActive => _captureState == CaptureState.Idle;
        #endregion

        #region Fields
        private SharpDX.DXGI.Factory1 _factory;
        private Exception _globalException { get; set; }
        private AutoResetEvent _waitHandle { get; set; }
        private ConcurrentQueue<Bitmap> _bitmapQueue { get; set; }
        private Thread _captureThread { get; set; }
        private Thread _callbackThread { get; set; }

        private volatile CaptureState _captureState;
        #endregion

        public DXStreamController()
        {
            _globalException = null;
            _waitHandle = null;
            _bitmapQueue = null;
            _captureThread = null;
            _callbackThread = null;
            _captureState = CaptureState.Idle;
            SkipFirstFrame = true;
            SkipFrames = true;
            PreserveBitmap = false;

            _factory = new SharpDX.DXGI.Factory1();
            AdaptersCollection.SetMany(_factory.Adapters ?? System.Array.Empty<SharpDX.DXGI.Adapter>());
        }


        public void StartCapture(Int32 displayIndex = 0, Int32 adapterIndex = 0)
        {
            if (_captureState == CaptureState.Idle)
            {
                _waitHandle = new AutoResetEvent(false);
                _bitmapQueue = new ConcurrentQueue<Bitmap>();
                _captureThread = new Thread(() => CaptureMain(adapterIndex, displayIndex));
                _callbackThread = new Thread(() => CallbackMain());
                _captureState = CaptureState.Starting;
                _captureThread.Priority = ThreadPriority.Highest;
                _captureThread.Start();
                _callbackThread.Start();
            }
        }

        public void StopCapture()
        {
            if (_captureState == CaptureState.Running)
            {
                _captureState = CaptureState.Stopping;
            }
        }

        private void CaptureMain(Int32 adapterIndex, Int32 displayIndex)
        {
            SharpDX.DXGI.Resource screenResource = null;
            try
            {
                using SharpDX.DXGI.Adapter1 adapter1 = _factory.GetAdapter1(adapterIndex);
                using SharpDX.Direct3D11.Device device = new SharpDX.Direct3D11.Device(adapter1);
                using SharpDX.DXGI.Output output = adapter1.GetOutput(displayIndex);
                using SharpDX.DXGI.Output1 output1 = output.QueryInterface<SharpDX.DXGI.Output1>();
                Int32 width = output1.Description.DesktopBounds.Right - output1.Description.DesktopBounds.Left;
                Int32 height = output1.Description.DesktopBounds.Bottom - output1.Description.DesktopBounds.Top;
                Rectangle bounds = new Rectangle(Point.Empty, new Size(width, height));
                SharpDX.Direct3D11.Texture2DDescription texture2DDescription = new SharpDX.Direct3D11.Texture2DDescription
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
                _captureState = CaptureState.Running;
                Int32 frameNumber = 0;
                do
                {
                    try
                    {
                        SharpDX.Result result = outputDuplication.TryAcquireNextFrame(100, out _, out screenResource);
                        if (result.Success)
                        {
                            frameNumber += 1;

                            using SharpDX.Direct3D11.Texture2D screenTexture2D = screenResource.QueryInterface<SharpDX.Direct3D11.Texture2D>();
                            device.ImmediateContext.CopyResource(screenTexture2D, texture2D);
                            SharpDX.DataBox dataBox = device.ImmediateContext.MapSubresource(texture2D, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
                            BitmapData bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                            IntPtr dataBoxPointer = dataBox.DataPointer;
                            IntPtr bitmapDataPointer = bitmapData.Scan0;
                            for (Int32 y = 0; y < height; y++)
                            {
                                SharpDX.Utilities.CopyMemory(bitmapDataPointer, dataBoxPointer, width * 4);
                                dataBoxPointer = IntPtr.Add(dataBoxPointer, dataBox.RowPitch);
                                bitmapDataPointer = IntPtr.Add(bitmapDataPointer, bitmapData.Stride);
                            }
                            bitmap.UnlockBits(bitmapData);
                            device.ImmediateContext.UnmapSubresource(texture2D, 0);
                            while (SkipFrames && _bitmapQueue.Count > 1)
                            {
                                _bitmapQueue.TryDequeue(out Bitmap dequeuedBitmap);
                                dequeuedBitmap.Dispose();
                            }
                            if (frameNumber > 1 || SkipFirstFrame == false)
                            {
                                _bitmapQueue.Enqueue(bitmap);
                                _waitHandle.Set();
                            }
                        }
                        else
                        {
                            if (SharpDX.ResultDescriptor.Find(result).ApiCode != SharpDX.Result.WaitTimeout.ApiCode)
                            {
                                result.CheckError();
                            }
                        }
                    }
                    finally
                    {
                        screenResource?.Dispose();
                        try
                        {
                            outputDuplication.ReleaseFrame();
                        }
                        catch { }
                    }
                } while (_captureState == CaptureState.Running);
            }
            catch (Exception exception)
            {
                _globalException = exception;
                _captureState = CaptureState.Running;
            }
            finally
            {
                _callbackThread.Join();
                Exception exception = _globalException;
                while (_bitmapQueue.Count > 0)
                {
                    _bitmapQueue.TryDequeue(out Bitmap dequeuedBitmap);
                    dequeuedBitmap.Dispose();
                }
                _globalException = null;
                _waitHandle = null;
                _bitmapQueue = null;
                _captureThread = null;
                _callbackThread = null;
                _captureState = CaptureState.Idle;
                if (OnCaptureStop != null)
                {
                    OnCaptureStop(null, new OnCaptureStopEventArgs(exception != null ? new Exception(exception.Message, exception) : null));
                }
                else
                {
                    if (exception != null)
                    {
                        ExceptionDispatchInfo.Capture(exception).Throw();
                    }
                }
            }
        }

        private void CallbackMain()
        {
            try
            {
                while (_captureState <= CaptureState.Running)
                {
                    while (_waitHandle.WaitOne(10) && _bitmapQueue.TryDequeue(out Bitmap bitmap))
                    {
                        try
                        {
                            OnScreenUpdated?.Invoke(null, new OnFrameUpdatedEventArgs(bitmap));
                        }
                        finally
                        {
                            if (!PreserveBitmap)
                            {
                                bitmap.Dispose();
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _globalException = exception;
                _captureState = CaptureState.Stopping;
            }
        }

    }
}
