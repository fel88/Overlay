using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace Karna.Magnification
{
    public class Magnifier : IDisposable
    {
        private Form form;
        public IntPtr hwndMag;
        private float magnification;
        private bool initialized;
        private RECT magWindowRect = new RECT();
        private System.Windows.Forms.Timer timer;

        Form addf;
        public Magnifier(Form form,Form add=null)
        {
            addf = add;
            if (form == null)
                throw new ArgumentNullException("form");

            magnification = 1.0f;
            this.form = form;
            this.form.Resize += new EventHandler(form_Resize);
            this.form.FormClosing += new FormClosingEventHandler(form_FormClosing);

            timer = new Timer();
            timer.Tick += new EventHandler(timer_Tick);

            initialized = NativeMethods.MagInitialize();
            if (initialized)
            {
                SetupMagnifier();
                timer.Interval = NativeMethods.USER_TIMER_MINIMUM;
                timer.Enabled = true;
            }
        }

        void form_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer.Enabled = false;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            UpdateMaginifier();
        }

        void form_Resize(object sender, EventArgs e)
        {
            ResizeMagnifier();
        }

        ~Magnifier()
        {
            Dispose(false);
        }

        protected virtual void ResizeMagnifier()
        {
            if (initialized && (hwndMag != IntPtr.Zero))
            {
                NativeMethods.GetClientRect(form.Handle, ref magWindowRect);
                // Resize the control to fill the window.
                NativeMethods.SetWindowPos(hwndMag, IntPtr.Zero,
                    magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, 0);
            }
        }

        public static RECT lastRect=new RECT();
        public virtual void UpdateMaginifier()
        {
            if ((!initialized) || (hwndMag == IntPtr.Zero))
                return;

            POINT mousePoint = new POINT();
            RECT sourceRect = new RECT();

            NativeMethods.GetCursorPos(ref mousePoint);

            int width = (int)((magWindowRect.right - magWindowRect.left) / magnification);
            int height = (int)((magWindowRect.bottom - magWindowRect.top) / magnification);

            sourceRect.left = mousePoint.x - width / 2;
            sourceRect.top = mousePoint.y - height / 2;


            // Don't scroll outside desktop area.
            if (sourceRect.left < 0)
            {
                sourceRect.left = 0;
            }
            if (sourceRect.left > NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) - width)
            {
                sourceRect.left = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) - width;
            }
            sourceRect.right = sourceRect.left + width;

            if (sourceRect.top < 0)
            {
                sourceRect.top = 0;
            }
            if (sourceRect.top > NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN) - height)
            {
                sourceRect.top = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN) - height;
            }
            sourceRect.bottom = sourceRect.top + height;

            if (this.form == null)
            {
                timer.Enabled = false;
                return;
            }

            if (this.form.IsDisposed)
            {
                timer.Enabled = false;
                return;
            }

            // Set the source rectangle for the magnifier control.
            NativeMethods.MagSetWindowSource(hwndMag, sourceRect);
            lastRect = sourceRect;
            // Reclaim topmost status, to prevent unmagnified menus from remaining in view. 
            NativeMethods.SetWindowPos(form.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                (int)SetWindowPosFlags.SWP_NOACTIVATE | (int)SetWindowPosFlags.SWP_NOMOVE | (int)SetWindowPosFlags.SWP_NOSIZE);

            // Force redraw.
            NativeMethods.InvalidateRect(hwndMag, IntPtr.Zero, true);
        }

        public float Magnification
        {
            get { return magnification; }
            set
            {
                if (magnification != value)
                {
                    magnification = value;
                    // Set the magnification factor.
                    Transformation matrix = new Transformation(magnification);
                    NativeMethods.MagSetWindowTransform(hwndMag, ref matrix);
                }
            }
        }

        public static Bitmap LastBmp;
        public static object lock1 = new object();
        protected void SetupMagnifier()
        {
            if (!initialized)
                return;

            IntPtr hInst;

            hInst = NativeMethods.GetModuleHandle(null);

            // Make the window opaque.
            form.AllowTransparency = true;
            form.TransparencyKey = Color.Empty;
            form.Opacity = 255;

            // Create a magnifier control that fills the client area.
            NativeMethods.GetClientRect(form.Handle, ref magWindowRect);
            hwndMag = NativeMethods.CreateWindow((int)ExtendedWindowStyles.WS_EX_CLIENTEDGE, NativeMethods.WC_MAGNIFIER,
                "MagnifierWindow", (int)WindowStyles.WS_CHILD /*| (int)MagnifierStyle.MS_SHOWMAGNIFIEDCURSOR*/ |
                (int)WindowStyles.WS_VISIBLE,
                magWindowRect.left + 300, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, form.Handle, IntPtr.Zero, hInst, IntPtr.Zero);


            var ret = NativeMethods.MagSetImageScalingCallback(hwndMag, (x1, scrdata, srcheader, x4, x5, x6, x7, x8) =>
            {
               
                byte[] managedArray = new byte[srcheader.size];
                Marshal.Copy(scrdata, managedArray, 0, (int)srcheader.size);
                //File.WriteAllBytes("byte1.dat", managedArray);
                
                var bmp = new Bitmap((int)srcheader.width, (int)srcheader.height,(int)srcheader.stride, System.Drawing.Imaging.PixelFormat.Format32bppArgb, scrdata);
                lock (lock1)
                {
                    if (LastBmp != null)
                    {
                        LastBmp.Dispose();
                    }
                    LastBmp = bmp;
                }

                //bmp.Save("temp1.jpg");
                /*using (var ms = new MemoryStream(managedArray))
                {
                    bmp = new Bitmap(ms);
                }*/
                /*
                BITMAPINFOHEADER bmif;
                // Setup the bitmap info header
                bmif.biSize = sizeof(BITMAPINFOHEADER);
                bmif.biHeight = srcheader.height;
                bmif.biWidth = srcheader.width;
                bmif.biSizeImage = srcheader.cbSize;
                bmif.biPlanes = 1;
                bmif.biBitCount = (WORD)(bmif.biSizeImage / bmif.biHeight / bmif.biWidth * 8);
                bmif.biCompression = BI_RGB;*/

                return true;
            });
            IntPtr[] array = new IntPtr[2];
            array[0] = this.form.Handle;
            array[1] = addf.Handle;
            ret = NativeMethods.MagSetWindowFilterList(hwndMag, NativeMethods.MW_FILTERMODE.MW_FILTERMODE_EXCLUDE, array.Length, array);
            if (!ret)
            {
                throw new Exception();
            }
            if (hwndMag == IntPtr.Zero)
            {
                return;
            }

            // Set the magnification factor.
            Transformation matrix = new Transformation(magnification);
            NativeMethods.MagSetWindowTransform(hwndMag, ref matrix);
        }

        protected void RemoveMagnifier()
        {
            if (initialized)
                NativeMethods.MagUninitialize();
        }

        protected virtual void Dispose(bool disposing)
        {
            timer.Enabled = false;
            if (disposing)
                timer.Dispose();
            timer = null;
            form.Resize -= form_Resize;
            RemoveMagnifier();
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
