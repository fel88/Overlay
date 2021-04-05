using Karna.Magnification;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Overlay
{
    public partial class overlay : Form
    {
        public overlay()
        {
            InitializeComponent();
            Load += Form1_Load;
            Form = this;

            DoubleBuffered = true;
            manager mm = new manager();
            mm.Show();
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            //MaximizeEverything();
            //BackColor = Color.Transparent;
            //TransparencyKey = Color.Wheat;
            SetFormTransparent(this.Handle);
            grayer = new Bitmap(2000, 1200);
            using (var gr = Graphics.FromImage(grayer))
            {
                gr.Clear(Color.FromArgb(128, Color.Gray));
            }
        }

        Bitmap grayer;

        public void InitNnet(string path)
        {
            Init(path);

            InputDatas[_nodes[0].Name] = new InputInfo() { Data = lastCaptured };

            _nodes[1].Tags.Add("loc");
            _nodes[2].Tags.Add("conf");
            InputDatas[_nodes[0].Name].Preprocessors.Add(new MeanStdPreprocessor() { Mean = new double[] { 104, 117, 123 }, Std = new double[] { 1, 1, 1 } });
            InputDatas[_nodes[0].Name].Preprocessors.Add(new NCHWPreprocessor());

            //SetTheLayeredWindowAttribute();

            /*   BackgroundWorker tmpBw = new BackgroundWorker();
               tmpBw.DoWork += new DoWorkEventHandler(bw_DoWork);

               this.bw = tmpBw;

               this.bw.RunWorkerAsync();*/

            if (th != null) return;
            th = new Thread(() =>
            {
                while (true)
                {
                    /*  Rectangle bounds = Screen.GetBounds(System.Drawing.Point.Empty);
                      using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                      {
                          using (Graphics g = Graphics.FromImage(bitmap))
                          {

                              g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);


                          }
                          if (lastCaptured != null)
                          {
                              lastCaptured.Dispose();
                          }
                          lastCaptured = BitmapConverter.ToMat(bitmap);
                          //Clipboard.SetImage(bitmap);


                      }*/
                    if (lastCaptured != null)
                    {
                        lastCaptured.Dispose();
                    }
                    lock (Magnifier.lock1)
                    {
                        if (Magnifier.LastBmp == null) continue;
                        lastCaptured = BitmapConverter.ToMat(Magnifier.LastBmp);
                    }

                    //float koef = 584f / lastCaptured.Width;
                    //var temp = lastCaptured.Resize(new OpenCvSharp.Size(1024, koef * lastCaptured.Height));
                    //lastCaptured.Dispose();
                    //lastCaptured = temp;
                    InputDatas[_nodes[0].Name].Data = lastCaptured;

                    run();
                }
            });
            th.IsBackground = true;
            th.Start();
        }
        float[] inputData;

        private void run()
        {
            Stopwatch sw = Stopwatch.StartNew();


            var inputMeta = session1.InputMetadata;
            var container = new List<NamedOnnxValue>();

            Mat mat2 = null;
            foreach (var name in inputMeta.Keys)
            {
                var data = InputDatas[name];
                if (data.Data is Mat matOrig)
                {

                    //var mat = matOrig.Clone(new Rect(shiftX, shiftY, 576, 288));
                    var mat = matOrig.Clone();
                    //mat = mat.Resize(new OpenCvSharp.Size(576, 288));
                    if (inputMeta[name].Dimensions[2] == -1)
                    {
                        inputMeta[name].Dimensions[2] = mat.Height;
                        inputMeta[name].Dimensions[3] = mat.Width;
                    }

                    mat2 = mat.Clone();
                    mat.ConvertTo(mat, MatType.CV_32F);
                    object param = mat;
                    foreach (var pitem in data.Preprocessors)
                    {
                        param = pitem.Process(param);
                    }

                    inputData = param as float[];
                    var tensor = new DenseTensor<float>(param as float[], inputMeta[name].Dimensions);

                    container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
                }
            }
            OutputDatas.Clear();
            using (var results = session1.Run(container))
            {

                // Get the results
                foreach (var result in results)
                {
                    var data = result.AsTensor<float>();
                    //var dims = data.Dimensions;
                    var rets = data.ToArray();
                    OutputDatas.Add(result.Name, rets);
                }
            }

            //if (checkBox1.Checked)
            {
                Stopwatch sw2 = Stopwatch.StartNew();
                var ret = boxesDecode(mat2);
                lock (lock1)
                {
                    last = ret;
                }
                sw2.Stop();

                if (ret != null)
                {

                    //var mm = drawBoxes(mat2, ret.Item1, ret.Item2, visTresh, ret.Item3);
                    /*pictureBox1.Image = BitmapConverter.ToBitmap(mm);
                    mat2 = mm;
                    pictureBox1.Invoke((Action)(() =>
                    {
                        if (pictureBox1.Image != null)
                        {
                            pictureBox1.Image.Dispose();
                        }
                        pictureBox1.Image = BitmapConverter.ToBitmap(mm);
                    }));*/
                }
            }

            sw.Stop();
            lastms = sw.ElapsedMilliseconds;

        }
        long lastms;
        object lock1 = new object();
        Tuple<Rect[], float[], int[]> last;
        public Tuple<Rect[], float[], int[]> boxesDecode(Mat mat1)
        {
            var f1 = _nodes.FirstOrDefault(z => z.Tags.Contains("conf"));
            var f2 = _nodes.FirstOrDefault(z => z.Tags.Contains("loc"));
            if (f1 == null || f2 == null)
            {
                return null;
            }
            var rets1 = OutputDatas[f2.Name] as float[];
            var rets3 = OutputDatas[f1.Name] as float[];
            var dims = _nodes.First(z => z.IsInput).Dims;
            var sz = new System.Drawing.Size(dims[3], dims[2]);
            if (dims[2] == -1)
            {
                sz.Height = mat1.Height;
                sz.Width = mat1.Width;
            }
            string key = $"{sz.Width}x{sz.Height}";
            if (!Decoders.allPriorBoxes.ContainsKey(key))
            {
                var pd = Decoders.PriorBoxes2(sz.Width, sz.Height); ;
                Decoders.allPriorBoxes.Add(key, pd);
            }
            var prior_data = Decoders.allPriorBoxes[key];
            return Decoders.boxesDecode(mat1.Size(), rets3, rets1, sz, prior_data, visTresh);


        }

        float visTresh = 0.5f;


        Mat lastCaptured;
        List<NodeInfo> _nodes = new List<NodeInfo>();
        InferenceSession session1;
      
        internal void Init(string lastPath)
        {


            session1 = new InferenceSession(lastPath, new SessionOptions() { IntraOpNumThreads = 1, InterOpNumThreads = 1 });

            foreach (var name in session1.InputMetadata.Keys)
            {
                var dims = session1.InputMetadata[name].Dimensions;
                var s1 = string.Join("x", dims);
                _nodes.Add(new NodeInfo() { Name = name, Dims = dims, IsInput = true });

            }
            foreach (var item in session1.OutputMetadata.Keys)
            {
                var dims = session1.OutputMetadata[item].Dimensions;
                _nodes.Add(new NodeInfo() { Name = item, Dims = dims });

            }

        }
        Thread th;
        public Dictionary<string, InputInfo> InputDatas = new Dictionary<string, InputInfo>();
        Dictionary<string, object> OutputDatas = new Dictionary<string, object>();
        public void SetFormTransparent(IntPtr Handle)
        {
            oldWindowLong = GetWindowLong(Handle, (int)GetWindowLongConst.GWL_EXSTYLE);
            SetWindowLong(Handle, (int)GetWindowLongConst.GWL_EXSTYLE, Convert.ToInt32(oldWindowLong | (uint)WindowStyles.WS_EX_LAYERED | (uint)WindowStyles.WS_EX_TRANSPARENT));
        }
        int oldWindowLong;

        [Flags]
        enum WindowStyles : uint
        {
            WS_OVERLAPPED = 0x00000000,
            WS_POPUP = 0x80000000,
            WS_CHILD = 0x40000000,
            WS_MINIMIZE = 0x20000000,
            WS_VISIBLE = 0x10000000,
            WS_DISABLED = 0x08000000,
            WS_CLIPSIBLINGS = 0x04000000,
            WS_CLIPCHILDREN = 0x02000000,
            WS_MAXIMIZE = 0x01000000,
            WS_BORDER = 0x00800000,
            WS_DLGFRAME = 0x00400000,
            WS_VSCROLL = 0x00200000,
            WS_HSCROLL = 0x00100000,
            WS_SYSMENU = 0x00080000,
            WS_THICKFRAME = 0x00040000,
            WS_GROUP = 0x00020000,
            WS_TABSTOP = 0x00010000,

            WS_MINIMIZEBOX = 0x00020000,
            WS_MAXIMIZEBOX = 0x00010000,

            WS_CAPTION = WS_BORDER | WS_DLGFRAME,
            WS_TILED = WS_OVERLAPPED,
            WS_ICONIC = WS_MINIMIZE,
            WS_SIZEBOX = WS_THICKFRAME,
            WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW,

            WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
            WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
            WS_CHILDWINDOW = WS_CHILD,

            //Extended Window Styles

            WS_EX_DLGMODALFRAME = 0x00000001,
            WS_EX_NOPARENTNOTIFY = 0x00000004,
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_ACCEPTFILES = 0x00000010,
            WS_EX_TRANSPARENT = 0x00000020,

            //#if(WINVER >= 0x0400)

            WS_EX_MDICHILD = 0x00000040,
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_WINDOWEDGE = 0x00000100,
            WS_EX_CLIENTEDGE = 0x00000200,
            WS_EX_CONTEXTHELP = 0x00000400,

            WS_EX_RIGHT = 0x00001000,
            WS_EX_LEFT = 0x00000000,
            WS_EX_RTLREADING = 0x00002000,
            WS_EX_LTRREADING = 0x00000000,
            WS_EX_LEFTSCROLLBAR = 0x00004000,
            WS_EX_RIGHTSCROLLBAR = 0x00000000,

            WS_EX_CONTROLPARENT = 0x00010000,
            WS_EX_STATICEDGE = 0x00020000,
            WS_EX_APPWINDOW = 0x00040000,

            WS_EX_OVERLAPPEDWINDOW = (WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE),
            WS_EX_PALETTEWINDOW = (WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST),
            //#endif /* WINVER >= 0x0400 */

            //#if(WIN32WINNT >= 0x0500)

            WS_EX_LAYERED = 0x00080000,
            //#endif /* WIN32WINNT >= 0x0500 */

            //#if(WINVER >= 0x0500)

            WS_EX_NOINHERITLAYOUT = 0x00100000, // Disable inheritence of mirroring by children
            WS_EX_LAYOUTRTL = 0x00400000, // Right to left mirroring
            //#endif /* WINVER >= 0x0500 */

            //#if(WIN32WINNT >= 0x0500)

            WS_EX_COMPOSITED = 0x02000000,
            WS_EX_NOACTIVATE = 0x08000000
            //#endif /* WIN32WINNT >= 0x0500 */

        }

        public enum GetWindowLongConst
        {
            GWL_WNDPROC = (-4),
            GWL_HINSTANCE = (-6),
            GWL_HWNDPARENT = (-8),
            GWL_STYLE = (-16),
            GWL_EXSTYLE = (-20),
            GWL_USERDATA = (-21),
            GWL_ID = (-12)
        }

        public enum LWA
        {
            ColorKey = 0x1,
            Alpha = 0x2,
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const UInt32 SWP_NOSIZE = 0x0001;
        const UInt32 SWP_NOMOVE = 0x0002;
        const UInt32 SWP_SHOWWINDOW = 0x0040;
        private void Form1_Load(object sender, EventArgs e)
        {
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        }

        IntPtr memDc, hBmp, hOldBmp;
        int shiftX = 200;
        int shiftY = 200;
        int xx;
        public static overlay Form;
        public static bool FillRects;
        public static bool UseHistory;

        public class RectLog
        {
            public Rect Rect;
            public OpenCvSharp.Point Shift;
            public int Timestamp = 100;
        }

        public List<RectLog> Rects = new List<RectLog>();

        private void timer1_Tick(object sender, EventArgs e)
        {
            var c = Cursor.Position;
            //Invalidate();


            APIHelp.BLENDFUNCTION blend;

            //Only works with a 32bpp bitmap
            blend.BlendOp = APIHelp.AC_SRC_OVER;
            //Always 0
            blend.BlendFlags = 0;
            //Set to 255 for per-pixel alpha values
            blend.SourceConstantAlpha = 255;
            //Only works when the bitmap contains an alpha channel
            blend.AlphaFormat = APIHelp.AC_SRC_ALPHA;

            IntPtr screenDc;

            screenDc = APIHelp.GetDC(IntPtr.Zero);
            shiftX = Magnifier.lastRect.left;
            shiftY = Magnifier.lastRect.top;

            using (Bitmap bmp = new Bitmap(this.Width, this.Height))
            {
                using (var gr = Graphics.FromImage(bmp))
                {
                    gr.Clear(Color.Transparent);
                    DoubleBuffered = true;
                    gr.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.Red)), xx, 100, 200, 300);
                    gr.DrawRectangle(new Pen(Color.Yellow, 3), new Rectangle(shiftX, shiftY, Magnifier.lastRect.right - Magnifier.lastRect.left, Magnifier.lastRect.bottom - Magnifier.lastRect.top));


                    lock (Magnifier.lock1)
                    {

                        //gr.DrawImage(Magnifier.LastBmp, 0, 0);
                        //gr.DrawImage(grayer, 0, 0);
                    }
                    Rect[] detections = new Rect[0];
                    float[] oscores = new float[0];
                    int[] classes = new int[0];
                    lock (lock1)
                    {
                        if (last != null)
                        {
                            /*using (var bmp1 = new Bitmap(1920, 1080))
                            {
                                bmp1.MakeTransparent();
                                using (var ggr = Graphics.FromImage(bmp1))
                                {
                                    ggr.Clear(Color.FromArgb(64, Color.Red));
                                    e.Graphics.DrawImage(bmp1, 0, 0);
                                }
                            }*/

                            detections = last.Item1;
                            oscores = last.Item2;
                            classes = last.Item3;
                        }
                    }
                    int hi = -1;
                    float hconf = 0;
                    for (int i = 0; i < detections.Length; i++)
                    {
                        if (oscores[i] < visTresh) continue;
                        int cls = 0;
                        if (classes != null)
                        {
                            cls = classes[i];
                        }
                        if (cls == 0)
                        {
                            if (oscores[i] > hconf)
                            {
                                hconf = oscores[i];
                                hi = i;
                            }
                        }
                    }

                    for (int i = 0; i < detections.Length; i++)
                    {
                        if (oscores[i] < visTresh) continue;
                        Rects.Add(new RectLog() { Rect = detections[i], Shift = new OpenCvSharp.Point(shiftX, shiftY) });

                        var text = Math.Round(oscores[i], 4).ToString();
                        int cls = -1;
                        if (classes != null)
                        {
                            cls = classes[i];
                            text += $"(cls: {cls})";
                        }
                        var cx = detections[i].X;
                        var cy = detections[i].Y + 12;
                        /*   if (cls == 0 && hi == i)
                           {
                               gr.DrawRectangle(new Pen(Color.FromArgb(0, 255, 0), 3), detections[i].Left + shiftX, detections[i].Top + shiftY, detections[i].Width, detections[i].Height);
                               if (FillRects)
                                   gr.FillRectangle(new SolidBrush(Color.FromArgb(0, 255, 0)), detections[i].Left + shiftX, detections[i].Top + shiftY, detections[i].Width, detections[i].Height);
                           }
                           else if (cls >= 2)
                           {
                               gr.DrawRectangle(new Pen(Color.FromArgb(255, 0, 255), 2), detections[i].Left + shiftX, detections[i].Top + shiftY, detections[i].Width, detections[i].Height);
                               if (FillRects)
                                   gr.FillRectangle(new SolidBrush(Color.FromArgb(0, 255, 0)), detections[i].Left + shiftX, detections[i].Top + shiftY, detections[i].Width, detections[i].Height);
                           }
                           else
                           {
                               gr.DrawRectangle(new Pen(Color.FromArgb(255, 0, 0), 2), detections[i].Left + shiftX, detections[i].Top + shiftY, detections[i].Width, detections[i].Height);
                               if (FillRects)
                                   gr.FillRectangle(new SolidBrush(Color.FromArgb(0, 255, 0)), detections[i].Left + shiftX, detections[i].Top + shiftY, detections[i].Width, detections[i].Height);
                           }*/
                        //mat.Rectangle(new OpenCvSharp.Point(cx, cy + 5), new OpenCvSharp.Point(cx + 120, cy - 15), new Scalar(0, 0, 0), -1);
                        //mat.PutText(text, new OpenCvSharp.Point(cx, cy),
                        //          HersheyFonts.HersheyDuplex, 0.5, new Scalar(255, 255, 255));
                    }

                    foreach (var item in Rects)
                    {
                        var shift = item.Shift;
                        item.Timestamp--;
                        gr.DrawRectangle(new Pen(Color.FromArgb(255, 0, 255), 2), item.Rect.Left + shift.X, item.Rect.Top + shift.Y, item.Rect.Width, item.Rect.Height);
                        if (FillRects)
                            gr.FillRectangle(new SolidBrush(Color.FromArgb(0, 255, 0)), item.Rect.Left + shift.X, item.Rect.Top + shift.Y, item.Rect.Width, item.Rect.Height);
                    }
                    if (UseHistory)
                    {
                        Rects.RemoveAll(z => z.Timestamp == 0);
                    }
                    else
                    {
                        Rects.Clear();
                    }




                    //using (bmp = (Bitmap)Bitmap.FromFile(@"C:\.......png")) //the image must be the same size as your form
                    {
                        memDc = APIHelp.CreateCompatibleDC(screenDc);
                        hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                        hOldBmp = APIHelp.SelectObject(memDc, hBmp); //memDc is a device context that contains our image
                    }
                }
            }

            APIHelp.DeleteDC(screenDc);


            APIHelp.Size newSize;
            APIHelp.Point newLocation;
            APIHelp.Point sourceLocation;

            newLocation.x = this.Location.X;
            newLocation.y = this.Location.Y;

            sourceLocation.x = 0;
            sourceLocation.y = 0;

            newSize.cx = this.Width;
            newSize.cy = this.Height;

            APIHelp.UpdateLayeredWindow(Handle, IntPtr.Zero, ref newLocation, ref newSize, memDc, ref sourceLocation,
                   0, ref blend, APIHelp.ULW_ALPHA);
            APIHelp.DeleteDC(memDc);
            APIHelp.DeleteObject(hBmp);
            APIHelp.DeleteObject(hOldBmp);

        }
    
    }
}
