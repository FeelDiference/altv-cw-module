using AltV.Net;
using MeshHub.Core.GameFiles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshHub.Rpf.Services
{
    /// <summary>
    /// Сервис для извлечения mesh данных из .yft файлов
    /// Предоставляет vertices и indices для рендеринга в Three.js
    /// </summary>
    public class MeshService
    {
        private readonly RpfService _rpfService;
        private readonly Dictionary<string, Dictionary<string, object>> _meshCache = new();

        public MeshService(RpfService rpfService)
        {
            _rpfService = rpfService;
        }

        /// <summary>
        /// Извлекает mesh данные из .yft файла автомобиля
        /// Возвращает упрощенные данные готовые для Three.js
        /// </summary>
        public Dictionary<string, object>? ExtractVehicleMeshData(string vehicleName)
        {
            try
            {
                // Проверяем кеш
                if (_meshCache.TryGetValue(vehicleName, out var cachedData))
                {
                    Alt.Log($"[MeshService] ✅ Mesh данные взяты из кеша: {vehicleName}");
                    return cachedData;
                }

                Alt.Log($"[MeshService] Извлечение mesh данных для автомобиля: {vehicleName}");

                // Ищем RPF архив с моделью автомобиля
                var archivePath = FindVehicleArchive(vehicleName);
                
                if (string.IsNullOrEmpty(archivePath))
                {
                    Alt.LogError($"[MeshService] RPF архив не найден для: {vehicleName}");
                    return null;
                }

                Alt.Log($"[MeshService] Найден архив: {archivePath}");

                // Открываем архив
                var archiveId = _rpfService.OpenRpfArchive(archivePath);
                
                if (string.IsNullOrEmpty(archiveId))
                {
                    Alt.LogError($"[MeshService] Не удалось открыть архив: {archivePath}");
                    return null;
                }

                try
                {
                    // Ищем .yft файл автомобиля (умный поиск по стандартным путям)
                    var yftPath = FindYftFileInVehicleArchive(archiveId, vehicleName);
                    if (string.IsNullOrEmpty(yftPath))
                    {
                        Alt.LogError($"[MeshService] .yft файл не найден для: {vehicleName}");
                        return null;
                    }

                    Alt.Log($"[MeshService] Найден .yft файл: {yftPath}");

                    // Извлекаем .yft файл (уже декомпрессирован через ExtractFile)
                    var yftData = _rpfService.ExtractFile(archiveId, yftPath);
                    if (yftData == null || yftData.Length == 0)
                    {
                        Alt.LogError($"[MeshService] Не удалось извлечь .yft файл");
                        return null;
                    }

                    // Парсим .yft файл используя Load(data, entry)
                    // ExtractFile уже декомпрессировал данные, поэтому создаем entry и грузим напрямую
                    var yftFile = new YftFile();
                    var resEntry = RpfFile.CreateResourceFileEntry(ref yftData, 162); // Version 162 для non-gen9
                    yftFile.Load(yftData, resEntry);

                    if (yftFile.Fragment?.Drawable == null)
                    {
                        Alt.LogError($"[MeshService] .yft файл не содержит Drawable");
                        return null;
                    }

                    // Извлекаем mesh данные из Drawable
                    var meshData = ExtractMeshFromDrawable(yftFile.Fragment.Drawable);
                    
                    Alt.Log($"[MeshService] ✅ Mesh данные извлечены: {meshData["vertexCount"]} вершин, {meshData["triangleCount"]} треугольников");
                    
                    // Кешируем результат
                    _meshCache[vehicleName] = meshData;
                    
                    return meshData;
                }
                finally
                {
                    // Закрываем архив
                    _rpfService.CloseRpfArchive(archiveId);
                }
            }
            catch (Exception ex)
            {
                Alt.LogError($"[MeshService] Ошибка извлечения mesh данных: {ex.Message}");
                Alt.LogError($"[MeshService] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Ищет RPF архив с моделью автомобиля
        /// Поддерживает поиск в resources и dlcpacks
        /// </summary>
        private string? FindVehicleArchive(string vehicleName)
        {
            // Базовый путь к ресурсам ALT:V
            var resourcesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources");
            
            Alt.Log($"[MeshService] Поиск архива для {vehicleName} в: {resourcesPath}");

            // Возможные пути к архивам (приоритет: meshhub_resources)
            var possiblePaths = new List<string>
            {
                // Путь 1: meshhub_resources/vehicles/vehicle_name/dlc.rpf (ОСНОВНОЙ!)
                System.IO.Path.Combine(resourcesPath, "meshhub_resources", "vehicles", vehicleName, "dlc.rpf"),
                
                // Путь 2: resources/vehicle_name/*.rpf
                System.IO.Path.Combine(resourcesPath, vehicleName, "stream", $"{vehicleName}.rpf"),
                System.IO.Path.Combine(resourcesPath, vehicleName, $"{vehicleName}.rpf"),
                
                // Путь 3: resources/vehicle_name/stream/*.rpf (любой .rpf в stream)
                System.IO.Path.Combine(resourcesPath, vehicleName, "stream"),
                
                // Путь 4: dlcpacks/vehicle_name/dlc.rpf
                System.IO.Path.Combine(resourcesPath, "..", "dlcpacks", vehicleName, "dlc.rpf"),
                
                // Путь 5: Прямой путь к папке с vehicle (ищем все .rpf)
                System.IO.Path.Combine(resourcesPath, vehicleName)
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    // Если это директория - ищем .rpf файлы внутри
                    if (System.IO.Directory.Exists(path))
                    {
                        Alt.Log($"[MeshService] Проверка директории: {path}");
                        var rpfFiles = System.IO.Directory.GetFiles(path, "*.rpf", System.IO.SearchOption.TopDirectoryOnly);
                        
                        if (rpfFiles.Length > 0)
                        {
                            Alt.Log($"[MeshService] ✅ Найден RPF: {rpfFiles[0]}");
                            return rpfFiles[0];
                        }
                    }
                    // Если это файл - проверяем его существование
                    else if (System.IO.File.Exists(path))
                    {
                        Alt.Log($"[MeshService] ✅ Найден RPF: {path}");
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    Alt.LogWarning($"[MeshService] Ошибка проверки пути {path}: {ex.Message}");
                    continue;
                }
            }

            Alt.LogError($"[MeshService] ❌ RPF архив не найден ни по одному из путей");
            return null;
        }

        /// <summary>
        /// Ищет .yft файл в архиве автомобиля (умный поиск по стандартным путям)
        /// НЕ СКАНИРУЕТ архив, а напрямую пытается извлечь по известным путям
        /// </summary>
        private string? FindYftFileInVehicleArchive(string archiveId, string vehicleName)
        {
            Alt.Log($"[MeshService] Поиск .yft файла для: {vehicleName} (БЕЗ сканирования)");

            // Точный путь к .yft файлу (внутри вложенного vehicles.rpf)
            var yftPath = $"x64/levels/gta5/vehicles/vehicles.rpf/{vehicleName}_hi.yft";

            // Пытаемся извлечь файл по точному пути
            Alt.Log($"[MeshService] Попытка извлечь: {yftPath}");
            
            try
            {
                var data = _rpfService.ExtractFile(archiveId, yftPath);
                if (data != null && data.Length > 0)
                {
                    Alt.Log($"[MeshService] ✅ Файл найден и доступен: {yftPath}");
                    return yftPath;
                }
            }
            catch (Exception ex)
            {
                Alt.LogError($"[MeshService] Ошибка извлечения файла: {yftPath} ({ex.Message})");
            }

            Alt.LogError($"[MeshService] ❌ .yft файл не найден ни по одному из стандартных путей");
            return null;
        }

        /// <summary>
        /// Извлекает mesh данные из Drawable
        /// Возвращает vertices и indices для Three.js
        /// </summary>
        private Dictionary<string, object> ExtractMeshFromDrawable(DrawableBase drawable)
        {
            var allVertices = new List<float>();
            var allIndices = new List<uint>();
            uint vertexOffset = 0;

            // Проходим по всем моделям в Drawable
            if (drawable.DrawableModels?.High != null)
            {
                foreach (var model in drawable.DrawableModels.High)
                {
                    if (model?.Geometries == null) continue;

                    foreach (var geometry in model.Geometries)
                    {
                        if (geometry?.VertexData?.VertexBytes == null) continue;

                        try
                        {
                            // Извлекаем вершины
                            var vertices = ExtractVertices(geometry);
                            var indices = ExtractIndices(geometry, vertexOffset);

                            allVertices.AddRange(vertices);
                            allIndices.AddRange(indices);

                            vertexOffset += (uint)(vertices.Count / 3);
                        }
                        catch (Exception ex)
                        {
                            Alt.LogWarning($"[MeshService] Пропуск геометрии из-за ошибки: {ex.Message}");
                            continue;
                        }
                    }
                }
            }

            // Вычисляем границы модели
            var bounds = CalculateBounds(allVertices);

            // Возвращаем словарь для корректной сериализации в JavaScript
            return new Dictionary<string, object>
            {
                ["vertices"] = allVertices.ToArray(),
                ["indices"] = allIndices.ToArray(),
                ["vertexCount"] = allVertices.Count / 3,
                ["triangleCount"] = allIndices.Count / 3,
                ["bounds"] = new Dictionary<string, object>
                {
                    ["minX"] = bounds.Min.X,
                    ["maxX"] = bounds.Max.X,
                    ["minY"] = bounds.Min.Y,
                    ["maxY"] = bounds.Max.Y,
                    ["minZ"] = bounds.Min.Z,
                    ["maxZ"] = bounds.Max.Z
                }
            };
        }

        /// <summary>
        /// Извлекает вершины из геометрии
        /// </summary>
        private List<float> ExtractVertices(DrawableGeometry geometry)
        {
            var vertices = new List<float>();
            var vd = geometry.VertexData;
            var stride = vd.VertexStride;
            var count = vd.VertexCount;

            for (int i = 0; i < count; i++)
            {
                var offset = i * stride;
                
                // Читаем позицию вершины (первые 12 байт обычно позиция XYZ как float)
                if (offset + 12 <= vd.VertexBytes.Length)
                {
                    var x = BitConverter.ToSingle(vd.VertexBytes, offset + 0);
                    var y = BitConverter.ToSingle(vd.VertexBytes, offset + 4);
                    var z = BitConverter.ToSingle(vd.VertexBytes, offset + 8);
                    
                    vertices.Add(x);
                    vertices.Add(y);
                    vertices.Add(z);
                }
            }

            return vertices;
        }

        /// <summary>
        /// Извлекает индексы из геометрии
        /// </summary>
        private List<uint> ExtractIndices(DrawableGeometry geometry, uint vertexOffset)
        {
            var indices = new List<uint>();
            var id = geometry.IndexBuffer;

            if (id?.Indices == null) return indices;

            // Добавляем индексы с учетом offset
            for (int i = 0; i < id.Indices.Length; i++)
            {
                indices.Add(id.Indices[i] + vertexOffset);
            }

            return indices;
        }

        /// <summary>
        /// Вычисляет границы модели (bounding box)
        /// </summary>
        private BoundsData CalculateBounds(List<float> vertices)
        {
            if (vertices.Count == 0)
            {
                return new BoundsData
                {
                    Min = new Vector3Data { X = 0, Y = 0, Z = 0 },
                    Max = new Vector3Data { X = 0, Y = 0, Z = 0 }
                };
            }

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            for (int i = 0; i < vertices.Count; i += 3)
            {
                var x = vertices[i];
                var y = vertices[i + 1];
                var z = vertices[i + 2];

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (z < minZ) minZ = z;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                if (z > maxZ) maxZ = z;
            }

            return new BoundsData
            {
                Min = new Vector3Data { X = minX, Y = minY, Z = minZ },
                Max = new Vector3Data { X = maxX, Y = maxY, Z = maxZ }
            };
        }
    }

    /// <summary>
    /// Результат извлечения mesh данных
    /// </summary>
    public class MeshDataResult
    {
        /// <summary>
        /// Массив вершин: [x, y, z, x, y, z, ...]
        /// </summary>
        public float[] Vertices { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Массив индексов треугольников: [i1, i2, i3, i1, i2, i3, ...]
        /// </summary>
        public uint[] Indices { get; set; } = Array.Empty<uint>();

        /// <summary>
        /// Количество вершин
        /// </summary>
        public int VertexCount { get; set; }

        /// <summary>
        /// Количество треугольников
        /// </summary>
        public int TriangleCount { get; set; }

        /// <summary>
        /// Границы модели (bounding box)
        /// </summary>
        public BoundsData? Bounds { get; set; }
    }

    /// <summary>
    /// Границы модели
    /// </summary>
    public class BoundsData
    {
        public Vector3Data Min { get; set; } = new Vector3Data();
        public Vector3Data Max { get; set; } = new Vector3Data();
    }

    /// <summary>
    /// 3D вектор для сериализации
    /// </summary>
    public class Vector3Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}

