using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace FinFlow.Desktop.Infrastructure;

/// <summary>Склеивает коллекцию строк для отображения в таблице.</summary>
public class JoinConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IEnumerable items && value is not string)
        {
            var parts = items.Cast<object>().Select(item => item?.ToString()).Where(text => !string.IsNullOrWhiteSpace(text));
            return string.Join(parameter as string ?? ", ", parts);
        }

        return value?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
