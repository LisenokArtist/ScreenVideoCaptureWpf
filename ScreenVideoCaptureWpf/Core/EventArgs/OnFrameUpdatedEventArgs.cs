using System.Drawing;

namespace ScreenVideoCaptureWpf.Core.EventArgs
{
    public class OnFrameUpdatedEventArgs
    {
        public Bitmap Bitmap { get; set; }

        public OnFrameUpdatedEventArgs(Bitmap bitmap)
        {
            this.Bitmap = bitmap;
        }
    }
}
