using System.Windows.Media.Imaging;

namespace CheckingForClosedApplication.Helpers;

public class ImageHelper
{
    public static BitmapImage GetImage(string path, int? width = null, int? height = null)
    {
        var bitmap = new BitmapImage();

        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.None;

        if (width.HasValue)
            bitmap.DecodePixelWidth = width.Value;

        if (height.HasValue)
            bitmap.DecodePixelHeight = height.Value;

        bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }
}