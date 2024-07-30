using ScreenVideoCaptureWpf.Controllers;
using ScreenVideoCaptureWpf.Core;
using ScreenVideoCaptureWpf.Core.Extensions;
using System.Windows.Controls;

namespace ScreenVideoCaptureWpf.ViewModels
{
    public class VideoCaptureVM
    {
        public enum CaptureMode : int
        {
            Desktop,
            Process,
            Camera
        }

        public CaptureMode SelectedMode { get; set; }

        private Image _imageControl;
        public DXStreamController DXStreamController { get; private set; }
        public SharpDX.DXGI.Adapter SelectedAdapter { get; set; }
        public SharpDX.DXGI.Output SelectedOutput { get; set; }

        #region Commands
        private RelayCommand _startCaptureCommand;
        public RelayCommand StartCaptureCommand => _startCaptureCommand ??= new RelayCommand(StartCapture);

        private RelayCommand _stopCaptureCommand;
        public RelayCommand StopCaptureCommand => _stopCaptureCommand ??= new RelayCommand(StopCapture);
        #endregion

        #region Constructor
        public VideoCaptureVM(Image imageControl)
        {
            _imageControl = imageControl;

            InitializeDXStreamController();
        }

        private void InitializeDXStreamController()
        {
            DXStreamController = new DXStreamController();

            DXStreamController.OnScreenUpdated += DXStreamController_OnScreenUpdated;
            DXStreamController.OnCaptureStop += DXStreamController_OnCaptureStop;
        }
        #endregion

        #region Events
        private void DXStreamController_OnCaptureStop(object? sender, OnCaptureStopEventArgs e)
        {
            if (e.Exception != null)
            {
                throw e.Exception;
            }
        }

        private void DXStreamController_OnScreenUpdated(object? sender, OnScreenUpdatedEventArgs e)
        {
            App.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (e?.Bitmap != null)
                {
                    _imageControl.Source = e.Bitmap.ToImageSource();
                }
            }));
        }
        #endregion

        #region Actions
        private void StartCapture()
        {
            if (DXStreamController.IsNotActive)
            {
                if (SelectedAdapter != null && SelectedOutput != null)
                {
                    var selectedAdapterIndex = DXStreamController.AdaptersCollection.IndexOf(SelectedAdapter);
                    var selectedOutputIndex = System.Array.FindIndex(SelectedAdapter.Outputs, x => x.Description.DeviceName == SelectedOutput.Description.DeviceName);
                    DXStreamController.StartCapture(selectedOutputIndex, selectedAdapterIndex);
                }
            }
        }

        private void StopCapture()
        {
            DXStreamController.StopCapture();
        }
        #endregion
    }
}
