using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AmberolWpf.Helpers
{
    public static class ColorExtractor
    {
        public static List<Color> ExtractColors(BitmapSource source)
        {
            var fallback = new List<Color>
            {
                Color.FromRgb(12, 18, 30),
                Color.FromRgb(24, 38, 56),
                Color.FromRgb(40, 60, 85),
                Color.FromRgb(65, 95, 125)
            };

            if (source == null)
            {
                return fallback;
            }

            try
            {
                // Resize to 16x16 using RenderTargetBitmap for pure WPF solution without GDI+
                var target = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    context.DrawImage(source, new System.Windows.Rect(0, 0, 16, 16));
                }
                target.Render(visual);

                // Copy pixels
                byte[] pixels = new byte[16 * 16 * 4];
                int stride = 16 * 4;
                target.CopyPixels(pixels, stride, 0);

                // Collect hues
                var hues = new List<double>();

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte a = pixels[i + 3];

                    if (a < 50) continue; // Skip semi-transparent pixels

                    // RGB to HSL conversion
                    double rf = r / 255.0;
                    double gf = g / 255.0;
                    double bf = b / 255.0;

                    double max = Math.Max(rf, Math.Max(gf, bf));
                    double min = Math.Min(rf, Math.Min(gf, bf));
                    double delta = max - min;

                    if (delta < 0.15 || max < 0.1) continue; // Skip gray/near-black

                    double s = delta / max; // Saturation (HSV)
                    if (s < 0.3) continue; // Skip desaturated colors

                    double h = 0;
                    if (max == rf)
                    {
                        h = ((gf - bf) / delta) % 6;
                    }
                    else if (max == gf)
                    {
                        h = (bf - rf) / delta + 2;
                    }
                    else
                    {
                        h = (rf - gf) / delta + 4;
                    }

                    h = Math.Round(h * 60);
                    if (h < 0) h += 360;

                    // Weight hue by saturation and brightness
                    int weight = (int)Math.Round(s * max * 5);
                    for (int w = 0; w < weight; w++)
                    {
                        hues.Add(h);
                    }
                }

                if (hues.Count < 4)
                {
                    return fallback;
                }

                // Group hues into 4 buckets (0-90, 90-180, 180-270, 270-360) and find median hues
                double[] buckets = { 0, 90, 180, 270 };
                var pickedColors = new List<Color>();

                foreach (var start in buckets)
                {
                    double end = start + 90;
                    var bucket = hues.FindAll(h => h >= start && h < end);
                    double pickedHue;

                    if (bucket.Count == 0)
                    {
                        pickedHue = hues[hues.Count / 2];
                    }
                    else
                    {
                        bucket.Sort();
                        pickedHue = bucket[bucket.Count / 2];
                    }

                    // Convert HSL back to RGB using dark and rich HSL profiles to look great as backdrop gradients
                    // Saturation = 55%, Lightness = 12% (Frosted backdrop needs to be dark)
                    pickedColors.Add(HslToRgb(pickedHue, 0.55, 0.12));
                }

                return pickedColors;
            }
            catch
            {
                return fallback;
            }
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;

            double r = 0, g = 0, b = 0;

            if (h >= 0 && h < 60) { r = c; g = x; b = 0; }
            else if (h >= 60 && h < 120) { r = x; g = c; b = 0; }
            else if (h >= 120 && h < 180) { r = 0; g = c; b = x; }
            else if (h >= 180 && h < 240) { r = 0; g = x; b = c; }
            else if (h >= 240 && h < 300) { r = x; g = 0; b = c; }
            else if (h >= 300 && h < 360) { r = c; g = 0; b = x; }

            byte red = (byte)Math.Round((r + m) * 255);
            byte green = (byte)Math.Round((g + m) * 255);
            byte blue = (byte)Math.Round((b + m) * 255);

            return Color.FromRgb(red, green, blue);
        }
    }
}
