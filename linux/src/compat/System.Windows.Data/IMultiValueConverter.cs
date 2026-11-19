using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace System.Windows.Data;

/// <summary>
/// WPF's multi-value converter contract (object[] in, object[] back). Avalonia passes an IList instead;
/// the default implementation below bridges the two, so upstream converters work in Avalonia multi-bindings as is.
/// </summary>
public interface IMultiValueConverter : Avalonia.Data.Converters.IMultiValueConverter
{
    object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
    object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);

    object Avalonia.Data.Converters.IMultiValueConverter.Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        => Convert(values.ToArray(), targetType, parameter, culture);
}
