using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Hoi4EnginePatcher
{
    static class Program
    {
        static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hoi4EnginePatcher");

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // 인자가 있으면 Steam 실행 옵션 "<이 exe>" %command% 로 불린 것 = 이어가기 모드
            return args.Length > 0 ? RunChain(args) : RunNormal();
        }

        // ── 평소: 언어 → 안내 → 메인 창 ──
        static int RunNormal()
        {
            try
            {
                string lang = LangForm.Ask();
                if (lang == null) return 0;
                SaveLang(lang);
                var t = Texts.All[lang];
                using (var info = new InfoForm(t))
                    if (info.ShowDialog() != DialogResult.OK) return 0;
                using (var f = new MainForm(t, false, null)) f.ShowDialog();
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("패치기를 시작할 수 없습니다. / The patcher could not start.\r\n\r\n" + ex.Message, "Hoi4 Engine Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        // ── 이어가기: 이미 패치돼 있으면 창 없이 바로 런처, 아니면 자동 패치 뒤 런처. 런처는 무슨 일이 있어도 띄운다 ──
        static int RunChain(string[] args)
        {
            ChainLog("start: " + string.Join(" | ", args));
            try
            {
                string game = GameExeFromChain(args);
                bool? patched = game == null ? (bool?)null : Engine.IsPatchedFast(game);
                ChainLog("game: " + (game ?? "(not found)") + "  patched: " + (patched.HasValue ? patched.Value.ToString() : "?"));
                if (patched == false)
                {
                    string lang = SavedLang() ?? LangForm.Ask();
                    if (lang != null)
                    {
                        SaveLang(lang);
                        using (var f = new MainForm(Texts.All[lang], true, game))
                        {
                            f.FormClosing += (s, e) => ChainLog("window closing: " + e.CloseReason);
                            f.ShowDialog();
                            ChainLog("patch window closed" + (f.Failed ? " (error shown)" : ""));
                        }
                    }
                    else ChainLog("language window closed — skip patch");
                }
            }
            catch (Exception ex) { ChainLog("error: " + ex.Message); }
            finally
            {
                StartChain(args);
                WaitGameSession();
            }
            return 0;
        }

        static string GameExeFromChain(string[] args)
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(args[0]));
                string exe = Path.Combine(dir, "hoi4.exe");
                if (File.Exists(exe)) return exe;
            }
            catch { }
            return GameExeCandidates(null).FirstOrDefault();
        }

        static void StartChain(string[] args)
        {
            try
            {
                string target = Path.GetFullPath(args[0]);
                if (string.Equals(target, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    ChainLog("chain points at the patcher itself — not started");
                    return;
                }
                var psi = new ProcessStartInfo(target, string.Join(" ", args.Skip(1).Select(Quote)))
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(target),
                };
                // Paradox Launcher 는 Electron 앱이라 이 변수가 걸리면 순수 Node 로 돌아 안 뜬다
                psi.EnvironmentVariables.Remove("ELECTRON_RUN_AS_NODE");
                var p = Process.Start(psi);
                ChainLog("launched " + Path.GetFileName(target) + (p != null ? " pid " + p.Id : ""));
            }
            catch (Exception ex) { ChainLog("launch failed: " + ex.Message); }
        }

        /// <summary>dowser → bootstrapper → 런처 → hoi4 사이에 빈 구간이 있으므로, 20초 내리 아무것도 안 보일 때 끝으로 본다.
        /// 먼저 끝나면 Steam 이 게임이 끝난 줄 알고 남은 자식을 정리해 버린다.</summary>
        static void WaitGameSession()
        {
            string[] names = { "dowser", "bootstrapper-v2", "Paradox Launcher", "hoi4" };
            var last = DateTime.Now;
            while ((DateTime.Now - last).TotalSeconds < 20)
            {
                if (names.Any(n => Process.GetProcessesByName(n).Length > 0)) last = DateTime.Now;
                Thread.Sleep(1000);
            }
            ChainLog("session ended");
        }

        static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0) return a;
            var sb = new StringBuilder("\"");
            int bs = 0;
            foreach (char c in a)
            {
                if (c == '\\') { bs++; continue; }
                if (c == '"') { sb.Append('\\', bs * 2 + 1).Append('"'); bs = 0; continue; }
                sb.Append('\\', bs).Append(c); bs = 0;
            }
            sb.Append('\\', bs * 2).Append('"');
            return sb.ToString();
        }

        // ── 언어 기억, 기록 ──
        static string SavedLang()
        {
            try
            {
                string v = File.ReadAllText(Path.Combine(DataDir, "lang.txt")).Trim();
                return Texts.All.ContainsKey(v) ? v : null;
            }
            catch { return null; }
        }

        static void SaveLang(string lang)
        {
            try { Directory.CreateDirectory(DataDir); File.WriteAllText(Path.Combine(DataDir, "lang.txt"), lang); } catch { }
        }

        static void ChainLog(string m)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                string f = Path.Combine(DataDir, "chain.log");
                if (File.Exists(f) && new FileInfo(f).Length > 512 * 1024) File.Delete(f);
                File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + m + "\r\n");
            }
            catch { }
        }

        // ── Steam 라이브러리(여러 드라이브)와 hoi4.exe 후보 ──
        public static IEnumerable<string> SteamLibraryRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            Action<string> add = p => { if (!string.IsNullOrEmpty(p) && Directory.Exists(p) && seen.Add(Path.GetFullPath(p))) list.Add(p); };
            string root = Steam.Root();
            add(root);
            if (root != null)
            {
                try
                {
                    string lf = Path.Combine(root, @"steamapps\libraryfolders.vdf");
                    if (File.Exists(lf))
                        foreach (Match m in Regex.Matches(File.ReadAllText(lf), "\"path\"\\s+\"([^\"]+)\""))
                            add(m.Groups[1].Value.Replace("\\\\", "\\"));
                }
                catch { }
            }
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
            add(@"D:\SteamLibrary");
            return list;
        }

        public static IEnumerable<string> GameExeCandidates(string preferred)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(preferred) && File.Exists(preferred) && seen.Add(preferred)) yield return preferred;
            // 흔한 설치 경로를 먼저(레지스트리 SteamPath 는 소문자로 적혀 있어 보기 나쁘다)
            foreach (var pf in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
            {
                string exe = Path.Combine(Environment.GetFolderPath(pf), @"Steam\steamapps\common\Hearts of Iron IV\hoi4.exe");
                if (File.Exists(exe) && seen.Add(exe)) yield return exe;
            }
            foreach (var r in SteamLibraryRoots())
            {
                string exe = Path.Combine(r, @"steamapps\common\Hearts of Iron IV\hoi4.exe");
                if (File.Exists(exe) && seen.Add(exe)) yield return exe;
            }
        }
    }
}
