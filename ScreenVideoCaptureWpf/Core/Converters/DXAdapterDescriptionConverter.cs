using System.Globalization;
using System.Windows.Data;

namespace ScreenVideoCaptureWpf.Core.Converters
{
    public class DXAdapterDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SharpDX.DXGI.AdapterDescription desc)
            {
                return desc.Description;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
