using System;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Globalization;

namespace AmberolWpf.Helpers
{
    /// <summary>
    /// Helper class to convert base64 image strings to BitmapImage at runtime.
    /// </summary>
    public class Base64ToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string coverArt = value as string;
            if (string.IsNullOrEmpty(coverArt)) return null;

            try
            {
                if (File.Exists(coverArt))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(coverArt);
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 36; // Tiny size for list item to save RAM
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }

                if (coverArt.Contains(","))
                {
                    string base64 = coverArt.Substring(coverArt.IndexOf(",") + 1);
                    byte[] bytes = System.Convert.FromBase64String(base64);
                    using (var ms = new MemoryStream(bytes))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 36;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
