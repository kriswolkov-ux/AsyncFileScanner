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

        var allFiles = Directory
            .EnumerateFiles(options.DirectoryPath, "*.*", SearchOption.AllDirectories)
            .ToList();

        using var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
        var tracker = new ConcurrencyTracker();

        var tasks = allFiles.Select(path =>
            ProcessFileLimitedAsync(path, options, semaphore, tracker, cancellationToken));

        var processed = await Task.WhenAll(tasks);

        foreach (var item in processed)
        {
            Interlocked.Increment(ref scannedFiles);

            if (item.IsError)
            {
                lock (errors) errors.Add(item.ErrorMessage!);
            }
            else if (item.Result is not null)
            {
                lock (results) results.Add(item.Result);
                Interlocked.Add(ref totalBytes, item.Result.SizeInBytes);
            }
        }

        stopwatch.Stop();

        return new ScanSummary(
            scannedFiles,
            results.Count,
            totalBytes,
            errors.Count,
            stopwatch.Elapsed,
            options.MaxConcurrency,
            tracker.Max,
            results,
            errors);
    }

    private async Task<FileProcessItem> ProcessFileLimitedAsync(
        string path,
        ScanOptions options,
        SemaphoreSlim semaphore,
        ConcurrencyTracker tracker,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        await semaphore.WaitAsync(token);

        try
        {
            int current = Interlocked.Increment(ref tracker.Active);

            int max = tracker.Max;
            while (current > max)
            {
                int previous = Interlocked.CompareExchange(ref tracker.Max, current, max);
                if (previous == max) break;
                max = previous;
            }

            try
            {
                var fileInfo = new FileInfo(path);

                if (fileInfo.Length < options.MinSizeBytes || fileInfo.Length > options.MaxSizeBytes)
                {
                    return FileProcessItem.Empty;
                }

                await _probe.ReadHeaderAsync(path, token);

                var result = new ScanResult(Path.GetFileName(path), fileInfo.Length);
                return FileProcessItem.FromResult(result);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return FileProcessItem.FromError($"Ошибка доступа к '{path}': {ex.Message}");
            }
            catch (Exception ex)
            {
                return FileProcessItem.FromError($"Неизвестная ошибка для '{path}': {ex.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref tracker.Active);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(options));

        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException($"Директория не найдена: {options.DirectoryPath}");

        if (options.Extensions == null || options.Extensions.Count == 0)
            throw new ArgumentException("Список расширений не может быть пустым.", nameof(options));

        if (options.MaxConcurrency < 1)
            throw new ArgumentException("Количество одновременных операций должно быть >= 1.", nameof(options));

        if (options.MinSizeBytes < 0 || options.MaxSizeBytes < options.MinSizeBytes)
            throw new ArgumentException("Некорректный диапазон размеров файлов.", nameof(options));
    }

    private sealed class ConcurrencyTracker
    {
        public int Active;
        public int Max;
    }

    internal sealed class FileProcessItem
    {
        public ScanResult? Result { get; }
        public string? ErrorMessage { get; }
        public bool IsError => ErrorMessage is not null;

        private FileProcessItem(ScanResult? result, string? errorMessage)
        {
            Result = result;
            ErrorMessage = errorMessage;
        }

        public static FileProcessItem Empty => new(null, null);
        public static FileProcessItem FromResult(ScanResult r) => new(r, null);
        public static FileProcessItem FromError(string msg) => new(null, msg);
    }
}
