using System;
using System.Collections.Generic;

namespace AsyncFileScanner.Models;

public sealed record ScanSummary(
    int ScannedFilesCount,
    int MatchedFilesCount,
    long TotalBytes,
    int ErrorsCount,
    TimeSpan Elapsed,
    int MaxConcurrency,
    int MaxObservedConcurrency,
    IReadOnlyList<ScanResult> Results,
    IReadOnlyList<string> Errors);
