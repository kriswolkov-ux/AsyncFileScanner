using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncFileScanner.Models;

namespace AsyncFileScanner.Services;

public sealed class FileScanner
{
    private readonly FileProbe _probe;

    public FileScanner(FileProbe probe)
    {
        _probe = probe;
    }

    public async Task<ScanSummary> ScanAsync(
        ScanOptions options,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<ScanResult>();
        var errors = new List<string>();

        int scannedFiles = 0;
        long totalBytes = 0;

        var allFiles = Directory.EnumerateFiles(options.DirectoryPath, "*.*", SearchOption.AllDirectories);

        foreach (var filePath in allFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break; 
            }

            var extension = Path.GetExtension(filePath);
            if (!options.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            scannedFiles++;

            try
            {
                var fileInfo = new FileInfo(filePath);
                long size = fileInfo.Length;

                await _probe.ReadHeaderAsync(filePath, cancellationToken);

                results.Add(new ScanResult(Path.GetFileName(filePath), size));
                totalBytes += size;
            }
            catch (OperationCanceledException)
            {
                throw; 
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                errors.Add($"Ошибка доступа к '{filePath}': {ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"Неизвестная ошибка для '{filePath}': {ex.Message}");
            }
        }

        stopwatch.Stop();

        return new ScanSummary(
            scannedFiles,
            results.Count,
            totalBytes,
            stopwatch.Elapsed,
            results,
            errors);
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(options));
        }

        if (!Directory.Exists(options.DirectoryPath))
        {
            throw new DirectoryNotFoundException($"Директория не найдена: {options.DirectoryPath}");
        }

        if (options.Extensions == null || options.Extensions.Count == 0)
        {
            throw new ArgumentException("Список расширений не может быть пустым.", nameof(options));
        }
    }
}