using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Tesseract;

namespace LicensePlateRecognition {
    static class BitmapConverter {
        public static BitmapSource ToBitmapSource(Bitmap bitmap) {
            return convert(bitmap);
        }

        public static Bitmap ToBitmap(BitmapSource bitmapSource) {
            return null;
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        private static BitmapSource convert(this System.Drawing.Bitmap source) {
            if (source == null) {
                throw new NullReferenceException();
            }

            IntPtr ip = source.GetHbitmap();
            try {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(ip, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            } finally {
                DeleteObject(ip);
            }
        }
    }
}