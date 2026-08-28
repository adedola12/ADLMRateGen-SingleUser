using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Stamps the ADLM mark onto screenshots of this app, without putting
    /// anything on screen while nobody is capturing.
    ///
    /// HOW, AND WHAT IT CANNOT DO
    ///
    /// There is no Windows notification that says "a capture is happening", and
    /// no way to make one appear only inside a capture: a screenshot records the
    /// pixels that were already on screen, so anything drawn in response to the
    /// key press is drawn after the picture was taken. Detecting PrintScreen in a
    /// keyboard hook and showing an overlay loses that race almost every time.
    ///
    /// What does work is the other end. PrintScreen, Alt+PrintScreen and
    /// Win+Shift+S all put their image on the CLIPBOARD, so the mark can be
    /// applied to the picture after the fact and before the user pastes it. The
    /// screen stays clean and the exported image carries the mark.
    ///
    /// It therefore covers the clipboard routes and nothing else. A capture tool
    /// writing straight to a file, a screen recorder, and a phone pointed at the
    /// monitor are all untouched — the last of those is beyond any software.
    /// An always-on overlay is the only thing that survives a camera; this is the
    /// trade for not showing one.
    ///
    /// CLIPBOARD MANNERS
    ///
    /// Rewriting the clipboard is intrusive, so this is deliberately narrow: it
    /// acts only on image data, only while this app's window is open, and only
    /// when the capture plausibly belongs to it — either the window is in front,
    /// or the thing in front is a known capture tool, or it was in front within
    /// the last few seconds. A screenshot of somebody's bank statement taken
    /// while this app happens to be running should come back untouched.
    /// </summary>
    public static class ScreenshotWatermark
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hwnd, out int pid);

        private static HwndSource? _source;
        private static IntPtr _hwnd = IntPtr.Zero;

        /// <summary>
        /// Ignore clipboard updates until this moment.
        ///
        /// Writing the stamped image back is itself a clipboard change, and the
        /// notification for it arrives asynchronously — after the write call has
        /// returned. A plain "am I writing" flag is therefore always false again
        /// by the time our own update lands, and the mark gets applied to the
        /// already-marked image over and over, darkening with each pass.
        /// </summary>
        private static DateTime _suppressUntil = DateTime.MinValue;

        private static DateTime _lastForeground = DateTime.MinValue;

        /// <summary>Capture tools that take the foreground while the user snips.
        /// Win+Shift+S hands over to one of these, so requiring OUR window to be
        /// in front would miss the most common case of all.</summary>
        private static readonly string[] CaptureProcesses =
        {
            "ScreenClippingHost", "SnippingTool", "ScreenSketch", "Snip & Sketch",
            "GreenshotTool", "Greenshot", "ShareX", "Lightshot",
        };

        public static bool IsEnabled { get; set; } = true;

        public static void Attach(Window window)
        {
            if (window == null || _source != null) return;

            var helper = new WindowInteropHelper(window);
            _hwnd = helper.Handle == IntPtr.Zero ? helper.EnsureHandle() : helper.Handle;

            _source = HwndSource.FromHwnd(_hwnd);
            if (_source == null) return;

            _source.AddHook(WndProc);
            AddClipboardFormatListener(_hwnd);

            // Remember when this window last had focus, so a snip taken from it
            // still counts once the snipping overlay has taken the foreground.
            window.Activated += (_, __) => _lastForeground = DateTime.UtcNow;
            window.Deactivated += (_, __) => _lastForeground = DateTime.UtcNow;
            window.Closed += (_, __) => Detach();
        }

        public static void Detach()
        {
            if (_source == null) return;
            try
            {
                RemoveClipboardFormatListener(_hwnd);
                _source.RemoveHook(WndProc);
            }
            catch { }
            _source = null;
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE && IsEnabled && DateTime.UtcNow >= _suppressUntil)
                TryStamp();

            return IntPtr.Zero;
        }

        private static bool CaptureLooksLikeOurs()
        {
            var fg = GetForegroundWindow();
            if (fg == _hwnd) return true;

            // The snipping overlay is in front rather than us during a snip.
            try
            {
                GetWindowThreadProcessId(fg, out var pid);
                if (pid > 0)
                {
                    var name = Process.GetProcessById(pid).ProcessName;
                    foreach (var candidate in CaptureProcesses)
                        if (name.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                }
            }
            catch { }

            // Or the user was on this app a moment ago. Kept short on purpose:
            // long enough for a snip, too short to catch an unrelated screenshot
            // taken after moving on to something else.
            return (DateTime.UtcNow - _lastForeground) < TimeSpan.FromSeconds(6);
        }

        private static void TryStamp()
        {
            // The clipboard is not ready the instant the message arrives, and the
            // owner may still hold it. Come back on the dispatcher so the read
            // happens after the capture tool has finished writing.
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!CaptureLooksLikeOurs()) return;
                    if (!Clipboard.ContainsImage()) return;

                    var source = Clipboard.GetImage();
                    if (source == null || source.PixelWidth < 8 || source.PixelHeight < 8) return;

                    var stamped = Stamp(source);
                    if (stamped == null) return;

                    // Quiet before the write, not after: the notification for our
                    // own change arrives later than this method returns.
                    _suppressUntil = DateTime.UtcNow.AddSeconds(2);
                    Clipboard.SetImage(stamped);
                }
                catch
                {
                    // A clipboard held by another process, an odd image format, a
                    // capture already gone: none of it is worth interrupting the
                    // user over. The screenshot simply goes out unmarked.
                }

            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>Draws the tiled mark over a copy of the captured image.</summary>
        private static BitmapSource? Stamp(BitmapSource source)
        {
            var brush = Application.Current?.TryFindResource("WatermarkStampBrush") as Brush;
            if (brush == null) return null;

            int w = source.PixelWidth, h = source.PixelHeight;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, w, h));
                dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
            }

            // 96 dpi against pixel dimensions, so the result is the same size as
            // what was captured rather than rescaled by the source's own dpi.
            var target = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            return target;
        }
    }
}
