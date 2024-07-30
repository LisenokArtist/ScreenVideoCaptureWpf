using System.Windows.Controls;

namespace ScreenVideoCaptureWpf.ViewModels
{
    public class VideoCaptureVM
    {
        private Image _imageControl;

        public VideoCaptureVM(Image imageControl)
        {
            _imageControl = imageControl;
        }
    }
}
