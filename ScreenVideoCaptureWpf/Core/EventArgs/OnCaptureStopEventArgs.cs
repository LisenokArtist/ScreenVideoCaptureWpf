namespace ScreenVideoCaptureWpf.Core.EventArgs
{
    public class OnCaptureStopEventArgs
    {
        public Exception Exception { get; set; }

        public OnCaptureStopEventArgs(Exception exception)
        {
            this.Exception = exception;
        }
    }
}
