using AltV.Net;
using MeshHub.Core.GameFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MeshHub.Rpf.Services
{
    /// <summary>
    /// Сервис для работы с интерьерами (MLO) из RPF архивов
    /// Поиск YTYP файлов с CMloArchetypeDef
    /// </summary>
    public class InteriorService
    {
        private readonly RpfService _rpfService;

        public InteriorService(RpfService rpfService)
        {
            _rpfService = rpfService;
        }

        /// <summary>
        /// Найти YTYP файл с MLO архетипом (CMloArchetypeDef)
        /// Извлекает данные сразу при обходе
        /// </summary>
        public string? FindMloYtypXml(string archiveId, string interiorName)
        {
            try
            {
                Alt.Log($"[InteriorService] 🔍 Searching for YTYP with CMloArchetypeDef");
                
                var rpf = _rpfService.GetRpfArchive(archiveId);
                if (rpf == null) return null;

                // Находим и сразу обрабатываем все .ytyp файлы
                var result = FindMloInYtyps(rpf, rpf.Root, "");
                
                if (result != null)
                {
                    Alt.Log($"[InteriorService] ✅ Found MLO YTYP");
                    return result;
                }

                Alt.LogWarning($"[InteriorService] No MLO found");
                return null;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[InteriorService] Error: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Рекурсивно ищет MLO в YTYP файлах, извлекая данные сразу
        /// </summary>
        private string? FindMloInYtyps(RpfFile rpf, RpfDirectoryEntry directory, string currentPath)
        {
            // Проверяем .ytyp файлы в текущей директории
            foreach (var file in directory.Files)
            {
                if (file.NameLower.EndsWith(".ytyp"))
                {
                    var fullPath = string.IsNullOrEmpty(currentPath) ? file.Name : $"{currentPath}/{file.Name}";
                    Alt.Log($"[InteriorService] Checking {fullPath}");
                    
                    try
                    {
                        var ytypData = rpf.ExtractFile(file);
                        if (ytypData == null) continue;

                        var ytypFile = new YtypFile(file);
                        ytypFile.Load(ytypData, file);

                        string xml;
                        if (ytypFile.Pso != null)
                        {
                            xml = PsoXml.GetXml(ytypFile.Pso);
                        }
                        else if (ytypFile.Meta != null)
                        {
                            xml = MetaXml.GetXml(ytypFile.Meta);
                        }
                        else
                        {
                            continue;
                        }

                        if (xml.Contains("CMloArchetypeDef"))
                        {
                            Alt.Log($"[InteriorService] ✅ Found in {fullPath}");
                            return xml;
                        }
                    }
                    catch (Exception ex)
                    {
                        Alt.LogWarning($"[InteriorService] Error loading {fullPath}: {ex.Message}");
                    }
                }
            }

            // Рекурсия по директориям
            foreach (var subDir in directory.Directories)
            {
                var newPath = string.IsNullOrEmpty(currentPath) ? subDir.Name : $"{currentPath}/{subDir.Name}";
                var result = FindMloInYtyps(rpf, subDir, newPath);
                if (result != null) return result;
            }
            
            // Обрабатываем вложенные RPF
            foreach (var file in directory.Files)
            {
                if (file.NameLower.EndsWith(".rpf"))
                {
                    try
                    {
                        var nestedData = rpf.ExtractFile(file);
                        if (nestedData == null) continue;

                        var tempPath = Path.GetTempFileName();
                        File.WriteAllBytes(tempPath, nestedData);
                        
                        try
                        {
                            var nestedRpf = new RpfFile(tempPath, file.Name);
                            using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                            using (var br = new BinaryReader(fs))
                            {
                                var readHeaderMethod = typeof(RpfFile).GetMethod("ReadHeader", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                readHeaderMethod?.Invoke(nestedRpf, new object[] { br });
                            }
                            
                            var newPath = string.IsNullOrEmpty(currentPath) ? file.Name : $"{currentPath}/{file.Name}";
                            var result = FindMloInYtyps(nestedRpf, nestedRpf.Root, newPath);
                            if (result != null) return result;
                        }
                        finally
                        {
                            if (File.Exists(tempPath))
                                File.Delete(tempPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Alt.LogWarning($"[InteriorService] Error in nested RPF {file.Name}: {ex.Message}");
                    }
                }
            }
            
            return null;
        }

        private YtypFile? LoadYtypFile(RpfFile rpf, RpfFileEntry entry)
        {
            try
            {
                var data = rpf.ExtractFile(entry);
                if (data == null) return null;

                var ytypFile = new YtypFile(entry);
                ytypFile.Load(data, null);
                return ytypFile;
            }
            catch
            {
                return null;
            }
        }

        private MloArchetype? FindAnyMloArchetype(YtypFile ytypFile)
        {
            foreach (var archetype in ytypFile.AllArchetypes)
            {
                if (archetype is MloArchetype mlo)
                {
                    return mlo; // Возвращаем первый найденный MLO
                }
            }
            return null;
        }

        /// <summary>
        /// Рекурсивно находит все .ytyp файлы с реальными RpfFileEntry объектами
        /// </summary>
        private void FindYtypEntries(RpfFile rpf, RpfDirectoryEntry directory, string currentPath, List<(RpfFile, RpfFileEntry, string)> ytypEntries)
        {
            // Ищем .ytyp файлы
            foreach (var file in directory.Files)
            {
                if (file.NameLower.EndsWith(".ytyp"))
                {
                    var fullPath = string.IsNullOrEmpty(currentPath) ? file.Name : $"{currentPath}/{file.Name}";
                    ytypEntries.Add((rpf, file, fullPath));
                    Alt.Log($"[InteriorService] Found YTYP: {fullPath}");
                }
            }

            // Рекурсия по директориям
            foreach (var subDir in directory.Directories)
            {
                var newPath = string.IsNullOrEmpty(currentPath) ? subDir.Name : $"{currentPath}/{subDir.Name}";
                FindYtypEntries(rpf, subDir, newPath, ytypEntries);
            }
            
            // Обрабатываем вложенные RPF
            foreach (var file in directory.Files)
            {
                if (file.NameLower.EndsWith(".rpf"))
                {
                    try
                    {
                        var nestedData = rpf.ExtractFile(file);
                        if (nestedData == null) continue;

                        var tempPath = Path.GetTempFileName();
                        File.WriteAllBytes(tempPath, nestedData);
                        
                        try
                        {
                            var nestedRpf = new RpfFile(tempPath, file.Name);
                            using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                            using (var br = new BinaryReader(fs))
                            {
                                var readHeaderMethod = typeof(RpfFile).GetMethod("ReadHeader", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                readHeaderMethod?.Invoke(nestedRpf, new object[] { br });
                            }
                            
                            var newPath = string.IsNullOrEmpty(currentPath) ? file.Name : $"{currentPath}/{file.Name}";
                            
                            // Индексируем все имена из вложенного RPF
                            if (ModuleMain.JenkIndexService != null)
                            {
                                ModuleMain.JenkIndexService.IndexRpfArchive(nestedRpf);
                            }
                            
                            FindYtypEntries(nestedRpf, nestedRpf.Root, newPath, ytypEntries);
                        }
                        finally
                        {
                            if (File.Exists(tempPath))
                                File.Delete(tempPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Alt.LogWarning($"[InteriorService] Error processing nested RPF {file.Name}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Старый метод FindYtypPaths (оставлен для совместимости)
        /// </summary>
        private void FindYtypPaths(RpfDirectoryEntry directory, string currentPath, List<string> ytypPaths)
        {
            // Проверяем файлы
            foreach (var file in directory.Files)
            {
                var fullPath = string.IsNullOrEmpty(currentPath) 
                    ? file.Name 
                    : $"{currentPath}/{file.Name}";
                
                if (file.NameLower.EndsWith(".ytyp"))
                {
                    ytypPaths.Add(fullPath);
                    Alt.Log($"[InteriorService] Found YTYP: {fullPath}");
                }
                // Вложенный RPF - заходим внутрь
                else if (file.NameLower.EndsWith(".rpf"))
                {
                    try
                    {
                        // Извлекаем вложенный RPF
                        var nestedData = file.File.ExtractFile(file);
                        if (nestedData != null)
                        {
                            // Создаем временный файл
                            var tempPath = Path.GetTempFileName();
                            File.WriteAllBytes(tempPath, nestedData);
                            
                            try
                            {
                                // Открываем вложенный RPF
                                var nestedRpf = new RpfFile(tempPath, file.Name);
                                using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                                using (var br = new BinaryReader(fs))
                                {
                                    var readHeaderMethod = typeof(RpfFile).GetMethod("ReadHeader", 
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    readHeaderMethod?.Invoke(nestedRpf, new object[] { br });
                                }
                                
                                // Рекурсивно ищем внутри
                                FindYtypPaths(nestedRpf.Root, fullPath, ytypPaths);
                            }
                            finally
                            {
                                if (File.Exists(tempPath))
                                    File.Delete(tempPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Alt.LogWarning($"[InteriorService] Error processing nested RPF {file.Name}: {ex.Message}");
                    }
                }
            }

            // Рекурсия по директориям
            foreach (var subDir in directory.Directories)
            {
                var newPath = string.IsNullOrEmpty(currentPath) 
                    ? subDir.Name 
                    : $"{currentPath}/{subDir.Name}";
                FindYtypPaths(subDir, newPath, ytypPaths);
            }
        }

        /// <summary>
        /// Конвертирует YtypFile в XML строку
        /// </summary>
        private string GetYtypXml(YtypFile ytypFile)
        {
            try
            {
                var sb = new StringBuilder();
                
                // Генерируем XML из YtypFile
                // Используем GetXml метод если доступен, иначе строим вручную
                
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sb.AppendLine("<CMapTypes>");
                
                // Extensions
                sb.AppendLine("  <extensions />");
                
                // Archetypes
                if (ytypFile.AllArchetypes != null && ytypFile.AllArchetypes.Length > 0)
                {
                    sb.AppendLine("  <archetypes>");
                    
                    foreach (var archetype in ytypFile.AllArchetypes)
                    {
                        // Получаем XML для каждого архетипа
                        var archetypeXml = GetArchetypeXml(archetype);
                        sb.AppendLine(archetypeXml);
                    }
                    
                    sb.AppendLine("  </archetypes>");
                }
                else
                {
                    sb.AppendLine("  <archetypes />");
                }
                
                // Name
                var nameStr = JenkIndex.TryGetString(ytypFile.NameHash);
                if (!string.IsNullOrEmpty(nameStr))
                {
                    sb.AppendLine($"  <name>{nameStr}</name>");
                }
                else
                {
                    sb.AppendLine("  <name />");
                }
                
                // Dependencies
                sb.AppendLine("  <dependencies />");
                
                sb.AppendLine("</CMapTypes>");
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Alt.LogError($"[InteriorService] ❌ Error converting YTYP to XML: {ex.Message}");
                return "<?xml version=\"1.0\"?><CMapTypes><error>Failed to convert YTYP to XML</error></CMapTypes>";
            }
        }

        /// <summary>
        /// Получает XML представление архетипа
        /// </summary>
        private string GetArchetypeXml(Archetype archetype)
        {
            var sb = new StringBuilder();
            
            if (archetype is MloArchetype mloArch)
            {
                sb.AppendLine("    <Item type=\"CMloArchetypeDef\">");
                sb.AppendLine($"      <name>{JenkIndex.TryGetString(mloArch.Hash)}</name>");
                sb.AppendLine($"      <lodDist value=\"{mloArch.LodDist}\" />");
                
                if (mloArch.entities != null && mloArch.entities.Length > 0)
                {
                    sb.AppendLine("      <entities>");
                    foreach (var entity in mloArch.entities)
                    {
                        var data = entity._Data;
                        sb.AppendLine("        <Item type=\"CEntityDef\">");
                        sb.AppendLine($"          <archetypeName>{JenkIndex.TryGetString(data.archetypeName)}</archetypeName>");
                        sb.AppendLine($"          <flags value=\"{data.flags}\" />");
                        sb.AppendLine($"          <guid value=\"{data.guid}\" />");
                        sb.AppendLine($"          <position x=\"{data.position.X}\" y=\"{data.position.Y}\" z=\"{data.position.Z}\" />");
                        sb.AppendLine($"          <rotation x=\"{data.rotation.X}\" y=\"{data.rotation.Y}\" z=\"{data.rotation.Z}\" w=\"{data.rotation.W}\" />");
                        sb.AppendLine("        </Item>");
                    }
                    sb.AppendLine("      </entities>");
                }
                else
                {
                    sb.AppendLine("      <entities />");
                }
                
                if (mloArch.entitySets != null && mloArch.entitySets.Length > 0)
                {
                    sb.AppendLine("      <entitySets>");
                    foreach (var entitySet in mloArch.entitySets)
                    {
                        sb.AppendLine("        <Item>");
                        sb.AppendLine($"          <name>{JenkIndex.TryGetString(entitySet._Data.name)}</name>");
                        sb.AppendLine("        </Item>");
                    }
                    sb.AppendLine("      </entitySets>");
                }
                
                sb.AppendLine("    </Item>");
            }
            else
            {
                sb.AppendLine("    <Item type=\"CBaseArchetypeDef\">");
                sb.AppendLine($"      <name>{JenkIndex.TryGetString(archetype.Hash)}</name>");
                sb.AppendLine($"      <lodDist value=\"{archetype.LodDist}\" />");
                sb.AppendLine("    </Item>");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Получить Entity Sets из MLO
        /// </summary>
        public string[]? GetMloEntitySets(string archiveId, string interiorName)
        {
            try
            {
                Alt.Log($"[InteriorService] 📦 Getting entity sets from MLO");
                
                // Сначала найдем YTYP с MLO
                var ytypXml = FindMloYtypXml(archiveId, interiorName);
                if (ytypXml == null) return Array.Empty<string>();

                // Простой парсинг entity sets из XML
                var entitySets = new List<string>();
                var lines = ytypXml.Split('\n');
                bool inEntitySets = false;
                
                foreach (var line in lines)
                {
                    if (line.Contains("<entitySets>"))
                    {
                        inEntitySets = true;
                    }
                    else if (line.Contains("</entitySets>"))
                    {
                        break;
                    }
                    else if (inEntitySets && line.Contains("<name>"))
                    {
                        var start = line.IndexOf("<name>") + 6;
                        var end = line.IndexOf("</name>");
                        if (end > start)
                        {
                            entitySets.Add(line.Substring(start, end - start));
                        }
                    }
                }

                Alt.Log($"[InteriorService] ✅ Found {entitySets.Count} entity sets");
                return entitySets.ToArray();
            }
            catch (Exception ex)
            {
                Alt.LogError($"[InteriorService] Error: {ex.Message}");
                return null;
            }
        }
    }
}

