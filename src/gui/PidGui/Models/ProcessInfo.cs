using System;

namespace PidGui.Models
{
    public sealed record ProcessInfo(
        int Id,
        string Name,
        string? WindowTitle,
        string? Path,
        string? CommandLine,
        string? WorkingDirectory,
        string? User,
        int ThreadCount,
        long WorkingSetBytes,
        long PrivateBytes,
        double CpuPercent,
        DateTime? StartTimeUtc,
        int? ParentId,
        string? ParentChain,
        string? Source,
        string? ServiceName
    );
}
