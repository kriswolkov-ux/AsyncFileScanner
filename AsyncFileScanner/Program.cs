using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncFileScanner.Models;
using AsyncFileScanner.Services;

Console.WriteLine("=== FILE SCANNER ===\n");

Console.WriteLine("Введите путь к директории:");
string directoryPath = Console.ReadLine()?.Trim() ?? "";

Console.WriteLine("\nВведите расширения через запятую (например: .cs,.txt,.json):");
string extensionsInput = Console.ReadLine()?.Trim() ?? "";

// Нормализуем расширения: убираем пробелы и гарантируем наличие точки в начале
var extensions = extensionsInput
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(ext => ext.StartsWith(".") ? ext : $".{ext}")
    .ToList();

var options = new ScanOptions(directoryPath, extensions);
var probe = new FileProbe();
var scanner = new FileScanner(probe);

using var cts = new CancellationTokenSource();

Console.WriteLine("\nСканирование...");
Console.WriteLine("Нажмите Enter для отмены.\n");

// Запускаем фоновую задачу, которая ждёт нажатия Enter и инициирует отмену
var cancelTask = Task.Run(() =>
{
    Console.ReadLine();
    cts.Cancel();
});

try
{
    // Запускаем сканирование
    var summary = await scanner.ScanAsync(options, cts.Token);

    Console.WriteLine(); // Пустая строка для разделения

    if (cts.IsCancellationRequested)
    {
        Console.WriteLine("Сканирование отменено пользователем.");
        Console.WriteLine($"Обработано файлов: {summary.ScannedFilesCount}");
    }
    else
    {
        Console.WriteLine("Найденные файлы:\n");

        int index = 1;
        foreach (var result in summary.Results)
        {
            // Форматирование: имя файла выравнивается по левому краю до 25 символов
            Console.WriteLine($"[{index++}] {result.FileName,-25} {result.SizeInBytes} байт");
        }

        Console.WriteLine("\nСканирование завершено.");
        Console.WriteLine($"Всего найдено файлов: {summary.SuccessfulFilesCount}");
        Console.WriteLine($"Общий размер: {summary.TotalBytes} байт");

        if (summary.Errors.Count > 0)
        {
            Console.WriteLine($"\n[!] Ошибок при обработке пропущено: {summary.Errors.Count}");
        }
    }
}
catch (OperationCanceledException)
{
    // Этот блок сработает, если исключение проброшено из глубины (хотя мы обрабатываем это через break)
    Console.WriteLine("\nСканирование принудительно прервано.");
}