﻿using ScreenVideoCaptureWpf.Controllers;
using ScreenVideoCaptureWpf.Core;
using ScreenVideoCaptureWpf.Core.EventArgs;
using ScreenVideoCaptureWpf.Core.Extensions;
using System.Windows.Controls;

namespace ScreenVideoCaptureWpf.ViewModels
{
    public class VideoCaptureVM
    {
        #region Properties
        public DXStreamController DXStreamController { get; private set; }
        public CameraStreamController CameraStreamController { get; private set; }
        public ObjectDetectionController ObjectDetectionController { get; private set; }

        public SharpDX.DXGI.Adapter SelectedAdapter { get; set; }
        public SharpDX.DXGI.Output SelectedOutput { get; set; }
        public CameraDevice SelectedCameraDevice { get; set; }
        public Resolution SelectedCameraResolution { get; set; }
        public string SelectedCameraResolutionWidth { get; set; }
        public string SelectedCameraResolutionHeight { get; set; }
        public bool IsManualResolutionSelection { get; set; }

        #endregion

        #region Fields
        private TabControl _tabControl;
        private Image _imageControl;
        #endregion

        #region Commands
        private RelayCommand _startCaptureCommand;
        public RelayCommand StartCaptureCommand => _startCaptureCommand ??= new RelayCommand(StartCapture);

        private RelayCommand _stopCaptureCommand;
        public RelayCommand StopCaptureCommand => _stopCaptureCommand ??= new RelayCommand(StopCapture);
        #endregion

        #region Constructor
        public VideoCaptureVM(TabControl tabControl, Image imageControl)
        {
            _tabControl = tabControl;
            _imageControl = imageControl;

            InitializeObjectDetectionController();
            InitializeDXStreamController();
            InitializeCameraStreamController();
        }

        private void InitializeObjectDetectionController()
        {
            ObjectDetectionController = new ObjectDetectionController();
        }

        private void InitializeDXStreamController()
        {
            DXStreamController = new DXStreamController();

            DXStreamController.OnScreenUpdated += OnFrameUpdated;
            DXStreamController.OnCaptureStop += OnCaptureStop;
        }

        private void InitializeCameraStreamController()
        {
            CameraStreamController = new CameraStreamController();

            CameraStreamController.OnCameraFrameUpdated += OnFrameUpdated; ;
            CameraStreamController.OnCameraStop += OnCaptureStop; ;
        }
        #endregion

        #region Events
        private void OnFrameUpdated(object? sender, OnFrameUpdatedEventArgs e)
        {
            App.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (e?.Bitmap != null)
                {
                    var mutated = ObjectDetectionController.Mutate(e.Bitmap);
                    _imageControl.Source = mutated.ToImageSource();
                    //_imageControl.Source = e.Bitmap.ToImageSource();
                }
            }));
        }

        private void OnCaptureStop(object? sender, OnCaptureStopEventArgs e)
        {
            if (e.Exception != null)
            {
                //throw e.Exception;
            }
        }
        #endregion

        #region Actions
        private void StartCapture()
        {
            if (DXStreamController.IsActive) return;
            if (CameraStreamController.IsActive) return;

            switch (_tabControl.SelectedIndex)
            {
                case 0:
                    if (DXStreamController.IsNotActive)
                    {
                        if (SelectedAdapter != null && SelectedOutput != null)
                        {
                            var selectedAdapterIndex = DXStreamController.AdaptersCollection.IndexOf(SelectedAdapter);
                            var selectedOutputIndex = System.Array.FindIndex(SelectedAdapter.Outputs, x => x.Description.DeviceName == SelectedOutput.Description.DeviceName);
                            DXStreamController.StartCapture(selectedOutputIndex, selectedAdapterIndex);
                        }
                    } 
                    break;
                case 1:
                    if (CameraStreamController.IsNotActive)
                    {
                        if (SelectedCameraDevice != null)
                        {
                            if (IsManualResolutionSelection)
                            {
                                var w = Convert.ToInt32(SelectedCameraResolutionWidth);
                                var h = Convert.ToInt32(SelectedCameraResolutionHeight);
                                if (w != 0 && h != 0)
                                    CameraStreamController.StartCapture(SelectedCameraDevice, new Resolution(w, h));
                            }
                            else
                            {
                                if (SelectedCameraResolution != null)
                                {
                                    CameraStreamController.StartCapture(SelectedCameraDevice, SelectedCameraResolution);
                                };
                            }
                        }
                    }
                    break;
            }

        }

        private void StopCapture()
        {
            DXStreamController.StopCapture();
            CameraStreamController.StopCapture();
        }
        #endregion
    }
}
