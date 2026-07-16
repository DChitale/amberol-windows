using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AmberolWpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ApplyEmbeddedFont();
        }

        private static void ApplyEmbeddedFont()
        {
            try
            {
                // Load Google Sans variable font from the embedded resource folder.
                // The variable font (GoogleSans-VariableFont_GRAD,opsz,wght.ttf) registers
                // its family name as "Google Sans" inside the TTF metadata.
                var fontUri = new Uri("pack://application:,,,/font/", UriKind.Absolute);
                var families = Fonts.GetFontFamilies(fontUri);

                // Prefer exact "Google Sans" (variable font) over "Google Sans 17pt Text"
                FontFamily? googleSans = null;
                FontFamily? fallback = null;

                foreach (var family in families)
                {
                    var name = family.Source;
                    if (name.EndsWith("#Google Sans", StringComparison.OrdinalIgnoreCase))
                    {
                        googleSans = family;
                        break; // exact match — stop searching
                    }
                    if (name.Contains("Google Sans", StringComparison.OrdinalIgnoreCase))
                    {
                        fallback ??= family;
                    }
                }

                var chosen = googleSans ?? fallback;

                if (chosen != null)
                {
                    // Apply as a global default Style so every control inherits the font
                    // without needing any per-element FontFamily attribute in XAML.
                    var tbStyle = new Style(typeof(TextBlock));
                    tbStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, chosen));
                    Current.Resources[typeof(TextBlock)] = tbStyle;

                    var ctrlStyle = new Style(typeof(Control));
                    ctrlStyle.Setters.Add(new Setter(Control.FontFamilyProperty, chosen));
                    Current.Resources[typeof(Control)] = ctrlStyle;

                    var winStyle = new Style(typeof(Window));
                    winStyle.Setters.Add(new Setter(Window.FontFamilyProperty, chosen));
                    Current.Resources[typeof(Window)] = winStyle;
                }
            }
            catch
            {
                // If font loading fails for any reason, fall back to system fonts silently
            }
        }
    }
}
