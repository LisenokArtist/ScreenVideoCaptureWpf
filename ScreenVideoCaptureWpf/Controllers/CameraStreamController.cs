using OpenCvSharp;
using ScreenVideoCaptureWpf.Core.Enums;
using DirectShowLib;
using System.Collections.ObjectModel;
using ScreenVideoCaptureWpf.Core.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using System.Drawing;
using System;
using System.Runtime.ExceptionServices;
using ScreenVideoCaptureWpf.Core.EventArgs;
using System.Windows.Navigation;
using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenVideoCaptureWpf.Controllers
{
    public class CameraDevice
    {
        public int OpenCvId { get; set; }
        public string Name { get; set; }
        public string DeviceId { get; set; }
        public List<Resolution> Resolutions { get; set; }
    }

    public class CameraStreamController
    {
        #region Events
        public event EventHandler<OnFrameUpdatedEventArgs> OnCameraFrameUpdated;
        public event EventHandler<OnCaptureStopEventArgs> OnCameraStop;
        #endregion

        #region Collections
        public ObservableCollection<CameraDevice> DeviceCollection { get; set; } = new ObservableCollection<CameraDevice>();
        #endregion

        #region Properties
        public bool SkipFirstFrame { get; set; }
        public bool SkipFrames { get; set; }
        public bool PreserveBitmap { get; set; }
        public bool IsActive => _captureState != CaptureState.Idle;
        public bool IsNotActive => _captureState == CaptureState.Idle;
        #endregion

        #region Fields
        private Exception _globalException { get; set; }
        private AutoResetEvent _waitHandle { get; set; }
        private ConcurrentQueue<Bitmap> _bitmapQueue { get; set; }
        private Thread _captureThread { get; set; }
        private Thread _callbackThread { get; set; }
        private volatile CaptureState _captureState;
        #endregion

        public CameraStreamController()
        {
            _captureState = CaptureState.Idle;

            RefreshCameraDevices();
        }

        public void StartCapture(CameraDevice device, Resolution resolution)
        {
            if (_captureState == CaptureState.Idle)
            {
                _waitHandle = new AutoResetEvent(false);
                _bitmapQueue = new ConcurrentQueue<Bitmap>();
                _captureThread = new Thread(() => CaptureMain(device, resolution));
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

        public void CaptureMain(CameraDevice camera, Resolution resolution)
        {
            try
            {
                using VideoCapture videoCapture = new VideoCapture(camera.OpenCvId, VideoCaptureAPIs.DSHOW);
                videoCapture.FrameWidth = resolution.Width;
                videoCapture.FrameHeight = resolution.Height;
                var isOpened = videoCapture.Open(camera.OpenCvId,VideoCaptureAPIs.DSHOW);
                if (!isOpened)
                {
                    throw new ApplicationException("Cannot connect to camera");
                }

                using Mat frame = new Mat();
                //Не знаю почему, но следующие две строки позволяют вернуть в VideoCapture реальный размер кадра, выводимый камерой (виртуальной)
                videoCapture.Set(VideoCaptureProperties.FrameWidth, videoCapture.Get(VideoCaptureProperties.FrameWidth));
                videoCapture.Set(VideoCaptureProperties.FrameHeight, videoCapture.Get(VideoCaptureProperties.FrameWidth));


                var width = videoCapture.FrameWidth;
                var height = videoCapture.FrameHeight;
                int frameNumber = 0;
                _captureState = CaptureState.Running;
                do
                {
                    videoCapture.Read(frame);

                    if (!frame.Empty())
                    {
                        frameNumber += 1;

                        var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame);
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
                        //frame.Dispose();
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
                if (OnCameraStop != null)
                {
                    OnCameraStop(null, new OnCaptureStopEventArgs(exception != null ? new Exception(exception.Message, exception) : null));
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
                            OnCameraFrameUpdated?.Invoke(null, new OnFrameUpdatedEventArgs(bitmap));
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

        public void RefreshCameraDevices()
        {
            var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            var openCVId = 0;
            var cameras = devices.Select(x => new CameraDevice()
            {
                DeviceId = x.DevicePath,
                Name = x.Name,
                OpenCvId = openCVId++,
                Resolutions = GetAllAvailableResolution(x),
            }) ?? new List<CameraDevice>();
            DeviceCollection.SetMany(cameras);
        }

        private static List<Resolution> GetAllAvailableResolution(DsDevice vidDev)
        {
            var AvailableResolutions = new List<Resolution>();

            try
            {
                IAMStreamConfig config = null;

                int hr, bitCount = 0;

                IBaseFilter sourceFilter = null;
                var m_FilterGraph2 = new FilterGraph() as IFilterGraph2;
                hr = m_FilterGraph2.AddSourceFilterForMoniker(vidDev.Mon, null, vidDev.Name, out sourceFilter);
                var pRaw2 = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);
                if (pRaw2 == null) return AvailableResolutions;

                VideoInfoHeader v = new VideoInfoHeader();
                IEnumMediaTypes mediaTypeEnum;
                hr = pRaw2.EnumMediaTypes(out mediaTypeEnum);

                AMMediaType[] mediaTypes = new AMMediaType[1];
                IntPtr fetched = IntPtr.Zero;
                hr = mediaTypeEnum.Next(1, mediaTypes, fetched);

                while (fetched != null && mediaTypes[0] != null)
                {
                    Marshal.PtrToStructure(mediaTypes[0].formatPtr, v);
                    if (v.BmiHeader.Size != 0 && v.BmiHeader.BitCount != 0)
                    {
                        if (v.BmiHeader.BitCount > bitCount)
                        {
                            AvailableResolutions.Clear();
                            bitCount = v.BmiHeader.BitCount;
                        }
                        AvailableResolutions.Add(new Resolution(v.BmiHeader.Width, v.BmiHeader.Height));
                    }
                    hr = mediaTypeEnum.Next(1, mediaTypes, fetched);
                }
                return AvailableResolutions.DistinctBy(x => $"{x.Width} {x.Height}").ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return AvailableResolutions;
            }
        }

        private static string GetResolutionForMediaType(AMMediaType media_type)
        {
            VideoInfoHeader videoInfoHeader = new VideoInfoHeader();
            Marshal.PtrToStructure(media_type.formatPtr, videoInfoHeader);

            return $"{videoInfoHeader.BmiHeader.Width}x{videoInfoHeader.BmiHeader.Height}";
        }
    }

    public class Resolution
    {
        public readonly int Width;
        public readonly int Height;
        public readonly string Ratio;

        public Resolution(int width, int height)
        {
            Width = width;
            Height = height;
            var gcd = GetGreatestCommonDivisor(width, height);
            Ratio = $"{width / gcd}:{height / gcd}";
        }

        public override string ToString()
        {
            return $"{Width}x{Height} [{Ratio}]";
        }

        private static int GetGreatestCommonDivisor(int a, int b)
        {
            int Remainder;

            while (b != 0)
            {
                Remainder = a % b;
                a = b;
                b = Remainder;
            }

            return a;
        }
    }
}
