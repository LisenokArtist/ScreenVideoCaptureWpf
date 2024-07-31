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

namespace ScreenVideoCaptureWpf.Controllers
{
    public class CameraDevice
    {
        public int OpenCvId { get; set; }
        public string Name { get; set; }
        public string DeviceId { get; set; }
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

        public void StartCapture(CameraDevice device)
        {
            if (_captureState == CaptureState.Idle)
            {
                _waitHandle = new AutoResetEvent(false);
                _bitmapQueue = new ConcurrentQueue<Bitmap>();
                _captureThread = new Thread(() => CaptureMain(device));
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

        public void CaptureMain(CameraDevice camera)
        {
            try
            {
                using VideoCapture videoCapture = new VideoCapture();
                var isOpened = videoCapture.Open(camera.OpenCvId);
                if (!isOpened)
                {
                    throw new ApplicationException("Cannot connect to camera");
                }
                using Mat frame = new Mat();
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
            }) ?? new List<CameraDevice>();
            DeviceCollection.SetMany(cameras);
        }
    }
}
