using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Hoi4EnginePatcher
{
    /// <summary>Oak 팔레트(MD 네이비) + 그라데이션 배경 + MD 로고 워터마크.</summary>
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(19, 27, 64);        // #131B40
        public static readonly Color Panel = Color.FromArgb(11, 16, 37);     // #0B1025
        public static readonly Color Btn = Color.FromArgb(30, 48, 92);       // #1E305C
        public static readonly Color Border = Color.FromArgb(18, 129, 181);  // #1281B5
        public static readonly Color Text = Color.FromArgb(246, 246, 246);
        public static readonly Color Title = Color.FromArgb(79, 163, 232);
        public static readonly Color Muted = Color.FromArgb(138, 154, 184);
        public static readonly Color Ok = Color.FromArgb(64, 190, 150);
        public static readonly Color Warn = Color.FromArgb(230, 168, 70);
        public static readonly Color Err = Color.FromArgb(224, 90, 90);
        static readonly Color GradTop = Color.FromArgb(19, 27, 64), GradMid = Color.FromArgb(11, 16, 37), GradBot = Color.FromArgb(5, 8, 16);

        public const string UiFont = "Malgun Gothic";

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hWnd, string app, string idList);

        public static void Caption(Form f)
        {
            try
            {
                int cap = 0x00401B13, txt = 0x00F6F6F6, bdr = 0x00B58112;   // COLORREF (BGR)
                DwmSetWindowAttribute(f.Handle, 35, ref cap, 4);
                DwmSetWindowAttribute(f.Handle, 36, ref txt, 4);
                DwmSetWindowAttribute(f.Handle, 34, ref bdr, 4);
            }
            catch { }
        }

        public static void DarkScrollbars(Control c) { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }

        static Image wm; static bool wmTried;
        static Image Watermark()
        {
            if (wmTried) return wm;
            wmTried = true;
            foreach (var root in Program.SteamLibraryRoots())
            {
                string p = Path.Combine(root, @"steamapps\workshop\content\394360\2777392649\thumbnail.png");
                if (!File.Exists(p)) continue;
                try { using (var tmp = Image.FromFile(p)) wm = new Bitmap(tmp); return wm; } catch { }
            }
            return null;
        }

        public static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
        {
            int d = r * 2;
            var p = new GraphicsPath();
            if (h < d) { r = h / 2; d = r * 2; }
            if (d <= 0) { p.AddRectangle(new Rectangle(x, y, w, h)); return p; }
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static Bitmap bg; static int bgW, bgH;
        public static Bitmap Background(int w, int h)
        {
            if (w <= 0 || h <= 0) return null;
            if (bg != null && bgW == w && bgH == h) return bg;
            if (bg != null) bg.Dispose();
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                var r = new Rectangle(0, 0, w, h);
                using (var br = new LinearGradientBrush(r, GradTop, GradBot, LinearGradientMode.Vertical))
                {
                    br.InterpolationColors = new ColorBlend(3) { Colors = new[] { GradTop, GradMid, GradBot }, Positions = new[] { 0f, 0.55f, 1f } };
                    g.FillRectangle(br, r);
                }
                var mark = Watermark();
                if (mark != null)
                {
                    int side = (int)(Math.Min(w, h) * 0.92);
                    int dx = (w - side) / 2, dy = (h - side) / 2;
                    var cm = new ColorMatrix { Matrix00 = 1, Matrix11 = 1, Matrix22 = 1, Matrix44 = 1, Matrix33 = 0.16f };
                    using (var ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(cm);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(mark, new Rectangle(dx, dy, side, side), 0, 0, mark.Width, mark.Height, GraphicsUnit.Pixel, ia);
                    }
                }
            }
            bg = bmp; bgW = w; bgH = h;
            return bmp;
        }

        static Icon icon;
        public static Icon AppIcon()
        {
            if (icon != null) return icon;
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Hoi4EnginePatcher.app.ico"))
                    if (s != null) icon = new Icon(s);
            }
            catch { }
            return icon;
        }

        /// <summary>폼에 테마를 입힌다. 컨트롤을 다 넣은 뒤에 부른다.</summary>
        public static void Apply(Form f)
        {
            f.BackColor = Bg;
            f.Paint += (s, e) => { var b = Background(f.ClientSize.Width, f.ClientSize.Height); if (b != null) e.Graphics.DrawImageUnscaled(b, 0, 0); };
            f.Resize += (s, e) => { bgW = 0; f.Invalidate(); };
            var ic = AppIcon();
            if (ic != null) f.Icon = ic;
            // HOI4 가 전체 화면이면 새 창이 게임 뒤에 열린다 → 맨 앞에 띄운다
            f.TopMost = true;
            f.ShowInTaskbar = true;
            f.Shown += (s, e) =>
            {
                Caption(f);
                foreach (Control c in f.Controls) if (c is TextBox || c is ComboBox) DarkScrollbars(c);
                f.Invalidate();
                f.Activate();
                f.BringToFront();
            };
            foreach (Control c in f.Controls)
            {
                if (c is Button b)
                {
                    b.BackColor = Btn; b.ForeColor = Text; b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = Border; b.FlatAppearance.BorderSize = 1;
                }
                else if (c is TextBox || c is ComboBox) { c.BackColor = Panel; c.ForeColor = Text; }
                else if (c is Label || c is CheckBox)
                {
                    c.BackColor = Color.Transparent;
                    if (c.ForeColor == SystemColors.ControlText || c.ForeColor == Color.Black) c.ForeColor = Text;
                }
                else if (c is System.Windows.Forms.Panel)
                {
                    c.BackColor = Color.Transparent;
                    foreach (Control cc in c.Controls)
                    {
                        cc.BackColor = Color.Transparent;
                        if (cc.ForeColor == SystemColors.ControlText || cc.ForeColor == Color.Black) cc.ForeColor = Text;
                    }
                    DarkScrollbars(c);
                }
            }
        }
    }

    class BufferedForm : Form
    {
        public BufferedForm()
        {
            DoubleBuffered = true;
            Font = new Font(Theme.UiFont, 12);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
        }
    }

    class BufferedPanel : Panel
    {
        public BufferedPanel() { DoubleBuffered = true; }
    }
}
