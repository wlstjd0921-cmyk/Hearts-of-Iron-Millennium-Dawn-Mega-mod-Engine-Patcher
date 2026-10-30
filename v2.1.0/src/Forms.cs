using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Hoi4EnginePatcher
{
    static class LangForm
    {
        static readonly string[,] Langs =
        {
            { "ko", "한국어" }, { "en", "English" }, { "ja", "日本語" }, { "zh", "简体中文" }, { "fr", "Français" },
            { "de", "Deutsch" }, { "es", "Español" }, { "pl", "Polski" }, { "ru", "Русский" }, { "pt", "Português" },
        };

        /// <summary>고른 언어 코드. 창을 그냥 닫으면 null.</summary>
        public static string Ask()
        {
            string chosen = null;
            using (var f = new BufferedForm { Text = "Language / 언어 선택", Width = 490, Height = 388, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                for (int i = 0; i < Langs.GetLength(0); i++)
                {
                    var b = new Button
                    {
                        Text = Langs[i, 1], Tag = Langs[i, 0],
                        Location = new Point(28 + (i % 2) * 212, 28 + (i / 2) * 58),
                        Size = new Size(196, 48),
                    };
                    b.Click += (s, e) => { chosen = (string)((Button)s).Tag; f.Close(); };
                    f.Controls.Add(b);
                }
                Theme.Apply(f);
                f.ShowDialog();
            }
            return chosen;
        }
    }

    /// <summary>처음 안내: 패치 정보와 변경 표. 계속을 눌러야 다음으로 간다.</summary>
    sealed class InfoForm : BufferedForm
    {
        readonly BufferedPanel scroll;
        readonly Label label;
        int offset, grabY, grabOff;
        bool drag;

        public InfoForm(Dictionary<string, string> t)
        {
            Text = t["info_title"]; Width = 820; Height = 640; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;

            scroll = new BufferedPanel { Location = new Point(24, 24), Size = new Size(766, 500), AutoScroll = false, BackColor = Color.Transparent };
            label = new Label
            {
                AutoSize = true, MaximumSize = new Size(726, 0), Location = new Point(10, 8),
                Font = new Font("Consolas", 12), BackColor = Color.Transparent, Text = t["info_text"],
            };
            scroll.Paint += PaintScroll;
            scroll.MouseWheel += (s, e) => SetOffset(offset - (e.Delta / 120) * 54);
            scroll.MouseDown += (s, e) =>
            {
                int barX = scroll.ClientSize.Width - 12;
                if (e.X >= barX - 4) { drag = true; grabY = e.Y; grabOff = offset; }
            };
            scroll.MouseMove += (s, e) =>
            {
                if (!drag) return;
                int content = label.Height + 16, view = scroll.ClientSize.Height;
                if (content <= view) return;
                int trackH = view - 8;
                int thumbH = Math.Max(36, (int)((double)trackH * view / content));
                int span = trackH - thumbH;
                if (span <= 0) return;
                SetOffset(grabOff + (int)((double)(e.Y - grabY) * (content - view) / span));
            };
            scroll.MouseUp += (s, e) => drag = false;
            scroll.MouseEnter += (s, e) => scroll.Select();
            scroll.Controls.Add(label);
            Controls.Add(scroll);

            var ok = new Button { Text = t["continue"], Location = new Point(270, 544), Size = new Size(130, 44), DialogResult = DialogResult.OK };
            var cancel = new Button { Text = t["cancel"], Location = new Point(420, 544), Size = new Size(130, 44), DialogResult = DialogResult.Cancel };
            AcceptButton = ok; CancelButton = cancel;
            Controls.Add(ok); Controls.Add(cancel);
            Theme.Apply(this);
            Shown += (s, e) => scroll.Select();
        }

        void SetOffset(int v)
        {
            int content = label.Height + 16, view = scroll.ClientSize.Height;
            int max = Math.Max(0, content - view);
            v = Math.Max(0, Math.Min(max, v));
            if (v == offset) return;
            offset = v;
            scroll.SuspendLayout();
            label.Top = 8 - v;
            scroll.ResumeLayout(false);
            scroll.Invalidate(true);
            scroll.Update();
        }

        void PaintScroll(object sender, PaintEventArgs e)
        {
            // 폼이 그린 그라데이션 + 로고가 이 패널을 통해 글 뒤로 비친다
            var bg = Theme.Background(ClientSize.Width, ClientSize.Height);
            if (bg == null) return;
            Point off = scroll.PointToScreen(Point.Empty), fo = PointToScreen(Point.Empty);
            e.Graphics.DrawImageUnscaled(bg, fo.X - off.X, fo.Y - off.Y);

            int content = label.Height + 16, view = scroll.ClientSize.Height;
            if (content <= view) return;
            int barW = 8, barX = scroll.ClientSize.Width - barW - 4, trackH = view - 8;
            int thumbH = Math.Max(36, (int)((double)trackH * view / content));
            int maxOff = content - view;
            double ratio = maxOff > 0 ? (double)offset / maxOff : 0;
            int thumbY = 4 + (int)((trackH - thumbH) * ratio);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var track = Theme.RoundRect(barX, 4, barW, trackH, 4))
            using (var tb = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) e.Graphics.FillPath(tb, track);
            using (var thumb = Theme.RoundRect(barX, thumbY, barW, thumbH, 4))
            using (var hb = new SolidBrush(Color.FromArgb(210, 18, 129, 181))) e.Graphics.FillPath(hb, thumb);
        }
    }

    /// <summary>메인 창. auto=true(이어가기 모드)면 뜨자마자 패치하고, 성공하면 2.5초 뒤 저절로 닫힌다.</summary>
    sealed class MainForm : BufferedForm
    {
        readonly Dictionary<string, string> t;
        readonly bool auto;
        readonly ComboBox pathBox;
        readonly Label status;
        readonly TextBox output;
        readonly CheckBox autoRun;
        bool autoRunBusy;
        Timer closeTimer;

        public MainForm(Dictionary<string, string> texts, bool autoMode, string preferredExe)
        {
            t = texts; auto = autoMode;
            Text = t["title"]; Width = 880; Height = 700;

            Controls.Add(new Label { Text = t["title"], Font = new Font(Theme.UiFont, 19, FontStyle.Bold), ForeColor = Theme.Title, Location = new Point(24, 20), Size = new Size(560, 46) });
            Controls.Add(new Label { Text = t["version"], Location = new Point(24, 64), Size = new Size(820, 30), ForeColor = Theme.Muted });
            Controls.Add(new Label { Text = t["file_path"], Location = new Point(24, 104), Size = new Size(140, 34) });

            pathBox = new ComboBox { Location = new Point(158, 102), Size = new Size(610, 34), DropDownStyle = ComboBoxStyle.Simple };
            foreach (var p in Program.GameExeCandidates(preferredExe)) pathBox.Items.Add(p);
            if (pathBox.Items.Count > 0) pathBox.SelectedIndex = 0;
            Controls.Add(pathBox);

            var browse = new Button { Text = "...", Location = new Point(788, 102), Size = new Size(60, 30) };
            browse.Click += (s, e) =>
            {
                using (var ofd = new OpenFileDialog { Filter = "hoi4.exe|hoi4.exe" })
                {
                    if (ofd.ShowDialog(this) != DialogResult.OK) return;
                    if (pathBox.Items.IndexOf(ofd.FileName) == -1) pathBox.Items.Add(ofd.FileName);
                    pathBox.SelectedItem = ofd.FileName;
                }
            };
            Controls.Add(browse);

            status = new Label { Text = t["ready"], Location = new Point(24, 148), Size = new Size(820, 32), ForeColor = Theme.Ok };
            Controls.Add(status);

            output = new TextBox { Location = new Point(24, 186), Size = new Size(824, 330), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 12) };
            Controls.Add(output);

            var patch = new Button { Text = t["patch"], Location = new Point(650, 532), Size = new Size(190, 46) };
            patch.Click += (s, e) => DoPatch();
            Controls.Add(patch);

            autoRun = new CheckBox { Text = t["autorun"], Location = new Point(24, 540), Size = new Size(600, 32), ForeColor = Theme.Text, BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat };
            try { autoRun.Checked = Steam.IsAutoRunRegistered(); } catch { }
            autoRun.CheckedChanged += (s, e) => ToggleAutoRun();
            Controls.Add(autoRun);

            Theme.Apply(this);
            Shown += (s, e) =>
            {
                ActiveControl = patch;                       // 경로 칸이 처음부터 통째로 선택돼 보이지 않게
                if (SelectedPath() == null) return;
                StatusCheck();
                if (auto) BeginInvoke((Action)DoPatch);
            };
        }

        public bool Failed { get; private set; }

        string SelectedPath()
        {
            string p = (pathBox.SelectedItem as string) ?? pathBox.Text;
            return string.IsNullOrWhiteSpace(p) ? null : p.Trim();
        }

        void Log(string m) { output.AppendText(m + "\r\n"); Refresh(); }

        void SetStatus(string text, Color color) { status.Text = text; status.ForeColor = color; Failed = color == Theme.Err; }

        static bool GameRunning() { return Process.GetProcessesByName("hoi4").Length > 0; }

        void StatusCheck()
        {
            status.Text = t["checking"];
            Refresh();
            try
            {
                string path = SelectedPath();
                var fi = new FileInfo(path);
                Log("[i] File: " + path);
                Log("[i] Size: " + Math.Round(fi.Length / 1048576.0, 2) + " MB");
                bool patched;
                Log("[i] Sections: " + Engine.SectionNames(path, out patched));
                Log("[i] " + (patched ? t["check_patched"] : t["check_original"]));
                bool running = GameRunning();
                Log("[i] " + (running ? t["game_running_info"] : t["game_ok"]));
                if (running) SetStatus(t["game_running_info"], Theme.Warn);
                else SetStatus(patched ? t["check_patched"] : t["check_original"], Theme.Ok);
            }
            catch (Exception ex)
            {
                Log("[!] Error: " + ex.Message);
                SetStatus(t["error"], Theme.Err);
            }
        }

        void CloseSoon(int ms)
        {
            closeTimer = new Timer { Interval = ms };
            closeTimer.Tick += (s, e) => { closeTimer.Stop(); if (!Failed) Close(); };
            closeTimer.Start();
        }

        void DoPatch()
        {
            SetStatus(t["patching"], Theme.Warn);
            Refresh();
            try
            {
                string path = SelectedPath();
                if (path == null || !File.Exists(path)) throw new FileNotFoundException(t["file_not_found"]);
                if (GameRunning()) throw new InvalidOperationException(t["game_running"]);

                Log("[+] 전체 엔진 패치를 적용합니다...");
                int n = Engine.Apply(path, Log);
                Log("");
                Log("[+] 완료 — " + n + " bytes");
                SetStatus(t["patch_complete"], Theme.Ok);
                Log("");
                Log("=== 되돌리려면 ===");
                Log("Steam > HOI4 우클릭 > 속성 > 설치된 파일");
                Log("  > 게임 파일 무결성 확인");
                Log("");
                Log(auto ? "[+] 2.5초 뒤 창이 닫히고 게임 런처가 열립니다." : "[+] 2초 뒤 창이 닫힙니다.");
                CloseSoon(auto ? 2500 : 2000);
            }
            catch (PatchException ex) when (ex.Message == "ALREADY_PATCHED")
            {
                Log("[i] 이미 패치되어 있습니다.");
                SetStatus(t["already_patched"], Theme.Ok);
                if (auto) CloseSoon(2500);
            }
            catch (PatchException ex) when (ex.Message.StartsWith("VERIFY_FAIL") || ex.Message.StartsWith("OFFSET_NOT_FOUND"))
            {
                Log("[!] 이 exe 에는 맞지 않는 패치입니다. 아무것도 쓰지 않았습니다.");
                Log("    " + ex.Message);
                SetStatus(t["version_mismatch"], Theme.Err);
            }
            catch (Exception ex)
            {
                Log("[!] Error: " + ex.Message);
                SetStatus(t["error"], Theme.Err);
            }
        }

        void ToggleAutoRun()
        {
            if (autoRunBusy) return;
            bool want = autoRun.Checked;
            autoRunBusy = true;
            try
            {
                if (Steam.IsRunning())
                {
                    var a = MessageBox.Show(this, t["autorun_ask"], t["title"], MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                    if (a != DialogResult.OK) { autoRun.Checked = !want; return; }
                }
                autoRun.Enabled = false;
                Refresh();
                Steam.SetAutoRun(want, Log);
                Log(want ? t["autorun_on"] : t["autorun_off"]);
                SetStatus(want ? t["autorun_on"] : t["autorun_off"], Theme.Ok);
            }
            catch (Exception ex)
            {
                Log(t["autorun_fail"] + " (" + ex.Message + ")");
                SetStatus(t["autorun_fail"], Theme.Err);
                autoRun.Checked = !want;
            }
            finally
            {
                autoRun.Enabled = true;
                autoRunBusy = false;
            }
        }
    }
}
