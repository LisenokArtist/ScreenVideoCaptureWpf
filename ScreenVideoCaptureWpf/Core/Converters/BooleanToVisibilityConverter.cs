using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace ScreenVideoCaptureWpf.Core.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean) return boolean == true ? Visibility.Visible : Visibility.Collapsed;
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility) return visibility == Visibility.Visible ? true : false;
            return Binding.DoNothing;
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean) return boolean == true ? Visibility.Collapsed : Visibility.Visible;
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility) return visibility == Visibility.Visible ? false : true;
            return Binding.DoNothing;
        }
    }
}
