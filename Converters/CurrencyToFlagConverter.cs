// CurrencyToFlagConverter.cs
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ADLMRateGen.Converters
{
	public class CurrencyToFlagConverter : IValueConverter
	{
		private readonly string _asmAndPath;

		public CurrencyToFlagConverter()
		{
			// e.g. "ADLMRateGen"
			var asmName = Assembly.GetExecutingAssembly().GetName().Name;
			// base pack URI up to your Resources folder
			_asmAndPath = $"pack://application:,,,/{asmName};component/Resources/";
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string code && !string.IsNullOrWhiteSpace(code))
			{
				code = code.ToLowerInvariant();

				// Try PNG first
				var uriPng = new Uri($"{_asmAndPath}{code}.png", UriKind.Absolute);
				if (ResourceExists(uriPng))
					return new BitmapImage(uriPng);

				// Then try .jpg or .jpeg
				var uriJpg = new Uri($"{_asmAndPath}{code}.jpg", UriKind.Absolute);
				if (ResourceExists(uriJpg))
					return new BitmapImage(uriJpg);

				var uriJpeg = new Uri($"{_asmAndPath}{code}.jpeg", UriKind.Absolute);
				if (ResourceExists(uriJpeg))
					return new BitmapImage(uriJpeg);
			}

			return null;
		}

		private bool ResourceExists(Uri uri)
		{
			try
			{
				var bitmap = new BitmapImage();
				bitmap.BeginInit();
				bitmap.UriSource = uri;
				bitmap.CacheOption = BitmapCacheOption.None;
				bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
				bitmap.EndInit();
				return true;
			}
			catch (IOException)
			{
				return false;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
