using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Qcm_app.Converters
{
	public  class InvertedBoolConverter : IValueConverter
	{
		public object Convert (object? value , Type targetType , object? parameter , CultureInfo culture )
		{
			return value is bool b ? !b : value;
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return value is bool b ? !b : value;
		}
	}
}
