using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncFileScanner.Models;
using AsyncFileScanner.Services;

Console.WriteLine("=== FILE SCANNER ===\n");

Console.WriteLine("Введите путь к директории:");
string directoryPath = Console.ReadLine()?.Trim() ?? "";

Console.WriteLine("\nВведите расширения через запятую:");
string extensionsInput = Console.ReadLine()?.Trim() ?? "";

var extensions = extensionsInput
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(ext => ext.StartsWith(".") ? ext : $".{ext}")
    .ToList();

Console.WriteLine("\nМинимальный размер (КБ):");
int minKb = int.Parse(Console.ReadLine()?.Trim() ?? "0");

Console.WriteLine("Максимальный размер (КБ):");
int maxKb = int.Parse(Console.ReadLine()?.Trim() ?? "0");

Console.WriteLine("\nВведите максимальное количество одновременных операций:");
int maxConcurrency = int.Parse(Console.ReadLine()?.Trim() ?? "4");

long minBytes = minKb * 1024L;
long maxBytes = maxKb * 1024L;

var options = new ScanOptions(directoryPath, extensions, minBytes, maxBytes, maxConcurrency);
var probe = new FileProbe();
var scanner = new FileScanner(probe);

using var cts = new CancellationTokenSource();

Console.WriteLine("\nСканирование...");
Console.WriteLine("Нажмите Enter для отмены.\n");

var cancelTask = Task.Run(() =>
{
    Console.ReadLine();
    cts.Cancel();
});

try
{
    var summary = await scanner.ScanAsync(options, cts.Token);

    Console.WriteLine();

    if (cts.IsCancellationRequested)
    {
        Console.WriteLine("Сканирование отменено пользователем.");
    }

    Console.WriteLine("=== РЕЗУЛЬТАТ СКАНИРОВАНИЯ ===\n");

    foreach (var result in summary.Results)
    {
        Console.WriteLine($"- {result.FileName,-30} {FormatSize(result.SizeInBytes)}");
    }

    Console.WriteLine($"\nПросмотрено файлов: {summary.ScannedFilesCount}");
    Console.WriteLine($"Подходящих файлов: {summary.MatchedFilesCount}");
    Console.WriteLine($"Общий размер: {FormatSize(summary.TotalBytes)}");
    Console.WriteLine($"Ошибок: {summary.ErrorsCount}");
    Console.WriteLine($"Время выполнения: {summary.Elapsed.TotalSeconds:F2} сек.");
    Console.WriteLine($"\nОграничение: {summary.MaxConcurrency}");
    Console.WriteLine($"Максимум одновременно обрабатываемых файлов: {summary.MaxObservedConcurrency}");

    if (summary.Errors.Count > 0)
    {
        Console.WriteLine("\n[!] Ошибки:");
        foreach (var err in summary.Errors)
        {
            Console.WriteLine($"    {err}");
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nСканирование принудительно прервано.");
}

static string FormatSize(long bytes)
{
    double mb = bytes / (1024.0 * 1024.0);
    return $"{mb:F2} МБ";
}
