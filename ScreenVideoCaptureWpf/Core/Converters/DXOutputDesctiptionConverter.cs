using System.Globalization;
using System.Windows.Data;

namespace ScreenVideoCaptureWpf.Core.Converters
{
    public class DXOutputDesctiptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var t = value.GetType();
            if (value is SharpDX.DXGI.OutputDescription output)
            {
                switch (parameter.ToString())
                {
                    case "name": return output.DeviceName;
                    case "resolution": return $"{output.DesktopBounds.Right - output.DesktopBounds.Left}x{output.DesktopBounds.Bottom - output.DesktopBounds.Top} ({output.DesktopBounds.Left}:{output.DesktopBounds.Top})";
                }
            }

            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
