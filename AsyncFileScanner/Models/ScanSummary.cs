using System;
using System.Collections.Generic;

namespace AsyncFileScanner.Models;

public sealed record ScanSummary(
    int ScannedFilesCount,
    int SuccessfulFilesCount,
    long TotalBytes,
    TimeSpan Elapsed,
    IReadOnlyList<ScanResult> Results,
    IReadOnlyList<string> Errors);