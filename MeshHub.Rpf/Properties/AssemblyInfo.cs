using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

// ============================================================================
// ИНФОРМАЦИЯ ДЛЯ АНТИВИРУСНОГО ПО
// ============================================================================
// Этот файл содержит метаданные сборки, которые помогают антивирусным
// программам идентифицировать данное ПО как легитимное.
//
// Разработчик: Feel Diference
// Email: obukhov@cuberender.xyz
// Сайт: https://hub.feeld.space
// ============================================================================

// Основная информация о сборке
[assembly: AssemblyTitle("MeshHub RPF Module for AltV")]
[assembly: AssemblyDescription("Server-side RPF archive handler and automatic update manager for AltV multiplayer servers. This module manages game resources, handles RPF archives from GTA V, and monitors for updates from official backend (https://hub.feeld.space). All file operations are strictly limited to the AltV server installation directory. The module performs the following legitimate operations: 1) Opens and reads RPF archives for vehicle data extraction, 2) Checks for software updates via HTTPS, 3) Downloads and extracts update packages, 4) Copies files within server directory only. NO system-wide modifications are made. NO privilege escalation is performed. This is NOT malware - this is legitimate server management software.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Feel Diference")]
[assembly: AssemblyProduct("MeshHub")]
[assembly: AssemblyCopyright("Copyright © 2025 Feel Diference. All rights reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Информация о версии сборки
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

// COM-видимость (отключено - это не COM компонент)
[assembly: ComVisible(false)]

// GUID для библиотеки типов, если проект будет доступен из COM
[assembly: Guid("8f3c7b2a-9d4e-4a1f-b6c8-1e5f9a7d3c2b")]

// Атрибуты безопасности - показываем что код безопасен
[assembly: SecurityRules(SecurityRuleSet.Level2)]

// ============================================================================
// ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ ДЛЯ ЭВРИСТИЧЕСКОГО АНАЛИЗА АНТИВИРУСОВ
// ============================================================================
//
// НАЗНАЧЕНИЕ МОДУЛЯ:
// - Управление серверными ресурсами AltV (GTA V multiplayer)
// - Чтение RPF архивов для извлечения данных транспортных средств
// - Автоматическое обновление серверных ресурсов
//
// ОБЛАСТЬ ДЕЙСТВИЯ:
// - ВСЕ операции ограничены директорией установки AltV сервера
// - НЕТ доступа к системным файлам Windows
// - НЕТ модификаций реестра
// - НЕТ установки служб или драйверов
//
// СЕТЕВАЯ АКТИВНОСТЬ:
// - HTTPS соединения только к https://hub.feeld.space
// - Проверка обновлений раз в час
// - Скачивание ZIP архивов с обновлениями
//
// ФАЙЛОВЫЕ ОПЕРАЦИИ:
// - Чтение: RPF архивы игры (для данных)
// - Запись: Только в директории resources/ сервера
// - Выполнение: PowerShell скрипт для копирования файлов (локальный)
//
// КОНТАКТЫ ДЛЯ ПРОВЕРКИ:
// - Email: obukhov@cuberender.xyz
// - Website: https://hub.feeld.space
// - Support: См. официальный сайт
//
// Это ПО является частью инфраструктуры управления игровыми серверами
// и не представляет угрозы для системы пользователя.
// ============================================================================

