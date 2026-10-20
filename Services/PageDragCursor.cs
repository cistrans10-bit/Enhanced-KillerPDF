using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;

namespace KillerPDF.Services
{
    internal sealed partial class PageDragCursor : IDisposable
    {
        private readonly SafeCursorHandle _handle;

        private PageDragCursor(SafeCursorHandle handle)
        {
            _handle = handle;
            Cursor = CursorInteropHelper.Create(handle);
        }

        internal Cursor Cursor { get; }

        internal static PageDragCursor? Create(BitmapSource? thumbnail, int pageCount)
        {
            if (thumbnail is null || thumbnail.PixelWidth <= 0 || thumbnail.PixelHeight <= 0)
                return null;

            const int width = 124;
            const int height = 162;
            const double maxPageWidth = 104;
            const double maxPageHeight = 138;
            double scale = Math.Min(
                maxPageWidth / thumbnail.PixelWidth,
                maxPageHeight / thumbnail.PixelHeight);
            double pageWidth = thumbnail.PixelWidth * scale;
            double pageHeight = thumbnail.PixelHeight * scale;
            var pageRect = new Rect(10, 10, pageWidth, pageHeight);

            var visual = new DrawingVisual();
            using (DrawingContext drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 1), pageRect);
                drawing.PushOpacity(0.5);
                drawing.DrawImage(thumbnail, pageRect);
                drawing.Pop();

                if (pageCount > 1)
                {
                    string count = pageCount.ToString();
                    var text = new FormattedText(
                        count,
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        11,
                        Brushes.White,
                        1);
                    double badgeWidth = Math.Max(22, text.Width + 12);
                    var badge = new Rect(
                        pageRect.Right - badgeWidth + 7,
                        pageRect.Top - 7,
                        badgeWidth,
                        20);
                    drawing.DrawRoundedRectangle(Brushes.Black, null, badge, 10, 10);
                    drawing.DrawText(text, new Point(
                        badge.Left + (badge.Width - text.Width) / 2,
                        badge.Top + (badge.Height - text.Height) / 2));
                }
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return CreateFromBitmap(bitmap);
        }

        private static PageDragCursor? CreateFromBitmap(BitmapSource bitmap)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);

            var info = new BITMAPINFO
            {
                Header = new BITMAPINFOHEADER
                {
                    Size = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32
                }
            };
            IntPtr color = CreateDIBSection(IntPtr.Zero, ref info, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (color == IntPtr.Zero || bits == IntPtr.Zero) return null;
            IntPtr mask = IntPtr.Zero;
            try
            {
                Marshal.Copy(pixels, 0, bits, pixels.Length);
                mask = CreateBitmap(width, height, 1, 1, IntPtr.Zero);
                var icon = new ICONINFO
                {
                    IsIcon = 0,
                    HotspotX = 4,
                    HotspotY = 4,
                    MaskBitmap = mask,
                    ColorBitmap = color
                };
                IntPtr cursor = CreateIconIndirect(ref icon);
                return cursor == IntPtr.Zero
                    ? null
                    : new PageDragCursor(new SafeCursorHandle(cursor));
            }
            finally
            {
                if (mask != IntPtr.Zero) DeleteObject(mask);
                DeleteObject(color);
            }
        }

        public void Dispose()
        {
            Cursor.Dispose();
            _handle.Dispose();
        }

        private sealed class SafeCursorHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            internal SafeCursorHandle(IntPtr cursor) : base(true) => SetHandle(cursor);

            protected override bool ReleaseHandle() => DestroyIcon(handle);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int Size;
            public int Width;
            public int Height;
            public short Planes;
            public short BitCount;
            public int Compression;
            public int ImageSize;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public int ColorsUsed;
            public int ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER Header;
            public int Colors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public int IsIcon;
            public int HotspotX;
            public int HotspotY;
            public IntPtr MaskBitmap;
            public IntPtr ColorBitmap;
        }

        [LibraryImport("gdi32.dll")]
        private static partial IntPtr CreateDIBSection(
            IntPtr hdc,
            ref BITMAPINFO bitmapInfo,
            uint usage,
            out IntPtr bits,
            IntPtr section,
            uint offset);

        [LibraryImport("gdi32.dll")]
        private static partial IntPtr CreateBitmap(
            int width,
            int height,
            uint planes,
            uint bitsPerPixel,
            IntPtr bits);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DeleteObject(IntPtr handle);

        [LibraryImport("user32.dll")]
        private static partial IntPtr CreateIconIndirect(ref ICONINFO iconInfo);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyIcon(IntPtr handle);
    }
}
