using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AltV.Net;

namespace MeshHub.Rpf.Services
{
    /// <summary>
    /// Сервис автоматического обновления MeshHub ресурса
    /// Проверяет версии на backend и автоматически обновляет ресурс
    /// </summary>
    public class AutoUpdaterService
    {
        private const int UPDATE_CHECK_INTERVAL_MS = 3600000; // 1 час
        private const int INITIAL_DELAY_MS = 10000; // 10 секунд после старта
        private const string RESOURCE_NAME = "meshhub";
        private const string SOFTWARE_NAME = "MeshHub"; // Имя софта на backend для поиска
        
        // Software API Key (начинается с software_)
        // Автоматически создается при загрузке софта через админ-панель
        private const string SOFTWARE_ID = "d29cd9ab-41a9-4b15-8d68-db8b8e1862c8";
        private const string API_KEY = "software_77e2ae52bda09744c445904256e63009f5f0251397f9c1a00f59657bb94711fd";
        private const string BACKEND_URL = "https://hub.feeld.space";
        
        private readonly HttpClient _httpClient;
        private readonly string _resourcePath;
        private readonly string _backupPath;
        
        private Timer? _updateCheckTimer;
        private bool _isChecking = false;
        private string _currentVersion;

        public AutoUpdaterService(string currentVersion)
        {
            _currentVersion = currentVersion;
            
            // Пути к ресурсам (относительно рабочей директории altv-server)
            var cwd = Directory.GetCurrentDirectory();
            _resourcePath = Path.Combine(cwd, "resources", RESOURCE_NAME);
            _backupPath = Path.Combine(cwd, "resources", $"{RESOURCE_NAME}.old");
            
            // Настройка HTTP клиента
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Игнорируем SSL для dev
            };
            
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", API_KEY);
        }

        /// <summary>
        /// Инициализация автообновления
        /// Запускает периодическую проверку обновлений
        /// </summary>
        public void Initialize()
        {
            Alt.Log("[AutoUpdate] 🚀 Initializing auto-updater...");
            
            // Проверяем конфигурацию
            if (!IsConfigured())
            {
                Alt.LogWarning("[AutoUpdate] ⚠️ Auto-update disabled: credentials not configured");
                Alt.LogWarning("[AutoUpdate] 💡 Update SOFTWARE_ID and API_KEY in AutoUpdaterService.cs");
                return;
            }
            
            Alt.Log($"[AutoUpdate] Current version: {_currentVersion}");
            Alt.Log($"[AutoUpdate] Backend URL: {BACKEND_URL}");
            Alt.Log($"[AutoUpdate] Software name: {SOFTWARE_NAME}");
            
            // Первая проверка через 10 секунд после старта
            Task.Run(async () =>
            {
                await Task.Delay(INITIAL_DELAY_MS);
                await CheckForUpdatesAsync();
            });
            
            // Периодическая проверка каждый час
            _updateCheckTimer = new Timer(
                async _ => await CheckForUpdatesAsync(),
                null,
                UPDATE_CHECK_INTERVAL_MS,
                UPDATE_CHECK_INTERVAL_MS
            );
            
            Alt.Log($"[AutoUpdate] ✅ Auto-updater initialized");
            Alt.Log($"[AutoUpdate] Update checks every {UPDATE_CHECK_INTERVAL_MS / 60000} minutes");
        }

        /// <summary>
        /// Проверяет наличие обновлений на backend
        /// Запрашивает конкретный софт по SOFTWARE_ID
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (_isChecking)
            {
                Alt.LogWarning("[AutoUpdate] Update check already in progress");
                return null;
            }
            
            try
            {
                _isChecking = true;
                
                Alt.Log("[AutoUpdate] 🔍 Checking for updates...");
                Alt.Log($"[AutoUpdate] Current version: {_currentVersion}");
                Alt.Log($"[AutoUpdate] Software ID: {MaskSecret(SOFTWARE_ID)}");
                
                // Запрашиваем конкретный софт по ID (более надежно чем поиск по имени)
                var url = $"{BACKEND_URL}/api/software/{SOFTWARE_ID}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Failed to get software: {response.StatusCode}");
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Alt.LogError("[AutoUpdate] ❌ API key is invalid or revoked");
                    }
                    return null;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var software = JsonSerializer.Deserialize<SoftwareInfo>(content);
                
                if (software == null)
                {
                    Alt.LogWarning("[AutoUpdate] ⚠️ Failed to parse software response");
                    return null;
                }
                
                var latestVersion = software.Version ?? "0.0.0";
                Alt.Log($"[AutoUpdate] ✅ Found software: {software.Name}");
                Alt.Log($"[AutoUpdate] Latest version: {latestVersion}");
                Alt.Log($"[AutoUpdate] Server type: {software.ServerType}");
                
                // Сравниваем версии
                var comparison = CompareVersions(latestVersion, _currentVersion);
                
                if (comparison > 0)
                {
                    Alt.Log($"[AutoUpdate] 🎉 New version available: {latestVersion}");
                    
                    var updateInfo = new UpdateInfo
                    {
                        Id = software.Id ?? "",
                        Version = latestVersion,
                        DownloadUrl = software.DownloadUrl ?? "",
                        FileSize = software.FileSize,
                        Name = software.Name ?? ""
                    };
                    
                    // Автоматически скачиваем и устанавливаем
                    Alt.Log($"[AutoUpdate] 🚀 Starting automatic update to version {latestVersion}...");
                    var success = await DownloadAndInstallAsync(updateInfo);
                    
                    if (success)
                    {
                        Alt.Log($"[AutoUpdate] 🎉 Update to {latestVersion} completed successfully!");
                        Alt.Log("[AutoUpdate] 🔄 Please restart the server to load new version");
                    }
                    
                    return updateInfo;
                }
                else if (comparison == 0)
                {
                    Alt.Log("[AutoUpdate] ✅ Already on latest version");
                }
                else
                {
                    Alt.LogWarning("[AutoUpdate] ⚠️ Current version is newer than backend version!");
                }
                
                return null;
            }
            catch (HttpRequestException ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Network error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Update check error: {ex.Message}");
                return null;
            }
            finally
            {
                _isChecking = false;
            }
        }

        /// <summary>
        /// Скачивает и устанавливает обновление
        /// </summary>
        public async Task<bool> DownloadAndInstallAsync(UpdateInfo updateInfo)
        {
            try
            {
                Alt.Log($"[AutoUpdate] 📥 Starting download of version {updateInfo.Version}...");
                Alt.Log($"[AutoUpdate] File size: {updateInfo.FileSize / 1024.0 / 1024.0:F2} MB");
                
                // Создаем бэкап текущей версии
                Alt.Log("[AutoUpdate] 💾 Creating backup of current version...");
                var backupSuccess = CreateBackup();
                
                if (!backupSuccess)
                {
                    Alt.LogError("[AutoUpdate] ❌ Failed to create backup, aborting update for safety");
                    return false;
                }
                
                // DownloadUrl уже содержится в updateInfo (получили из /api/software)
                // Это относительный путь, добавляем BACKEND_URL
                var downloadUrl = updateInfo.DownloadUrl.StartsWith("http") 
                    ? updateInfo.DownloadUrl 
                    : $"{BACKEND_URL}{updateInfo.DownloadUrl}";
                    
                Alt.Log($"[AutoUpdate] Downloading from: {downloadUrl}");
                
                // Скачиваем ZIP файл
                var downloadResponse = await _httpClient.GetAsync(downloadUrl);
                
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    Alt.LogError($"[AutoUpdate] ❌ Download failed: {downloadResponse.StatusCode}");
                    return false;
                }
                
                var zipData = await downloadResponse.Content.ReadAsByteArrayAsync();
                
                // Сохраняем ZIP временно
                var tempDir = Path.Combine(Path.GetTempPath(), "meshhub_updates");
                Directory.CreateDirectory(tempDir);
                
                var zipPath = Path.Combine(tempDir, $"meshhub_{updateInfo.Version}.zip");
                await File.WriteAllBytesAsync(zipPath, zipData);
                
                Alt.Log($"[AutoUpdate] ✅ Downloaded: {zipPath} ({zipData.Length / 1024.0 / 1024.0:F2} MB)");
                
                // Устанавливаем обновление
                Alt.Log("[AutoUpdate] 📦 Installing update...");
                var installSuccess = InstallUpdate(zipPath, updateInfo.Version);
                
                if (installSuccess)
                {
                    Alt.Log($"[AutoUpdate] 🎉 Update to version {updateInfo.Version} installed successfully!");
                    Alt.Log("[AutoUpdate] 🔄 Please restart the server to load new version");
                    Alt.Log("[AutoUpdate] 💡 Command: restart meshhub");
                    return true;
                }
                else
                {
                    Alt.LogError("[AutoUpdate] ❌ Installation failed, restoring backup...");
                    var restoreSuccess = RestoreBackup();
                    
                    if (restoreSuccess)
                    {
                        Alt.Log("[AutoUpdate] ✅ Backup restored successfully");
                    }
                    else
                    {
                        Alt.LogError("[AutoUpdate] ❌ CRITICAL: Failed to restore backup!");
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Download/Install error: {ex.Message}");
                Alt.Log("[AutoUpdate] Attempting to restore backup...");
                RestoreBackup();
                return false;
            }
        }

        /// <summary>
        /// Создает бэкап текущей версии (переименование meshhub → meshhub.old)
        /// </summary>
        private bool CreateBackup()
        {
            try
            {
                // Удаляем старый бэкап если существует
                if (Directory.Exists(_backupPath))
                {
                    Alt.Log($"[AutoUpdate] Removing old backup: {_backupPath}");
                    Directory.Delete(_backupPath, true);
                }
                
                // Переименовываем текущий ресурс в .old
                if (Directory.Exists(_resourcePath))
                {
                    Alt.Log($"[AutoUpdate] Renaming {RESOURCE_NAME} → {RESOURCE_NAME}.old");
                    Directory.Move(_resourcePath, _backupPath);
                    Alt.Log($"[AutoUpdate] ✅ Backup created: {_backupPath}");
                    return true;
                }
                else
                {
                    Alt.LogWarning($"[AutoUpdate] ⚠️ Resource directory not found: {_resourcePath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Backup creation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Устанавливает обновление из ZIP архива
        /// </summary>
        private bool InstallUpdate(string zipPath, string version)
        {
            try
            {
                Alt.Log($"[AutoUpdate] Installing update from: {zipPath}");
                
                // Создаем временную директорию для распаковки
                var extractPath = Path.Combine(Path.GetTempPath(), $"meshhub_extract_{version}_{DateTime.Now.Ticks}");
                Directory.CreateDirectory(extractPath);
                
                // Распаковываем ZIP
                Alt.Log("[AutoUpdate] 📦 Extracting ZIP archive...");
                ZipFile.ExtractToDirectory(zipPath, extractPath);
                Alt.Log("[AutoUpdate] ✅ ZIP extracted successfully");
                
                // Создаем новую директорию ресурса
                Directory.CreateDirectory(_resourcePath);
                
                // Копируем файлы из распакованного архива
                Alt.Log("[AutoUpdate] 🔄 Copying files...");
                CopyDirectory(extractPath, _resourcePath);
                
                Alt.Log("[AutoUpdate] ✅ Files copied successfully");
                
                // Очищаем временные файлы
                Alt.Log("[AutoUpdate] 🗑️ Cleaning up temporary files...");
                try
                {
                    Directory.Delete(extractPath, true);
                    File.Delete(zipPath);
                }
                catch
                {
                    // Игнорируем ошибки очистки
                }
                
                Alt.Log($"[AutoUpdate] ✅ Update to version {version} completed successfully");
                
                return true;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Installation failed: {ex.Message}");
                Alt.LogError($"[AutoUpdate] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Восстанавливает бэкап (переименование meshhub.old → meshhub)
        /// </summary>
        private bool RestoreBackup()
        {
            try
            {
                if (!Directory.Exists(_backupPath))
                {
                    Alt.LogError("[AutoUpdate] ❌ Backup directory not found, cannot restore");
                    return false;
                }
                
                Alt.Log($"[AutoUpdate] 🔄 Restoring backup from: {_backupPath}");
                
                // Удаляем поврежденную версию если существует
                if (Directory.Exists(_resourcePath))
                {
                    Directory.Delete(_resourcePath, true);
                }
                
                // Восстанавливаем из бэкапа
                Directory.Move(_backupPath, _resourcePath);
                
                Alt.Log("[AutoUpdate] ✅ Backup restored successfully");
                return true;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] ❌ Restore failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Копирует директорию рекурсивно
        /// </summary>
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }
            
            // Создаем директорию назначения
            Directory.CreateDirectory(destinationDir);
            
            // Копируем файлы
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetPath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetPath, true);
            }
            
            // Рекурсивно копируем поддиректории
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string targetPath = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, targetPath);
            }
        }

        /// <summary>
        /// Сравнивает версии (semver)
        /// </summary>
        /// <returns>1 если v1 > v2, -1 если v1 < v2, 0 если равны</returns>
        private int CompareVersions(string v1, string v2)
        {
            try
            {
                var parts1 = v1.Split('.');
                var parts2 = v2.Split('.');
                
                int maxLength = Math.Max(parts1.Length, parts2.Length);
                
                for (int i = 0; i < maxLength; i++)
                {
                    int p1 = i < parts1.Length && int.TryParse(parts1[i], out int parsed1) ? parsed1 : 0;
                    int p2 = i < parts2.Length && int.TryParse(parts2[i], out int parsed2) ? parsed2 : 0;
                    
                    if (p1 > p2) return 1;
                    if (p1 < p2) return -1;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Alt.LogError($"[AutoUpdate] Version comparison error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Проверяет что credentials настроены
        /// </summary>
        private bool IsConfigured()
        {
            return !string.IsNullOrEmpty(SOFTWARE_ID) && 
                   !SOFTWARE_ID.StartsWith("ЗАМЕНИТЕ") &&
                   !string.IsNullOrEmpty(API_KEY) &&
                   !API_KEY.StartsWith("ЗАМЕНИТЕ");
        }

        /// <summary>
        /// Маскирует секретное значение для безопасного логирования
        /// </summary>
        private string MaskSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret) || secret.Length <= 8)
            {
                return "***";
            }
            
            return secret.Substring(0, 4) + "***" + secret.Substring(secret.Length - 4);
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            _updateCheckTimer?.Dispose();
            _httpClient?.Dispose();
        }

        // ==================== Вспомогательные классы ====================

        /// <summary>
        /// Информация о программном обеспечении с backend (из списка /api/software)
        /// </summary>
        private class SoftwareInfo
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? Version { get; set; }
            public string? DownloadUrl { get; set; }
            public long FileSize { get; set; }
            public string? ServerType { get; set; }
            public bool IsActive { get; set; }
        }

        /// <summary>
        /// Информация об обновлении
        /// </summary>
        public class UpdateInfo
        {
            public string Id { get; set; } = "";
            public string Version { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public long FileSize { get; set; }
            public string Name { get; set; } = "";
        }
    }
}

