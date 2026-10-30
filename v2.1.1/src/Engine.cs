using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hoi4EnginePatcher
{
    sealed class Site
    {
        public readonly uint Rva;
        public readonly byte[] Old;
        public readonly byte[] New;
        public Site(uint rva, string oldHex, string newHex) { Rva = rva; Old = Hex.Parse(oldHex); New = Hex.Parse(newHex); }
    }

    sealed class Reloc
    {
        public readonly int At, Size, IOff, ILen;
        public readonly uint Target;
        public Reloc(int at, int size, uint target, int ioff, int ilen) { At = at; Size = size; Target = target; IOff = ioff; ILen = ilen; }
    }

    sealed class Tramp
    {
        public readonly uint Site, Src;
        public readonly int At;
        public readonly byte[] Body;
        public readonly uint[] Parent;
        public readonly Reloc[] Relocs;
        public Tramp(uint site, int at, uint src, string bodyHex, uint[] parent, Reloc[] relocs)
        { Site = site; At = at; Src = src; Body = Hex.Parse(bodyHex); Parent = parent; Relocs = relocs; }
    }

    static class Hex
    {
        public static byte[] Parse(string h)
        {
            var b = new byte[h.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
            return b;
        }
    }

    sealed class PatchException : Exception
    {
        public PatchException(string code) : base(code) { }
    }

    sealed class Section
    {
        public string Name;
        public uint VSize, VA, RSize, RA;
    }

    sealed class PeInfo
    {
        public int PE, Opt, DD, Sect;
        public int NSec;
        public uint SAlign, FAlign;
        public List<Section> Secs = new List<Section>();

        public static PeInfo Parse(byte[] b)
        {
            var p = new PeInfo();
            p.PE = BitConverter.ToInt32(b, 0x3C);
            p.NSec = BitConverter.ToUInt16(b, p.PE + 6);
            int optsz = BitConverter.ToUInt16(b, p.PE + 20);
            p.Opt = p.PE + 24;
            ushort magic = BitConverter.ToUInt16(b, p.Opt);
            p.DD = p.Opt + (magic == 0x20B ? 112 : 96);
            p.Sect = p.Opt + optsz;
            for (int i = 0; i < p.NSec; i++)
            {
                int o = p.Sect + i * 40;
                p.Secs.Add(new Section
                {
                    Name = Encoding.ASCII.GetString(b, o, 8).TrimEnd('\0'),
                    VSize = BitConverter.ToUInt32(b, o + 8),
                    VA = BitConverter.ToUInt32(b, o + 12),
                    RSize = BitConverter.ToUInt32(b, o + 16),
                    RA = BitConverter.ToUInt32(b, o + 20),
                });
            }
            p.SAlign = BitConverter.ToUInt32(b, p.Opt + 32);
            p.FAlign = BitConverter.ToUInt32(b, p.Opt + 36);
            return p;
        }

        public int RvaToOffset(uint rva)
        {
            foreach (var s in Secs)
            {
                long hi = (long)s.VA + Math.Max(s.VSize, s.RSize);
                if ((long)rva >= s.VA && (long)rva < hi)
                {
                    long off = (long)rva - s.VA;
                    if (off < s.RSize) return (int)(s.RA + off);
                    return -1;
                }
            }
            return -1;
        }
    }

    static class Engine
    {
        public static readonly string[] PatchSections = { ".fwmall", ".fwmfix", ".fwmbdr" };

        /// <summary>정수 나머지로만 올린다 (부동소수 나눗셈은 .5 자리에서 한 칸 넘어간다).</summary>
        public static long Align(long v, long a)
        {
            long r = v % a;
            return r == 0 ? v : v + (a - r);
        }

        /// <summary>섹션 이름만으로 패치 여부를 본다. 섹션표는 파일 머리 4 KB 안에 있으므로 57 MB 를 읽지 않는다.</summary>
        public static bool? IsPatchedFast(string path)
        {
            try
            {
                byte[] head;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    head = new byte[(int)Math.Min(fs.Length, 8192)];
                    int got = 0;
                    while (got < head.Length) { int n = fs.Read(head, got, head.Length - got); if (n <= 0) break; got += n; }
                }
                var info = PeInfo.Parse(head);
                return info.Secs.Any(s => PatchSections.Contains(s.Name));
            }
            catch { return null; }
        }

        public static string SectionNames(string path, out bool patched)
        {
            byte[] head;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                head = new byte[(int)Math.Min(fs.Length, 8192)];
                int got = 0;
                while (got < head.Length) { int n = fs.Read(head, got, head.Length - got); if (n <= 0) break; got += n; }
            }
            var info = PeInfo.Parse(head);
            patched = info.Secs.Any(s => PatchSections.Contains(s.Name));
            return string.Join(" ", info.Secs.Select(s => s.Name));
        }

        static void AddU32(List<byte> list, uint v) { list.AddRange(BitConverter.GetBytes(v)); }

        sealed class Place { public uint Site; public int At; public uint Begin, End, Unwind; }

        /// <summary>전체 엔진 패치. 37곳을 전부 확인한 뒤에야 쓰기 시작한다. 성공하면 .text 에 쓴 바이트 수.</summary>
        public static int Apply(string path, Action<string> log)
        {
            byte[] arr = File.ReadAllBytes(path);
            var info = PeInfo.Parse(arr);

            foreach (var s in info.Secs)
                if (PatchSections.Contains(s.Name)) throw new PatchException("ALREADY_PATCHED");

            foreach (var st in PatchData.Sites)
            {
                int o = info.RvaToOffset(st.Rva);
                if (o < 0) throw new PatchException(string.Format("OFFSET_NOT_FOUND 0x{0:X}", st.Rva));
                for (int i = 0; i < st.Old.Length; i++)
                    if (arr[o + i] != st.Old[i]) throw new PatchException(string.Format("VERIFY_FAIL at 0x{0:X}+{1}", st.Rva, i));
            }
            log("[+] 검증 완료 — " + PatchData.Sites.Length + "곳 모두 순정과 일치");

            uint excRva = BitConverter.ToUInt32(arr, info.DD + 24);
            uint excSize = BitConverter.ToUInt32(arr, info.DD + 28);
            if (excRva != PatchData.ExcRva || excSize != PatchData.ExcSize)
                throw new PatchException(string.Format("EXC_MISMATCH 0x{0:X}/{1}", excRva, excSize));
            int excOff = info.RvaToOffset(excRva);
            if (excOff < 0) throw new PatchException("EXC_NOT_MAPPED");

            var last = info.Secs.OrderBy(s => s.VA).Last();
            uint newVa = (uint)Align((long)last.VA + Math.Max(last.VSize, last.RSize), info.SAlign);
            uint newRa = (uint)Align(arr.Length, info.FAlign);

            var blob = new List<byte>();
            var place = new List<Place>();
            foreach (var t in PatchData.Tramps)
            {
                while (blob.Count % 16 != 0) blob.Add(0x90);
                uint tb = (uint)((long)newVa + blob.Count);
                byte[] body = (byte[])t.Body.Clone();
                foreach (var r in t.Relocs)
                {
                    long tgt = r.Target;
                    if (tgt >= t.Src && tgt < (long)t.Src + body.Length) tgt = (long)tb + (tgt - t.Src);
                    int disp = (int)(tgt - ((long)tb + r.IOff + r.ILen));
                    if (r.Size == 4) Array.Copy(BitConverter.GetBytes(disp), 0, body, r.At, 4);
                    else
                    {
                        if (disp < -128 || disp > 127) throw new PatchException("REL8_OVERFLOW");
                        body[r.At] = unchecked((byte)(sbyte)disp);
                    }
                }
                blob.AddRange(body);
                while (blob.Count % 4 != 0) blob.Add(0x90);
                uint urva = (uint)((long)newVa + blob.Count);
                blob.AddRange(new byte[] { 0x21, 0x00, 0x00, 0x00 });     // UNWIND_INFO: ver 1, UNW_FLAG_CHAININFO
                AddU32(blob, t.Parent[0]);
                AddU32(blob, t.Parent[1]);
                AddU32(blob, t.Parent[2]);
                place.Add(new Place { Site = t.Site, At = t.At, Begin = tb, End = (uint)((long)tb + body.Length), Unwind = urva });
            }

            while (blob.Count % 4 != 0) blob.Add(0x00);
            uint excNewRva = (uint)((long)newVa + blob.Count);
            var tbl = new byte[excSize];
            Array.Copy(arr, excOff, tbl, 0, excSize);
            blob.AddRange(tbl);
            foreach (var p in place.OrderBy(p => p.Begin))
            {
                AddU32(blob, p.Begin);
                AddU32(blob, p.End);
                AddU32(blob, p.Unwind);
            }
            uint excNewSize = (uint)((long)excSize + 12 * place.Count);

            uint vsize = (uint)blob.Count;
            uint rsize = (uint)Align(vsize, info.FAlign);
            log(string.Format("[+] 새 섹션 .fwmall  RVA 0x{0:X}  {1:N0} bytes", newVa, vsize));
            log(string.Format("[+] 예외 테이블 {0:N0} -> {1:N0} 항목", excSize / 12.0, excNewSize / 12.0));

            int written = 0;
            foreach (var st in PatchData.Sites)
            {
                byte[] nw = (byte[])st.New.Clone();
                foreach (var p in place.Where(p => p.Site == st.Rva))
                {
                    int rel = (int)((long)p.Begin - ((long)st.Rva + p.At + 5));
                    nw[p.At] = 0xE9;
                    Array.Copy(BitConverter.GetBytes(rel), 0, nw, p.At + 1, 4);
                }
                int o = info.RvaToOffset(st.Rva);
                Array.Copy(nw, 0, arr, o, nw.Length);
                written += nw.Length;
            }
            log("[+] .text " + PatchData.Sites.Length + "곳 / " + written + " bytes 적용");

            int hdr = info.Sect + info.NSec * 40;
            uint firstRaw = info.Secs.Where(s => s.RA > 0).Min(s => s.RA);
            if (hdr + 40 > firstRaw) throw new PatchException("NO_HEADER_ROOM");
            var ent = new byte[40];
            Array.Copy(Encoding.ASCII.GetBytes(".fwmall"), 0, ent, 0, 7);
            Array.Copy(BitConverter.GetBytes(vsize), 0, ent, 8, 4);
            Array.Copy(BitConverter.GetBytes(newVa), 0, ent, 12, 4);
            Array.Copy(BitConverter.GetBytes(rsize), 0, ent, 16, 4);
            Array.Copy(BitConverter.GetBytes(newRa), 0, ent, 20, 4);
            Array.Copy(BitConverter.GetBytes(0x60000040u), 0, ent, 36, 4);   // CODE | INITIALIZED_DATA | EXECUTE | READ
            Array.Copy(ent, 0, arr, hdr, 40);
            Array.Copy(BitConverter.GetBytes((ushort)(info.NSec + 1)), 0, arr, info.PE + 6, 2);
            uint sizeOfImage = (uint)Align((long)newVa + vsize, info.SAlign);
            Array.Copy(BitConverter.GetBytes(sizeOfImage), 0, arr, info.Opt + 56, 4);
            Array.Copy(BitConverter.GetBytes(excNewRva), 0, arr, info.DD + 24, 4);
            Array.Copy(BitConverter.GetBytes(excNewSize), 0, arr, info.DD + 28, 4);

            int total = (int)((long)newRa + rsize);
            var output = new byte[total];
            Array.Copy(arr, 0, output, 0, arr.Length);
            var b = blob.ToArray();
            Array.Copy(b, 0, output, (int)newRa, b.Length);
            File.WriteAllBytes(path, output);

            log(string.Format("[+] 섹션 {0} -> {1}  SizeOfImage 0x{2:X}  파일 {3:N0} B", info.NSec, info.NSec + 1, sizeOfImage, total));
            return written;
        }
    }
}
