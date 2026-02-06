using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using PidGui.Models;

namespace PidGui.Services
{
    public sealed class ProcessScannerService
    {
        private sealed record CpuSample(TimeSpan TotalCpu, DateTime TimestampUtc);
        private sealed record WmiProcessInfo(
            int ProcessId,
            string Name,
            int ParentProcessId,
            string? CommandLine,
            string? ExecutablePath,
            DateTime? StartTimeUtc,
            string? Owner
        );

        private readonly object _lock = new();
        private readonly Dictionary<int, CpuSample> _cpuSamples = new();

        public Task<IReadOnlyList<ProcessInfo>> GetSnapshotAsync()
        {
            var now = DateTime.UtcNow;
            var processes = Process.GetProcesses();
            var results = new List<ProcessInfo>(processes.Length);
            var alive = new HashSet<int>();

            var wmiMap = GetWmiProcessMap();
            var serviceMap = GetServiceMap();

            foreach (var process in processes)
            {
                ProcessInfo? info = null;
                try
                {
                    info = BuildInfo(process, now, wmiMap, serviceMap);
                }
                catch
                {
                    // Ignore processes that exit or deny access mid-scan.
                }

                if (info is not null)
                {
                    results.Add(info);
                    alive.Add(info.Id);
                }
            }

            CleanupSamples(alive);

            results.Sort((a, b) => b.CpuPercent.CompareTo(a.CpuPercent));
            return Task.FromResult<IReadOnlyList<ProcessInfo>>(results);
        }

        private ProcessInfo? BuildInfo(
            Process process,
            DateTime now,
            IReadOnlyDictionary<int, WmiProcessInfo> wmiMap,
            IReadOnlyDictionary<int, string> serviceMap)
        {
            var id = process.Id;
            var name = SafeGet(() => process.ProcessName) ?? "<unknown>";
            var title = SafeGet(() => process.MainWindowTitle);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = null;
            }

            WmiProcessInfo? wmiInfo = null;
            if (wmiMap.TryGetValue(id, out var mapped))
            {
                wmiInfo = mapped;
            }

            var path = wmiInfo?.ExecutablePath ?? SafeGet(() => process.MainModule?.FileName);
            var commandLine = wmiInfo?.CommandLine;
            var workingDir = !string.IsNullOrWhiteSpace(path) ? SafeGet(() => Path.GetDirectoryName(path)) : null;
            var user = wmiInfo?.Owner;
            var startTime = wmiInfo?.StartTimeUtc ?? SafeGet(() => process.StartTime.ToUniversalTime());
            var parentId = wmiInfo is null ? null : (int?)wmiInfo.ParentProcessId;
            var parentChain = BuildParentChain(parentId, wmiMap);

            var workingSet = SafeGet(() => process.WorkingSet64);
            var privateBytes = SafeGet(() => process.PrivateMemorySize64);
            var threads = SafeGet(() => process.Threads.Count);
            var cpuPercent = ComputeCpu(process, now);

            var source = ResolveSource(process, serviceMap, out var serviceName);

            return new ProcessInfo(
                id,
                name ?? "<unknown>",
                title,
                path,
                commandLine,
                workingDir,
                user,
                threads,
                workingSet,
                privateBytes,
                cpuPercent,
                startTime,
                parentId,
                parentChain,
                source,
                serviceName
            );
        }

        private static string? BuildParentChain(int? parentId, IReadOnlyDictionary<int, WmiProcessInfo> wmiMap)
        {
            if (parentId is null || parentId <= 0)
            {
                return null;
            }

            var chain = new List<string>();
            var seen = new HashSet<int>();
            var current = parentId.Value;
            var depth = 0;

            while (current > 0 && depth < 8 && !seen.Contains(current))
            {
                seen.Add(current);
                if (!wmiMap.TryGetValue(current, out var info))
                {
                    chain.Add($"pid {current}");
                    break;
                }

                var label = string.IsNullOrWhiteSpace(info.Name) ? $"pid {current}" : $"{info.Name} (pid {current})";
                chain.Add(label);
                current = info.ParentProcessId;
                depth++;
            }

            return chain.Count == 0 ? null : string.Join(" -> ", chain);
        }

        private static IReadOnlyDictionary<int, WmiProcessInfo> GetWmiProcessMap()
        {
            var map = new Dictionary<int, WmiProcessInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, ParentProcessId, Name, CommandLine, ExecutablePath, CreationDate FROM Win32_Process");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var pid = ToInt(obj["ProcessId"]);
                    if (pid == 0)
                    {
                        continue;
                    }

                    var parentPid = ToInt(obj["ParentProcessId"]);
                    var name = obj["Name"]?.ToString() ?? string.Empty;
                    var commandLine = obj["CommandLine"]?.ToString();
                    var executablePath = obj["ExecutablePath"]?.ToString();
                    var creation = obj["CreationDate"]?.ToString();
                    DateTime? startTime = null;
                    if (!string.IsNullOrWhiteSpace(creation))
                    {
                        try
                        {
                            startTime = ManagementDateTimeConverter.ToDateTime(creation).ToUniversalTime();
                        }
                        catch
                        {
                            startTime = null;
                        }
                    }

                    var owner = GetOwner(obj);

                    map[pid] = new WmiProcessInfo(pid, name, parentPid, commandLine, executablePath, startTime, owner);
                }
            }
            catch
            {
                // WMI may be unavailable or restricted; fall back to basic process data.
            }

            return map;
        }

        private static IReadOnlyDictionary<int, string> GetServiceMap()
        {
            var map = new Dictionary<int, List<string>>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DisplayName, ProcessId FROM Win32_Service WHERE ProcessId != 0");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var pid = ToInt(obj["ProcessId"]);
                    if (pid == 0)
                    {
                        continue;
                    }

                    var name = obj["DisplayName"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = obj["Name"]?.ToString();
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (!map.TryGetValue(pid, out var list))
                    {
                        list = new List<string>();
                        map[pid] = list;
                    }

                    if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(name);
                    }
                }
            }
            catch
            {
                // Ignore service lookup failures.
            }

            return map.ToDictionary(kvp => kvp.Key, kvp => string.Join(", ", kvp.Value));
        }

        private static string? GetOwner(ManagementObject obj)
        {
            try
            {
                var ownerInfo = new string[2];
                var result = obj.InvokeMethod("GetOwner", ownerInfo);
                if (result is uint status && status == 0)
                {
                    var user = ownerInfo[0];
                    var domain = ownerInfo[1];
                    if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(domain))
                    {
                        return $"{domain}\\{user}";
                    }

                    if (!string.IsNullOrWhiteSpace(user))
                    {
                        return user;
                    }
                }
            }
            catch
            {
                // Ignore owner lookup failures.
            }

            return null;
        }

        private static int ToInt(object? value)
        {
            if (value is null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static string ResolveSource(Process process, IReadOnlyDictionary<int, string> serviceMap, out string? serviceName)
        {
            serviceName = null;
            if (serviceMap.TryGetValue(process.Id, out var name))
            {
                serviceName = name;
                return "Service Control Manager (windows_service)";
            }

            var session = SafeGet(() => process.SessionId);
            if (session == 0)
            {
                return "System";
            }

            return "Interactive";
        }

        private double ComputeCpu(Process process, DateTime now)
        {
            TimeSpan totalCpu;
            try
            {
                totalCpu = process.TotalProcessorTime;
            }
            catch
            {
                return 0;
            }

            lock (_lock)
            {
                if (_cpuSamples.TryGetValue(process.Id, out var sample))
                {
                    var deltaCpu = totalCpu - sample.TotalCpu;
                    var elapsed = now - sample.TimestampUtc;
                    var elapsedMs = elapsed.TotalMilliseconds;
                    if (elapsedMs > 0)
                    {
                        var percent = deltaCpu.TotalMilliseconds / (elapsedMs * Environment.ProcessorCount) * 100.0;
                        if (percent < 0)
                        {
                            percent = 0;
                        }

                        _cpuSamples[process.Id] = new CpuSample(totalCpu, now);
                        return percent;
                    }
                }

                _cpuSamples[process.Id] = new CpuSample(totalCpu, now);
            }

            return 0;
        }

        private void CleanupSamples(HashSet<int> alive)
        {
            lock (_lock)
            {
                if (_cpuSamples.Count == 0)
                {
                    return;
                }

                var dead = _cpuSamples.Keys.Where(id => !alive.Contains(id)).ToList();
                foreach (var id in dead)
                {
                    _cpuSamples.Remove(id);
                }
            }
        }

        private static T? SafeGet<T>(Func<T> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return default;
            }
        }
    }
}
