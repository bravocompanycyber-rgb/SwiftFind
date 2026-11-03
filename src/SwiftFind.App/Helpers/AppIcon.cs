using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace SwiftFind.Helpers
{
    /// <summary>Loads the brand icon and logo embedded in the executable.</summary>
    public static class AppIcon
    {
        public static Icon Load()
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SwiftFind.ico"))
                    if (s != null) return new Icon(s);
            }
            catch { }
            return null;
        }

        public static Image LoadLogo()
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png"))
                    if (s != null) return new Bitmap(s);
            }
            catch { }
            return null;
        }
    }
}
