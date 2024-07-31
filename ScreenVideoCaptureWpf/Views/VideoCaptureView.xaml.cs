using ScreenVideoCaptureWpf.ViewModels;
using System.Windows.Controls;

namespace ScreenVideoCaptureWpf.Views
{
    /// <summary>
    /// Логика взаимодействия для VideoCaptureView.xaml
    /// </summary>
    public partial class VideoCaptureView : UserControl
    {
        public VideoCaptureView()
        {
            InitializeComponent();
            DataContext = new VideoCaptureVM(tabControl, imageControl);
        }
    }
}
