// 자동 생성: gen_from_ps1.py — 손으로 고치지 말 것 (원본 .ps1 의 $FWM_SITES / $FWM_TRAMPS)
namespace Hoi4EnginePatcher
{
    static class PatchData
    {
        public const uint ExcRva = 0x03696000;
        public const uint ExcSize = 1690740;

        public static readonly Site[] Sites =
        {
            new Site(0x00128421, "4000", "0004"),
            new Site(0x00BA2D7A, "305d", "406d"),
            new Site(0x00BACB8C, "488bcee8fc72e7ff", "e97f28c902909090"),
            new Site(0x01307FBA, "305d", "406d"),
            new Site(0x013092A3, "440fb73cd84b8d3cbf", "e94889700290909090"),
            new Site(0x0130A124, "4c5300000f2ec17a0274304084b4c34853", "445d00000f2ec17a0274304084b4c3405d"),
            new Site(0x0130A14A, "4c53", "445d"),
            new Site(0x0130A171, "4c5300000f2ec17a0274324084b4c34853", "445d00000f2ec17a0274324084b4c3405d"),
            new Site(0x0130A197, "4c53", "445d"),
            new Site(0x0130A1C0, "4c5300000f2ec17a02742f4084b4c34853", "445d00000f2ec17a02742f4084b4c3405d"),
            new Site(0x0130A1E6, "4c53", "445d"),
            new Site(0x0130A20C, "4c5300000f2ec17a0274324084b4c34853", "445d00000f2ec17a0274324084b4c3405d"),
            new Site(0x0130A232, "4c53", "445d"),
            new Site(0x0130A294, "4c5300000f2ec17a02742d4084b4c34853", "445d00000f2ec17a02742d4084b4c3405d"),
            new Site(0x0130A2BA, "4c53", "445d"),
            new Site(0x0130A905, "4c5300000f2ec17a0274304084b4c34853", "445d00000f2ec17a0274304084b4c3405d"),
            new Site(0x0130A92B, "4c53", "445d"),
            new Site(0x0130A952, "4c5300000f2ec17a0274324084b4c34853", "445d00000f2ec17a0274324084b4c3405d"),
            new Site(0x0130A978, "4c53", "445d"),
            new Site(0x0130A9A1, "4c5300000f2ec17a02742f4084b4c34853", "445d00000f2ec17a02742f4084b4c3405d"),
            new Site(0x0130A9C7, "4c53", "445d"),
            new Site(0x0130A9ED, "4c5300000f2ec17a0274324084b4c34853", "445d00000f2ec17a0274324084b4c3405d"),
            new Site(0x0130AA13, "4c53", "445d"),
            new Site(0x0130AA84, "4c5300000f2ec17a02742d4084b4c34853", "445d00000f2ec17a02742d4084b4c3405d"),
            new Site(0x0130AAAA, "4c53", "445d"),
            new Site(0x0130B291, "8b97a4000000", "e91a09570290"),
            new Site(0x0130F5BA, "0fb70c40", "8b0c8090"),
            new Site(0x013101B4, "884c2453f30f11442454", "e9071a70029090909090"),
            new Site(0x01311DA7, "4853", "405d"),
            new Site(0x01641508, "412bc499f7f9", "e92373240290"),
            new Site(0x01BB4994, "0fb77e0c", "8b7e0c90"),
            new Site(0x01BB4B2A, "66397c48fe", "397c88fc90"),
            new Site(0x01BB4B6F, "4803d241b802000000ff50084c8bf049634c240c66893c484d6344240c4d03c0498b1424", "e9ccf9e50190909090ff50084c8bf049634c240c893c88904d6344240ce9cff9e5019090"),
            new Site(0x01BB4BDF, "66893c48", "893c8890"),
            new Site(0x02292BA2, "2003b900002003", "0006b900000006"),
            new Site(0x0248A173, "01", "04"),
            new Site(0x0268474A, "305d", "406d"),
        };

        public static readonly Tramp[] Tramps =
        {
            new Tramp(0x00BACB8C, 0, 0x037EC070, "4885f60f84e3d736fd488bcee84fd11efde96ed736fd", new uint[] { 0x00BAC590, 0x00BAE5F4, 0x02F185D4 }, new[] { new Reloc(5, 4, 0x00BACBFC, 3, 6), new Reloc(13, 4, 0x00A23E90, 12, 5), new Reloc(18, 4, 0x00BACB94, 17, 5) }),
            new Tramp(0x013092A3, 0, 0x03982070, "440fb73cd80fb67cd803c1ef03c1e7104109ff4b8d3cbf83e107e99d768ffd", new uint[] { 0x01309040, 0x01309445, 0x02F9E67C }, new[] { new Reloc(27, 4, 0x013092AC, 26, 5) }),
            new Tramp(0x0130B291, 0, 0x037EC030, "483bdf0f849bf7a8fd8b97a4000000e9d3f6a8fd", new uint[] { 0x0130B264, 0x0130B3DA, 0x02F9EA9C }, new[] { new Reloc(5, 4, 0x0130B354, 3, 6), new Reloc(16, 4, 0x0130B297, 15, 5) }),
            new Tramp(0x013101B4, 0, 0x03982040, "8b442464c1e810c1e00308c888442453f30f11442454e9e3e58ffd", new uint[] { 0x0130F070, 0x01310529, 0x02F9E820 }, new[] { new Reloc(23, 4, 0x013101BE, 22, 5) }),
            new Tramp(0x01641508, 0, 0x037EC000, "85c90f851200000041837d14000f8e688edbfd31f6e9ec8ddbfd412bc499f7f9e9b98cdbfd", new uint[] { 0x01641450, 0x016416BC, 0x02EE72BC }, new[] { new Reloc(4, 4, 0x037EC01A, 2, 6), new Reloc(15, 4, 0x016416AB, 13, 6), new Reloc(22, 4, 0x01641636, 21, 5), new Reloc(33, 4, 0x0164150E, 32, 5) }),
            new Tramp(0x01BB4B6F, 0, 0x03982000, "48c1e20241b804000000e929061afe", new uint[] { 0x01BB29E0, 0x01BB5307, 0x03037600 }, new[] { new Reloc(11, 4, 0x01BB4B78, 10, 5) }),
            new Tramp(0x01BB4B6F, 29, 0x03982020, "49c1e002498b1424e926061afe", new uint[] { 0x01BB29E0, 0x01BB5307, 0x03037600 }, new[] { new Reloc(9, 4, 0x01BB4B93, 8, 5) }),
        };
    }
}
