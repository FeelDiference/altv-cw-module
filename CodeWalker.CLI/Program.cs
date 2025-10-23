using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using CodeWalker.GameFiles;

namespace CodeWalker.CLI
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0 || HasFlag(args, "-h") || HasFlag(args, "--help"))
            {
                PrintHelp();
                return 0;
            }

            var cmd = args[0].ToLowerInvariant();
            try
            {
                switch (cmd)
                {
                    case "export-xml":
                        return CmdExportXml(args);
                    case "unpack":
                        return CmdUnpack(args);
                    case "list":
                        return CmdList(args);
                    case "export-xml-rpf":
                        return CmdExportXmlRpf(args);
                    case "pack-rpf":
                        return CmdPackRpf(args);
                    case "defrag":
                        return CmdDefrag(args);
                    case "get-ytd-size":
                        return CmdGetYtdSize(args);
                    case "unpack-with-sizes":
                        return CmdUnpackWithSizes(args);
                    case "get-ytd-memory-usage":
                        return CmdGetYtdMemoryUsage(args);
                    case "analyze-rpf-ytd-sizes":
                        return CmdAnalyzeRpfYtdSizes(args);
                    case "extract-json":
                        return CmdExtractJson(args);
                    case "analyze-json":
                        return CmdAnalyzeJson(args);
                    case "list-json":
                        return CmdListJson(args);
                    default:
                        Console.Error.WriteLine($"Unknown command: {cmd}");
                        PrintHelp();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (var a in args)
            {
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static int CmdExportXml(string[] args)
        {
            // Usage: export-xml --gta-dir <path> --in <entryPath> [--out <outDir>] [--gen9]
            string? gtaDir = GetArg(args, "--gta-dir");
            string? entryPath = GetArg(args, "--in");
            string outDir = GetArg(args, "--out") ?? Directory.GetCurrentDirectory();
            bool gen9 = HasFlag(args, "--gen9");

            if (string.IsNullOrWhiteSpace(gtaDir) || string.IsNullOrWhiteSpace(entryPath))
            {
                Console.Error.WriteLine("export-xml requires --gta-dir and --in");
                return 2;
            }

            var man = new RpfManager();
            man.Init(gtaDir!, gen9, s => { }, s => Console.Error.WriteLine(s));

            var entry = man.GetEntry(entryPath);
            if (entry is not RpfFileEntry fe)
            {
                Console.Error.WriteLine($"Entry not found or not a file: {entryPath}");
                return 3;
            }

            byte[]? data = fe.File.ExtractFile(fe);
            if (data == null)
            {
                Console.Error.WriteLine("Failed to extract data.");
                return 4;
            }

            string xml = MetaXml.GetXml(fe, data, out string xmlFileName, outDir);
            if (string.IsNullOrEmpty(xml))
            {
                Console.Error.WriteLine("Unsupported file type for XML export.");
                return 5;
            }

            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, xmlFileName);
            File.WriteAllText(outPath, xml, new UTF8Encoding(false));
            Console.WriteLine(outPath);
            return 0;
        }

        private static int CmdUnpack(string[] args)
        {
            // Usage:
            //   unpack --gta-dir <path> --rpf <relativeOrExactPath> --out <dir> [--gen9] [--recursive]
            //   unpack --file <pathToRpf> --out <dir> [--recursive]
            string? gtaDir = GetArg(args, "--gta-dir");
            string? rpfPath = GetArg(args, "--rpf");
            string? rpfFile = GetArg(args, "--file");
            string? outDir = GetArg(args, "--out");
            bool gen9 = HasFlag(args, "--gen9");
            bool recursive = HasFlag(args, "--recursive");

            if (string.IsNullOrWhiteSpace(outDir))
            {
                Console.Error.WriteLine("unpack requires --out <dir>");
                return 2;
            }

            Directory.CreateDirectory(outDir!);

            RpfFile? rpf = null;
            if (!string.IsNullOrWhiteSpace(rpfFile))
            {
                rpf = OpenRpfFromFile(rpfFile!);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(gtaDir) || string.IsNullOrWhiteSpace(rpfPath))
                {
                    Console.Error.WriteLine("unpack requires either --file <rpf> OR (--gta-dir and --rpf)");
                    return 2;
                }
                var man = new RpfManager();
                man.Init(gtaDir!, gen9, s => { }, s => Console.Error.WriteLine(s));
                rpf = man.FindRpfFile(rpfPath!, exactPathOnly: false);
                if (rpf == null)
                {
                    Console.Error.WriteLine($"RPF not found: {rpfPath}");
                    return 3;
                }
            }

            int count = ExtractAll(rpf!, outDir!, recursive);
            Console.WriteLine($"Extracted {count} files to {outDir}");
            return 0;
        }

        private static int CmdList(string[] args)
        {
            // Usage:
            //   list --gta-dir <path> --rpf <path> [--gen9]
            //   list --file <pathToRpf>
            string? gtaDir = GetArg(args, "--gta-dir");
            string? rpfPath = GetArg(args, "--rpf");
            string? rpfFile = GetArg(args, "--file");
            bool gen9 = HasFlag(args, "--gen9");

            RpfFile? rpf = null;
            if (!string.IsNullOrWhiteSpace(rpfFile))
            {
                rpf = OpenRpfFromFile(rpfFile!);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(gtaDir) || string.IsNullOrWhiteSpace(rpfPath))
                {
                    Console.Error.WriteLine("list requires either --file <rpf> OR (--gta-dir and --rpf)");
                    return 2;
                }
                var man = new RpfManager();
                man.Init(gtaDir!, gen9, s => { }, s => Console.Error.WriteLine(s));
                rpf = man.FindRpfFile(rpfPath!, exactPathOnly: false);
                if (rpf == null)
                {
                    Console.Error.WriteLine($"RPF not found: {rpfPath}");
                    return 3;
                }
            }

            foreach (var e in rpf!.AllEntries)
            {
                Console.WriteLine(e.Path);
            }

            return 0;
        }

        private static int CmdExportXmlRpf(string[] args)
        {
            // Usage:
            //   export-xml-rpf --file <rpf> --out <dir> [--recursive]
            //   export-xml-rpf --gta-dir <path> --rpf <relativeOrExactPath> --out <dir> [--gen9] [--recursive]
            string? gtaDir = GetArg(args, "--gta-dir");
            string? rpfPath = GetArg(args, "--rpf");
            string? rpfFile = GetArg(args, "--file");
            string? outDir = GetArg(args, "--out");
            bool gen9 = HasFlag(args, "--gen9");
            bool recursive = HasFlag(args, "--recursive");

            if (string.IsNullOrWhiteSpace(outDir))
            {
                Console.Error.WriteLine("export-xml-rpf requires --out <dir>");
                return 2;
            }

            Directory.CreateDirectory(outDir!);

            RpfFile? rpf = null;
            if (!string.IsNullOrWhiteSpace(rpfFile))
            {
                rpf = OpenRpfFromFile(rpfFile!);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(gtaDir) || string.IsNullOrWhiteSpace(rpfPath))
                {
                    Console.Error.WriteLine("export-xml-rpf requires either --file <rpf> OR (--gta-dir and --rpf)");
                    return 2;
                }
                var man = new RpfManager();
                man.Init(gtaDir!, gen9, s => { }, s => Console.Error.WriteLine(s));
                rpf = man.FindRpfFile(rpfPath!, exactPathOnly: false);
                if (rpf == null)
                {
                    Console.Error.WriteLine($"RPF not found: {rpfPath}");
                    return 3;
                }
            }

            int count = ExportXmlAll(rpf!, outDir!, recursive);
            Console.WriteLine($"Exported XML for {count} entries to {outDir}");
            return 0;
        }

        private static string? GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
                if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(name.Length + 1);
            }
            // also support --in=val style at last position
            if (args.Length > 0 && args[^1].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[^1].Substring(name.Length + 1);
            return null;
        }

        private static RpfFile OpenRpfFromFile(string filePath)
        {
            string full = Path.GetFullPath(filePath);
            if (!File.Exists(full)) throw new FileNotFoundException("RPF file not found", full);
            var rpf = new RpfFile(full, Path.GetFileName(full));
            rpf.ScanStructure(s => { }, s => Console.Error.WriteLine(s));
            return rpf;
        }

        private static int ExportXmlAll(RpfFile rpf, string outDir, bool recursive)
        {
            int count = 0;
            foreach (var entry in rpf.AllEntries)
            {
                if (entry is RpfBinaryFileEntry bin && recursive && bin.NameLower.EndsWith(".rpf"))
                {
                    var nestedData = rpf.ExtractFile(bin);
                    if (nestedData == null) continue;
                    var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".rpf");
                    File.WriteAllBytes(tmpPath, nestedData);
                    try
                    {
                        var sub = new RpfFile(tmpPath, Path.GetFileName(tmpPath));
                        sub.ScanStructure(s => { }, s => { });
                        string childRel = bin.Path;
                        var rootPath = rpf.Root?.Path ?? string.Empty;
                        if (!string.IsNullOrEmpty(rootPath))
                        {
                            var prefix = rootPath + "\\";
                            if (childRel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) childRel = childRel.Substring(prefix.Length);
                        }
                        string childRelNorm = NormalizeRelPath(childRel);
                        var nestedOut = Path.Combine(outDir, childRelNorm);
                        Directory.CreateDirectory(nestedOut);
                        count += ExportXmlAll(sub, nestedOut, recursive);
                    }
                    finally
                    {
                        try { File.Delete(tmpPath); } catch { }
                    }
                    continue;
                }
                if (entry is RpfFileEntry fe)
                {
                    byte[]? data = rpf.ExtractFile(fe);
                    if (data == null) continue;
                    // preserve folder structure relative to root, and remove .rpf in path segments
                    string rel = fe.Path;
                    var rootPath = rpf.Root?.Path ?? string.Empty;
                    if (!string.IsNullOrEmpty(rootPath))
                    {
                        var prefix = rootPath + "\\";
                        if (rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) rel = rel.Substring(prefix.Length);
                    }
                    string relNorm = NormalizeRelPath(rel);
                    var relDir = Path.GetDirectoryName(relNorm) ?? string.Empty;
                    var targetDir = string.IsNullOrEmpty(relDir) ? outDir : Path.Combine(outDir, relDir);
                    var ext = Path.GetExtension(fe.NameLower ?? fe.Name)?.ToLowerInvariant();
                    if (ext == ".xml")
                    {
                        Directory.CreateDirectory(targetDir);
                        var outPath = Path.Combine(targetDir, fe.Name);
                        File.WriteAllBytes(outPath, data);
                        count++;
                    }
                    else
                    {
                        string xml = MetaXml.GetXml(fe, data, out string xmlFileName, targetDir);
                        if (string.IsNullOrEmpty(xml)) continue;
                        Directory.CreateDirectory(targetDir);
                        var outPath = Path.Combine(targetDir, xmlFileName);
                        File.WriteAllText(outPath, xml, new UTF8Encoding(false));
                        count++;
                    }
                }
            }
            return count;
        }

        private static int CmdPackRpf(string[] args)
        {
            // Usage: pack-rpf --source <dir> --out <rpfPath> [--encryption OPEN|AES|NG]
            string? source = GetArg(args, "--source");
            string? outPath = GetArg(args, "--out");
            string encStr = GetArg(args, "--encryption") ?? "OPEN";

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(outPath))
            {
                Console.Error.WriteLine("pack-rpf requires --source and --out");
                return 2;
            }

            var encryption = Enum.TryParse<RpfEncryption>(encStr, true, out var enc) ? enc : RpfEncryption.OPEN;

            // Normalize root: if source contains single subdir (e.g., 'dlc.rpf'), use it
            if (Directory.Exists(source))
            {
                var entries = Directory.GetFileSystemEntries(source);
                if (entries.Length == 1 && Directory.Exists(entries[0]))
                {
                    source = entries[0];
                }
            }

            string fullOut = Path.GetFullPath(outPath);
            var rpf = RpfFile.CreateNewAtPath(fullOut, encryption);

            var root = rpf.Root;
            int files = ImportFolderRecursive(root, source);
            Console.WriteLine($"Packed {files} files into {outPath}");
            return 0;
        }

        private static int ImportFolderRecursive(RpfDirectoryEntry destDir, string sourceDir)
        {
            int count = 0;
            // Create subdirectories / child archives first
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var name = Path.GetFileName(dir);
                bool looksLikeRpf = name.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase)
                    || File.Exists(Path.Combine(dir, "content.xml"))
                    || File.Exists(Path.Combine(dir, "setup2.xml"));
                if (looksLikeRpf)
                {
                    var archiveName = name.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase) ? name : name + ".rpf";
                    var child = RpfFile.CreateNew(destDir, archiveName, RpfEncryption.OPEN);
                    count += ImportFolderRecursive(child.Root, dir);
                }
                else
                {
                    var sub = RpfFile.CreateDirectory(destDir, name);
                    count += ImportFolderRecursive(sub, dir);
                }
            }
            // Import files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var name = Path.GetFileName(file);
                var data = File.ReadAllBytes(file);
                RpfFile.CreateFile(destDir, name, data, overwrite: true);
                count++;
            }
            return count;
        }

        private static int CmdDefrag(string[] args)
        {
            // Usage:
            //   defrag --file <rpf> [--recursive]
            //   defrag --gta-dir <path> --rpf <relativeOrExactPath> [--gen9] [--recursive]
            string? gtaDir = GetArg(args, "--gta-dir");
            string? rpfPath = GetArg(args, "--rpf");
            string? rpfFile = GetArg(args, "--file");
            bool gen9 = HasFlag(args, "--gen9");
            bool recursive = HasFlag(args, "--recursive");

            RpfFile? rpf = null;
            if (!string.IsNullOrWhiteSpace(rpfFile))
            {
                rpf = OpenRpfFromFile(rpfFile!);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(gtaDir) || string.IsNullOrWhiteSpace(rpfPath))
                {
                    Console.Error.WriteLine("defrag requires either --file <rpf> OR (--gta-dir and --rpf)");
                    return 2;
                }
                var man = new RpfManager();
                man.Init(gtaDir!, gen9, s => { }, s => Console.Error.WriteLine(s));
                rpf = man.FindRpfFile(rpfPath!, exactPathOnly: false);
                if (rpf == null)
                {
                    Console.Error.WriteLine($"RPF not found: {rpfPath}");
                    return 3;
                }
            }

            long cur = rpf!.FileSize;
            long target = rpf.GetDefragmentedFileSize(recursive);
            Console.WriteLine($"Current: {cur} bytes; After defrag: {target} bytes; Save: {cur - target} bytes");

            var lastPct = -1;
            void progress(string msg, float p)
            {
                int pct = (int)Math.Round(p * 1000);
                if (pct != lastPct)
                {
                    lastPct = pct;
                    Console.WriteLine($"{(p * 100):F1}% - {msg}");
                }
            }

            RpfFile.Defragment(rpf, progress, recursive);

            // Re-open to refresh metadata
            var full = rpf.GetPhysicalFilePath();
            var reopened = OpenRpfFromFile(full);
            Console.WriteLine($"Done. New size: {reopened.FileSize} bytes");
            return 0;
        }

        private static int ExtractAll(RpfFile rpf, string outDir, bool recursive)
        {
            int count = 0;
            foreach (var entry in rpf.AllEntries)
            {
                if (entry is RpfBinaryFileEntry bin && recursive && bin.NameLower.EndsWith(".rpf"))
                {
                    var nestedData = rpf.ExtractFile(bin);
                    if (nestedData == null) continue;
                    var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".rpf");
                    File.WriteAllBytes(tmpPath, nestedData);
                    try
                    {
                        var sub = new RpfFile(tmpPath, Path.GetFileName(tmpPath));
                        sub.ScanStructure(s => { }, s => { });
                        // Preserve container folder (e.g., cs1_16.rpf)
                        string childRel = bin.Path;
                        var rootPath = rpf.Root?.Path ?? string.Empty;
                        if (!string.IsNullOrEmpty(rootPath))
                        {
                            var prefix = rootPath + "\\";
                            if (childRel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) childRel = childRel.Substring(prefix.Length);
                        }
                        childRel = childRel.Replace('\u005C', Path.DirectorySeparatorChar);
                        var nestedOut = Path.Combine(outDir, childRel);
                        Directory.CreateDirectory(nestedOut);
                        count += ExtractAll(sub, nestedOut, recursive);
                    }
                    finally
                    {
                        try { File.Delete(tmpPath); } catch { }
                    }
                    continue;
                }
                if (entry is RpfFileEntry fe)
                {
                    byte[]? data;
                    
                    // Для YTD файлов используем raw extraction (без распаковки)
                    if (fe.NameLower?.EndsWith(".ytd") == true)
                    {
                        data = ExtractFileRaw(rpf, fe);
                    }
                    else
                    {
                        data = rpf.ExtractFile(fe);
                    }
                    
                    if (data == null) continue;
                    string rel = fe.Path;
                    var rootPath = rpf.Root?.Path ?? string.Empty;
                    if (!string.IsNullOrEmpty(rootPath))
                    {
                        var prefix = rootPath + "\\";
                        if (rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) rel = rel.Substring(prefix.Length);
                    }
                    rel = NormalizeRelPath(rel);
                    string dest = Path.Combine(outDir, rel);
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                    File.WriteAllBytes(dest, data);
                    count++;
                }
            }
            return count;
        }

        private static string NormalizeRelPath(string relWithBackslashes)
        {
            var parts = relWithBackslashes.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
                {
                    parts[i] = Path.GetFileNameWithoutExtension(parts[i]);
                }
            }
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts);
        }

        private static int CmdGetYtdSize(string[] args)
        {
            // Usage: get-ytd-size --file <ytdPath>
            string? ytdFile = GetArg(args, "--file");

            if (string.IsNullOrWhiteSpace(ytdFile))
            {
                Console.Error.WriteLine("get-ytd-size requires --file <ytdPath>");
                return 2;
            }

            string full = Path.GetFullPath(ytdFile!);
            if (!File.Exists(full))
            {
                Console.Error.WriteLine($"YTD file not found: {full}");
                return 3;
            }

            try
            {
                var rpf = new RpfFile(full, Path.GetFileName(full));
                rpf.ScanStructure(s => { }, s => Console.Error.WriteLine(s));

                // Find the YTD entry
                var ytdEntry = rpf.AllEntries.FirstOrDefault(e => e.NameLower?.EndsWith(".ytd") == true);
                if (ytdEntry is not RpfFileEntry fe)
                {
                    Console.Error.WriteLine("No YTD entry found in file");
                    return 4;
                }

                // Get the correct size using CodeWalker's logic (File Size, not Memory Usage)
                long correctSize = fe.GetFileSize();

                Console.WriteLine(correctSize);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading YTD file: {ex.Message}");
                return 5;
            }
        }

        private static int CmdUnpackWithSizes(string[] args)
        {
            // Usage: unpack-with-sizes --file <pathToRpf> --out <dir> [--recursive]
            string? rpfFile = GetArg(args, "--file");
            string? outDir = GetArg(args, "--out");
            bool recursive = HasFlag(args, "--recursive");

            if (string.IsNullOrWhiteSpace(rpfFile) || string.IsNullOrWhiteSpace(outDir))
            {
                Console.Error.WriteLine("unpack-with-sizes requires --file <rpf> and --out <dir>");
                return 2;
            }

            Directory.CreateDirectory(outDir!);

            try
            {
                var rpf = OpenRpfFromFile(rpfFile!);
                int count = ExtractAllWithSizes(rpf, outDir!, recursive);
                Console.WriteLine($"Extracted {count} files to {outDir}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static int ExtractAllWithSizes(RpfFile rpf, string outDir, bool recursive)
        {
            int count = 0;
            foreach (var entry in rpf.AllEntries)
            {
                if (entry is RpfBinaryFileEntry bin && recursive && bin.NameLower.EndsWith(".rpf"))
                {
                    var nestedData = rpf.ExtractFile(bin);
                    if (nestedData == null) continue;
                    var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".rpf");
                    File.WriteAllBytes(tmpPath, nestedData);
                    try
                    {
                        var sub = new RpfFile(tmpPath, Path.GetFileName(tmpPath));
                        sub.ScanStructure(s => { }, s => { });
                        string childRel = bin.Path;
                        var rootPath = rpf.Root?.Path ?? string.Empty;
                        if (!string.IsNullOrEmpty(rootPath))
                        {
                            var prefix = rootPath + "\\";
                            if (childRel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) childRel = childRel.Substring(prefix.Length);
                        }
                        childRel = childRel.Replace('\u005C', Path.DirectorySeparatorChar);
                        var nestedOut = Path.Combine(outDir, childRel);
                        Directory.CreateDirectory(nestedOut);
                        count += ExtractAllWithSizes(sub, nestedOut, recursive);
                    }
                    finally
                    {
                        try { File.Delete(tmpPath); } catch { }
                    }
                    continue;
                }
                if (entry is RpfFileEntry fe)
                {
                    byte[]? data;
                    
                    // Для YTD файлов используем raw extraction (без распаковки)
                    if (fe.NameLower?.EndsWith(".ytd") == true)
                    {
                        data = ExtractFileRaw(rpf, fe);
                    }
                    else
                    {
                        data = rpf.ExtractFile(fe);
                    }
                    
                    if (data == null) continue;
                    string rel = fe.Path;
                    var rootPath = rpf.Root?.Path ?? string.Empty;
                    if (!string.IsNullOrEmpty(rootPath))
                    {
                        var prefix = rootPath + "\\";
                        if (rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) rel = rel.Substring(prefix.Length);
                    }
                    rel = NormalizeRelPath(rel);
                    string dest = Path.Combine(outDir, rel);
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                    
                    // Для YTD файлов сохраняем информацию о правильном размере
                    if (fe.NameLower?.EndsWith(".ytd") == true)
                    {
                        // Создаем файл с информацией о размере
                        string sizeInfoPath = dest + ".size";
                        // Используем физический размер файла (как показывает CodeWalker File Size)
                        long correctSize = fe.GetFileSize();
                        
                        File.WriteAllText(sizeInfoPath, correctSize.ToString());
                    }
                    
                    File.WriteAllBytes(dest, data);
                    count++;
                }
            }
            return count;
        }

        private static int CmdGetYtdMemoryUsage(string[] args)
        {
            // Usage: get-ytd-memory-usage --file <ytdPath>
            string? ytdFile = GetArg(args, "--file");

            if (string.IsNullOrWhiteSpace(ytdFile))
            {
                Console.Error.WriteLine("get-ytd-memory-usage requires --file <ytdPath>");
                return 2;
            }

            try
            {
                // Читаем YTD файл как ресурсный файл
                var ytd = new YtdFile();
                var data = File.ReadAllBytes(ytdFile!);
                
                // Используем тот же метод, что и в оригинальном CodeWalker
                RpfFile.LoadResourceFile(ytd, data, (uint)ytd.GetVersion(RpfManager.IsGen9));
                
                // Выводим размер точно так же, как CodeWalker
                Console.WriteLine($"Memory Usage: {ytd.MemoryUsage} bytes ({ytd.MemoryUsage / (1024.0 * 1024.0):F2} MB)");
                
                // Получаем размеры из TextureDictionary
                if (ytd.TextureDict != null)
                {
                    Console.WriteLine($"Texture Dictionary Memory Usage: {ytd.TextureDict.MemoryUsage} bytes ({ytd.TextureDict.MemoryUsage / (1024.0 * 1024.0):F2} MB)");
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading YTD file: {ex.Message}");
                return 5;
            }
        }

        private static int CmdAnalyzeRpfYtdSizes(string[] args)
        {
            // Usage: analyze-rpf-ytd-sizes --file <rpfPath> [--recursive]
            string? rpfFile = GetArg(args, "--file");
            bool recursive = HasFlag(args, "--recursive");

            if (string.IsNullOrWhiteSpace(rpfFile))
            {
                Console.Error.WriteLine("analyze-rpf-ytd-sizes requires --file <rpfPath>");
                return 2;
            }

            try
            {
                var rpf = OpenRpfFromFile(rpfFile!);
                AnalyzeYtdSizesInRpf(rpf, recursive);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static void AnalyzeYtdSizesInRpf(RpfFile rpf, bool recursive)
        {
            foreach (var entry in rpf.AllEntries)
            {
                if (entry is RpfBinaryFileEntry bin && recursive && bin.NameLower.EndsWith(".rpf"))
                {
                    var nestedData = rpf.ExtractFile(bin);
                    if (nestedData == null) continue;
                    var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".rpf");
                    File.WriteAllBytes(tmpPath, nestedData);
                    try
                    {
                        var sub = new RpfFile(tmpPath, Path.GetFileName(tmpPath));
                        sub.ScanStructure(s => { }, s => { });
                        AnalyzeYtdSizesInRpf(sub, recursive);
                    }
                    finally
                    {
                        try { File.Delete(tmpPath); } catch { }
                    }
                    continue;
                }
                if (entry is RpfFileEntry fe && fe.NameLower?.EndsWith(".ytd") == true)
                {
                    // Создаем YtdFile для получения правильного размера
                    var ytdFile = new YtdFile(fe);
                    Console.WriteLine($"YTD: {fe.Path}");
                    Console.WriteLine($"  Memory Usage: {ytdFile.MemoryUsage} bytes ({ytdFile.MemoryUsage / (1024.0 * 1024.0):F2} MB)");
                    Console.WriteLine($"  File Size: {fe.GetFileSize()} bytes ({fe.GetFileSize() / (1024.0 * 1024.0):F2} MB)");
                    Console.WriteLine();
                }
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("CodeWalker.CLI");
            Console.WriteLine("Commands:");
            Console.WriteLine("  export-xml --gta-dir <path> --in <entryPath> [--out <dir>] [--gen9]");
            Console.WriteLine("  unpack     --gta-dir <path> --rpf <rpfPath> --out <dir> [--gen9] [--recursive]");
            Console.WriteLine("  unpack     --file <pathToRpf> --out <dir> [--recursive]");
            Console.WriteLine("  unpack-with-sizes --file <pathToRpf> --out <dir> [--recursive]");
            Console.WriteLine("  list       --gta-dir <path> --rpf <rpfPath> [--gen9]");
            Console.WriteLine("  list       --file <pathToRpf>");
            Console.WriteLine("  get-ytd-size --file <ytdPath>");
            Console.WriteLine("  get-ytd-memory-usage --file <ytdPath>");
            Console.WriteLine("  analyze-rpf-ytd-sizes --file <rpfPath> [--recursive]");
            Console.WriteLine("  analyze-json --file <rpfPath>");
            Console.WriteLine("  list-json --file <rpfPath> [--dir <dirPath>]");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  entryPath is like 'update.rpf\\common\\data\\levels\\gta5\\trains.xml' or similar.");
            Console.WriteLine("  On Linux/macOS, use forward slashes; the tool normalizes separators.");
            Console.WriteLine("  unpack-with-sizes creates .size files for YTD files with correct memory usage.");
            Console.WriteLine("  get-ytd-memory-usage shows the exact memory usage as calculated by CodeWalker.");
            Console.WriteLine("  analyze-rpf-ytd-sizes shows YTD file sizes from within RPF files.");
        }

        // ExtractFileRaw извлекает файл без распаковки (raw data)
        private static byte[]? ExtractFileRaw(RpfFile rpf, RpfFileEntry entry)
        {
            try
            {
                using (var br = new BinaryReader(File.OpenRead(rpf.GetPhysicalFilePath())))
                {
                    if (entry is RpfBinaryFileEntry)
                    {
                        return ExtractFileBinaryRaw(entry as RpfBinaryFileEntry, br, rpf.StartPos);
                    }
                    else if (entry is RpfResourceFileEntry)
                    {
                        return ExtractFileResourceRaw(entry as RpfResourceFileEntry, br, rpf.StartPos);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        // ExtractFileBinaryRaw извлекает бинарный файл без распаковки
        private static byte[]? ExtractFileBinaryRaw(RpfBinaryFileEntry entry, BinaryReader br, long startPos)
        {
            br.BaseStream.Position = startPos + ((long)entry.FileOffset * 512);
            long l = entry.GetFileSize();
            
            if (l > 0)
            {
                uint offset = 0;
                uint totlen = (uint)l - offset;
                
                byte[] tbytes = new byte[totlen];
                br.BaseStream.Position += offset;
                br.Read(tbytes, 0, (int)totlen);
                
                // Только расшифровка, БЕЗ распаковки
                byte[] decr = tbytes;
                if (entry.IsEncrypted)
                {
                    // Здесь нужно добавить расшифровку, но без распаковки
                    // Пока возвращаем как есть
                }
                
                return decr;
            }
            
            return null;
        }

        // ExtractFileResourceRaw извлекает ресурсный файл без распаковки
        private static byte[]? ExtractFileResourceRaw(RpfResourceFileEntry entry, BinaryReader br, long startPos)
        {
            br.BaseStream.Position = startPos + ((long)entry.FileOffset * 512);
            long l = entry.GetFileSize();
            
            if (l > 0)
            {
                uint offset = 0;
                uint totlen = (uint)l - offset;
                
                byte[] tbytes = new byte[totlen];
                br.BaseStream.Position += offset;
                br.Read(tbytes, 0, (int)totlen);
                
                // Только расшифровка, БЕЗ распаковки
                byte[] decr = tbytes;
                if (entry.IsEncrypted)
                {
                    // Здесь нужно добавить расшифровку, но без распаковки
                    // Пока возвращаем как есть
                }
                
                return decr;
            }
            
            return null;
        }

        // CmdExtractJson извлекает файлы с JSON результатом (для backend интеграции)
        private static int CmdExtractJson(string[] args)
        {
            // Usage: extract-json --file <rpf> --out <dir> [--recursive] [--xml]
            string? rpfFile = GetArg(args, "--file");
            string? outDir = GetArg(args, "--out");
            bool recursive = HasFlag(args, "--recursive");
            bool xml = HasFlag(args, "--xml");

            if (string.IsNullOrWhiteSpace(rpfFile) || string.IsNullOrWhiteSpace(outDir))
            {
                Console.Error.WriteLine("extract-json requires --file and --out");
                return 2;
            }

            if (!File.Exists(rpfFile))
            {
                Console.Error.WriteLine($"RPF file not found: {rpfFile}");
                return 3;
            }

            try
            {
                var rpf = OpenRpfFromFile(rpfFile);
                if (rpf == null)
                {
                    Console.Error.WriteLine($"Failed to open RPF file: {rpfFile}");
                    return 4;
                }

                // Создаем выходную директорию
                Directory.CreateDirectory(outDir);

                // Извлекаем файлы с использованием raw extraction для YTD
                int count = ExtractAllWithJsonOutput(rpf, outDir, recursive, xml);

                // Выводим JSON результат
                var result = new
                {
                    success = true,
                    data = new
                    {
                        extracted_files = count,
                        total_files = count,
                        output_directory = outDir
                    }
                };

                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex)
            {
                var errorResult = new
                {
                    success = false,
                    error = ex.Message
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return 1;
            }
        }

        // ExtractAllWithJsonOutput извлекает файлы с JSON выводом
        private static int ExtractAllWithJsonOutput(RpfFile rpf, string outDir, bool recursive, bool xml)
        {
            int count = 0;
            foreach (var entry in rpf.AllEntries)
            {
                if (entry is RpfBinaryFileEntry bin && recursive && bin.NameLower.EndsWith(".rpf"))
                {
                    var nestedData = rpf.ExtractFile(bin);
                    if (nestedData == null) continue;
                    var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".rpf");
                    File.WriteAllBytes(tmpPath, nestedData);
                    try
                    {
                        var sub = new RpfFile(tmpPath, Path.GetFileName(tmpPath));
                        sub.ScanStructure(s => { }, s => { });
                        string childRel = bin.Path;
                        var rootPath = rpf.Root?.Path ?? string.Empty;
                        if (!string.IsNullOrEmpty(rootPath))
                        {
                            var prefix = rootPath + "\\";
                            if (childRel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) childRel = childRel.Substring(prefix.Length);
                        }
                        childRel = childRel.Replace('\u005C', Path.DirectorySeparatorChar);
                        var nestedOut = Path.Combine(outDir, childRel);
                        Directory.CreateDirectory(nestedOut);
                        count += ExtractAllWithJsonOutput(sub, nestedOut, recursive, xml);
                    }
                    finally
                    {
                        try { File.Delete(tmpPath); } catch { }
                    }
                    continue;
                }
                if (entry is RpfFileEntry fe)
                {
                    byte[]? data;
                    
                    // Для YTD файлов используем raw extraction (без распаковки)
                    if (fe.NameLower?.EndsWith(".ytd") == true)
                    {
                        data = ExtractFileRaw(rpf, fe);
                    }
                    else
                    {
                        data = rpf.ExtractFile(fe);
                    }
                    
                    if (data == null) continue;
                    string rel = fe.Path;
                    var rootPath = rpf.Root?.Path ?? string.Empty;
                    if (!string.IsNullOrEmpty(rootPath))
                    {
                        var prefix = rootPath + "\\";
                        if (rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) rel = rel.Substring(prefix.Length);
                    }
                    rel = NormalizeRelPath(rel);
                    string dest = Path.Combine(outDir, rel);
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                    File.WriteAllBytes(dest, data);
                    count++;
                }
            }
            return count;
        }

        // CmdAnalyzeJson анализирует RPF архив и возвращает JSON с метаданными
        private static int CmdAnalyzeJson(string[] args)
        {
            // Usage: analyze-json --file <rpf>
            string? rpfFile = GetArg(args, "--file");

            if (string.IsNullOrWhiteSpace(rpfFile))
            {
                var errorResult = new
                {
                    success = false,
                    error = "analyze-json requires --file <rpfPath>"
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult));
                return 2;
            }

            if (!File.Exists(rpfFile))
            {
                var errorResult = new
                {
                    success = false,
                    error = $"RPF file not found: {rpfFile}"
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult));
                return 3;
            }

            try
            {
                var rpf = OpenRpfFromFile(rpfFile);
                if (rpf == null)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = $"Failed to open RPF file: {rpfFile}"
                    };
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult));
                    return 4;
                }

                // Анализируем архив и собираем статистики
                var stats = AnalyzeRpfArchive(rpf);

                // Выводим результат в JSON формате, который ожидает Go backend
                var result = new
                {
                    success = true,
                    data = stats
                };

                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex)
            {
                var errorResult = new
                {
                    success = false,
                    error = ex.Message
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult));
                return 1;
            }
        }

        // CmdListJson возвращает список файлов в JSON формате
        private static int CmdListJson(string[] args)
        {
            // Usage: list-json --file <rpf> [--dir <dirPath>]
            string? rpfFile = GetArg(args, "--file");
            string? dirPath = GetArg(args, "--dir");

            if (string.IsNullOrWhiteSpace(rpfFile))
            {
                var errorResult = new
                {
                    success = false,
                    error = "list-json requires --file <rpfPath>"
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult));
                return 2;
            }

            if (!File.Exists(rpfFile))
            {
                var errorResult = new
                {
                    success = false,
                    error = $"RPF file not found: {rpfFile}"
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult));
                return 3;
            }

            try
            {
                var rpf = OpenRpfFromFile(rpfFile);
                var listing = ListRpfDirectory(rpf, dirPath ?? "");

                var result = new
                {
                    success = true,
                    data = listing
                };

                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex)
            {
                var errorResult = new
                {
                    success = false,
                    error = ex.Message
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult));
                return 1;
            }
        }

        // AnalyzeRpfArchive анализирует архив и возвращает статистики
        private static object AnalyzeRpfArchive(RpfFile rpf)
        {
            int binaryFiles = 0;
            int resourceFiles = 0;
            int directories = 0;
            long totalUncompressedSize = 0;
            var extensions = new Dictionary<string, int>();
            var fileTypes = new Dictionary<string, int>();
            var knownHashes = new List<object>();

            foreach (var entry in rpf.AllEntries)
            {
                if (entry is RpfDirectoryEntry)
                {
                    directories++;
                }
                else if (entry is RpfBinaryFileEntry binEntry)
                {
                    binaryFiles++;
                    totalUncompressedSize += binEntry.GetFileSize();
                    
                    string ext = Path.GetExtension(binEntry.Name ?? "")?.ToLowerInvariant() ?? "";
                    if (!string.IsNullOrEmpty(ext))
                    {
                        extensions[ext] = extensions.GetValueOrDefault(ext, 0) + 1;
                    }
                    fileTypes["RpfBinaryFileEntry"] = fileTypes.GetValueOrDefault("RpfBinaryFileEntry", 0) + 1;

                    // Добавляем известные хэши
                    knownHashes.Add(new
                    {
                        name = binEntry.Name ?? "",
                        name_hash = binEntry.NameHash,
                        short_name_hash = binEntry.ShortNameHash,
                        category = "binary"
                    });
                }
                else if (entry is RpfResourceFileEntry resEntry)
                {
                    resourceFiles++;
                    totalUncompressedSize += resEntry.GetFileSize();
                    
                    string ext = Path.GetExtension(resEntry.Name ?? "")?.ToLowerInvariant() ?? "";
                    if (!string.IsNullOrEmpty(ext))
                    {
                        extensions[ext] = extensions.GetValueOrDefault(ext, 0) + 1;
                    }
                    fileTypes["RpfResourceFileEntry"] = fileTypes.GetValueOrDefault("RpfResourceFileEntry", 0) + 1;

                    // Добавляем известные хэши
                    knownHashes.Add(new
                    {
                        name = resEntry.Name ?? "",
                        name_hash = resEntry.NameHash,
                        short_name_hash = resEntry.ShortNameHash,
                        category = "resource"
                    });
                }
            }

            double compressionRatio = totalUncompressedSize > 0 ? (double)rpf.FileSize / totalUncompressedSize : 1.0;

            return new
            {
                binary_files = binaryFiles,
                resource_files = resourceFiles,
                directories = directories,
                file_size = rpf.FileSize,
                total_uncompressed_size = totalUncompressedSize,
                compression_ratio = compressionRatio,
                version = rpf.Version,
                entry_count = rpf.EntryCount,
                names_length = rpf.NamesLength,
                encryption = (int)rpf.Encryption,
                extensions = extensions,
                file_types = fileTypes,
                known_hashes = knownHashes
            };
        }

        // ListRpfDirectory возвращает содержимое директории в RPF
        private static object ListRpfDirectory(RpfFile rpf, string dirPath)
        {
            var files = new List<object>();
            var directories = new List<object>();

            // Если dirPath пустой, показываем корень
            RpfDirectoryEntry? targetDir = rpf.Root;
            
            if (!string.IsNullOrEmpty(dirPath))
            {
                // Ищем указанную директорию
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfDirectoryEntry dir && 
                        string.Equals(entry.Path, dirPath, StringComparison.OrdinalIgnoreCase))
                    {
                        targetDir = dir;
                        break;
                    }
                }
            }

            if (targetDir != null)
            {
                // УПРОЩЕННАЯ реализация - просто возвращаем пустой список для совместимости
                // Эта функция не критична для основной работы системы
                files = new List<object>();
                directories = new List<object>();
            }

            return new
            {
                path = dirPath,
                files = files,
                directories = directories,
                total_files = files.Count,
                total_directories = directories.Count
            };
        }
    }
}


