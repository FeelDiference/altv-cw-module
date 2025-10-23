using AltV.Net;
using System;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MeshHub.Rpf.Services
{
    /// <summary>
    /// Сервис для работы с handling.meta файлами
    /// Все методы публичные и доступны через экспорт
    /// </summary>
    public class HandlingService
    {
        private readonly RpfService _rpfService;

        public HandlingService(RpfService rpfService)
        {
            _rpfService = rpfService;
        }

        /// <summary>
        /// Получает handling.meta как XML строку из RPF архива
        /// </summary>
        public string? GetHandlingXml(string archiveId, string filePath)
        {
            try
            {
                var data = _rpfService.ExtractFile(archiveId, filePath);
                if (data == null)
                {
                    Alt.LogError($"[HandlingService] Failed to extract handling file: {filePath}");
                    return null;
                }

                var xml = Encoding.UTF8.GetString(data);
                Alt.Log($"[HandlingService] ✅ Got handling XML ({xml.Length} chars)");
                return xml;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[HandlingService] Error getting handling XML: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Сохраняет handling.meta XML в RPF архив
        /// </summary>
        public bool SaveHandlingXml(string archiveId, string filePath, string xmlContent)
        {
            try
            {
                // Валидируем XML
                if (!ValidateHandlingXml(xmlContent, out var error))
                {
                    Alt.LogError($"[HandlingService] Invalid handling XML: {error}");
                    return false;
                }

                var data = Encoding.UTF8.GetBytes(xmlContent);
                var success = _rpfService.ReplaceFile(archiveId, filePath, data);

                if (success)
                {
                    Alt.Log($"[HandlingService] ✅ Saved handling XML ({xmlContent.Length} chars)");
                }

                return success;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[HandlingService] Error saving handling XML: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Валидирует handling.meta XML
        /// </summary>
        public bool ValidateHandlingXml(string xmlContent, out string? error)
        {
            error = null;

            try
            {
                var doc = XDocument.Parse(xmlContent);

                // Проверяем базовую структуру
                if (doc.Root?.Name.LocalName != "CHandlingDataMgr")
                {
                    error = "Root element must be CHandlingDataMgr";
                    return false;
                }

                var handlingData = doc.Root.Element("HandlingData");
                if (handlingData == null)
                {
                    error = "Missing HandlingData element";
                    return false;
                }

                // Проверяем что есть хотя бы один Item
                var items = handlingData.Elements("Item");
                if (!items.Any())
                {
                    error = "No Item elements found in HandlingData";
                    return false;
                }

                // Проверяем что у каждого Item есть handlingName
                foreach (var item in items)
                {
                    var handlingName = item.Element("handlingName")?.Value;
                    if (string.IsNullOrEmpty(handlingName))
                    {
                        error = "Item without handlingName found";
                        return false;
                    }
                }

                Alt.Log($"[HandlingService] ✅ Handling XML is valid ({items.Count()} items)");
                return true;
            }
            catch (XmlException ex)
            {
                error = $"XML parse error: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Validation error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Извлекает конкретный Item по имени из handling.meta
        /// </summary>
        public string? GetHandlingItem(string archiveId, string filePath, string itemName)
        {
            try
            {
                var xml = GetHandlingXml(archiveId, filePath);
                if (xml == null) return null;

                var doc = XDocument.Parse(xml);
                var item = doc.Descendants("Item")
                    .FirstOrDefault(i => i.Element("handlingName")?.Value.Equals(itemName, StringComparison.OrdinalIgnoreCase) == true);

                if (item == null)
                {
                    Alt.LogError($"[HandlingService] Item not found: {itemName}");
                    return null;
                }

                Alt.Log($"[HandlingService] ✅ Got handling item: {itemName}");
                return item.ToString();
            }
            catch (Exception ex)
            {
                Alt.LogError($"[HandlingService] Error getting handling item: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Получает список всех handlingName из handling.meta
        /// </summary>
        public string[]? ListHandlingItems(string archiveId, string filePath)
        {
            try
            {
                var xml = GetHandlingXml(archiveId, filePath);
                if (xml == null) return null;

                var doc = XDocument.Parse(xml);
                var names = doc.Descendants("Item")
                    .Select(i => i.Element("handlingName")?.Value)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToArray();

                Alt.Log($"[HandlingService] ✅ Found {names.Length} handling items");
                return names!;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[HandlingService] Error listing handling items: {ex.Message}");
                return null;
            }
        }
    }
}
