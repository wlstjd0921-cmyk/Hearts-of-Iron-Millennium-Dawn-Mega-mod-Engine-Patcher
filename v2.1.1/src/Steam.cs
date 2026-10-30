using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Hoi4EnginePatcher
{
    /// <summary>
    /// Steam 실행 옵션: "&lt;이 exe&gt;" %command% 를 HOI4(394360) 실행 옵션에 넣으면 Steam 이 게임 대신
    /// 이 패치기를 띄우고 원래 명령을 인자로 넘긴다. 패치기가 일을 마치고 그 명령을 이어서 실행한다.
    /// </summary>
    static class Steam
    {
        const string AppId = "394360";
        static readonly RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        public static string Root()
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    string sub = hive == Registry.CurrentUser ? @"Software\Valve\Steam" : @"SOFTWARE\WOW6432Node\Valve\Steam";
                    using (var k = hive.OpenSubKey(sub))
                    {
                        if (k == null) continue;
                        foreach (var name in new[] { "SteamPath", "InstallPath" })
                        {
                            var v = k.GetValue(name) as string;
                            if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) return v.Replace('/', '\\');
                        }
                    }
                }
                catch { }
            }
            foreach (var p in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                var d = Path.Combine(p, "Steam");
                if (Directory.Exists(d)) return d;
            }
            return null;
        }

        /// <summary>로그인한 계정을 먼저 쓴다. 없으면 이 게임 기록이 있는 것 중 가장 최근 것.</summary>
        public static string ConfigFile()
        {
            string root = Root();
            if (root == null) return null;
            string ud = Path.Combine(root, "userdata");
            if (!Directory.Exists(ud)) return null;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess"))
                {
                    object au = k == null ? null : k.GetValue("ActiveUser");
                    if (au != null && Convert.ToInt64(au) != 0)
                    {
                        string f = Path.Combine(ud, Convert.ToString(au), "config", "localconfig.vdf");
                        if (File.Exists(f)) return f;
                    }
                }
            }
            catch { }
            string best = null;
            foreach (var d in Directory.GetDirectories(ud))
            {
                string f = Path.Combine(d, "config", "localconfig.vdf");
                if (!File.Exists(f)) continue;
                bool has;
                try { has = File.ReadAllText(f).Contains("\"" + AppId + "\""); } catch { continue; }
                if (!has) continue;
                if (best == null || File.GetLastWriteTime(f) > File.GetLastWriteTime(best)) best = f;
            }
            return best;
        }

        static int[] AppBlockRange(IList<string> lines)
        {
            int start = -1;
            for (int i = 0; i < lines.Count; i++)
                if (Regex.IsMatch(lines[i], "^\\s*\"" + AppId + "\"\\s*$", I)) { start = i; break; }
            if (start < 0 || start + 1 >= lines.Count || !Regex.IsMatch(lines[start + 1], "^\\s*\\{\\s*$", I)) return null;
            int depth = 0;
            for (int i = start + 1; i < lines.Count; i++)
            {
                if (Regex.IsMatch(lines[i], "^\\s*\\{\\s*$", I)) depth++;
                else if (Regex.IsMatch(lines[i], "^\\s*\\}\\s*$", I)) { depth--; if (depth == 0) return new[] { start, i }; }
            }
            return null;
        }

        public static bool IsAutoRunRegistered()
        {
            string f = ConfigFile();
            if (f == null) return false;
            var lines = File.ReadAllLines(f);
            var r = AppBlockRange(lines);
            if (r == null) return false;
            for (int i = r[0]; i <= r[1]; i++)
            {
                var m = Regex.Match(lines[i], "^\\s*\"LaunchOptions\"\\s+\"(.*)\"\\s*$", I);
                if (m.Success) return Regex.IsMatch(m.Groups[1].Value, "Hoi4 Engine Patcher", I);
            }
            return false;
        }

        public static bool IsRunning() { return Process.GetProcessesByName("steam").Length > 0; }

        static void Wait(int ms)
        {
            var until = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
        }

        public static void SetAutoRun(bool on, Action<string> log)
        {
            string f = ConfigFile();
            if (f == null) throw new InvalidOperationException("NO_STEAM_CONFIG");
            string root = Root();
            bool wasRunning = IsRunning();
            if (wasRunning)
            {
                log("[*] Steam 종료 중...");
                Process.Start(new ProcessStartInfo(Path.Combine(root, "steam.exe"), "-shutdown") { UseShellExecute = false });
                var dl = DateTime.Now.AddSeconds(60);
                while (DateTime.Now < dl && IsRunning()) Wait(800);
                if (IsRunning()) throw new InvalidOperationException("STEAM_STILL_RUNNING");
            }

            var lines = File.ReadAllLines(f).ToList();
            var r = AppBlockRange(lines);
            if (r == null) throw new InvalidOperationException("NO_APP_BLOCK");

            string value = "";
            if (on) value = "\"" + Application.ExecutablePath + "\" %command%";
            string esc = value.Replace("\\", "\\\\").Replace("\"", "\\\"");      // KeyValues 텍스트는 \\ 와 \" 로 이스케이프
            string indent = r[0] + 2 < lines.Count ? Regex.Match(lines[r[0] + 2], "^(\\s*)").Groups[1].Value : "";
            if (indent.Length == 0) indent = "\t\t\t\t\t";
            string newLine = indent + "\"LaunchOptions\"" + "\t\t" + "\"" + esc + "\"";

            int found = -1;
            for (int i = r[0] + 2; i < r[1]; i++)
                if (Regex.IsMatch(lines[i], "^\\s*\"LaunchOptions\"", I)) { found = i; break; }
            if (found >= 0) lines[found] = newLine; else lines.Insert(r[1], newLine);

            File.WriteAllText(f, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
            log("[*] 실행 옵션 " + (on ? "등록" : "해제") + " — " + f);

            if (wasRunning)
            {
                log("[*] Steam 다시 켜는 중...");
                var psi = new ProcessStartInfo(Path.Combine(root, "steam.exe")) { UseShellExecute = false, WorkingDirectory = root };
                psi.EnvironmentVariables.Remove("ELECTRON_RUN_AS_NODE");     // 물려받으면 Steam 이 띄우는 런처가 Node 로 돌아 안 뜬다
                Process.Start(psi);
            }
        }
    }
}
