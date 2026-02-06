using System;

namespace PidGui.Models
{
    public sealed record NetworkConnectionInfo(
        string Protocol,
        string LocalAddress,
        int LocalPort,
        string RemoteAddress,
        int RemotePort,
        string? State,
        int Pid,
        string? ProcessName,
        int? ParentId);
}
