using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PidGui.Models;

namespace PidGui.Services
{
    public sealed class NetworkScannerService
    {
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        public async Task<IReadOnlyList<NetworkConnectionInfo>> GetConnectionsAsync()
        {
            var processMap = BuildProcessMap();
            var results = new List<NetworkConnectionInfo>();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
            }
            catch
            {
                return results;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("Proto", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Active", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var tokens = Whitespace.Split(line);
                if (tokens.Length < 4)
                {
                    continue;
                }

                var proto = tokens[0].ToUpperInvariant();
                if (proto != "TCP" && proto != "UDP")
                {
                    continue;
                }

                var local = tokens[1];
                var remote = tokens[2];
                string? state = null;
                string pidToken;

                if (proto == "TCP")
                {
                    if (tokens.Length < 5)
                    {
                        continue;
                    }

                    state = tokens[3];
                    pidToken = tokens[4];
                }
                else
                {
                    pidToken = tokens[3];
                }

                if (!int.TryParse(pidToken, out var pid))
                {
                    continue;
                }

                ParseEndpoint(local, out var localAddress, out var localPort);
                ParseEndpoint(remote, out var remoteAddress, out var remotePort);

                processMap.TryGetValue(pid, out var name);

                results.Add(new NetworkConnectionInfo(
                    proto,
                    localAddress,
                    localPort,
                    remoteAddress,
                    remotePort,
                    state,
                    pid,
                    name));
            }

            return results;
        }

        private static Dictionary<int, string> BuildProcessMap()
        {
            var map = new Dictionary<int, string>();
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    map[process.Id] = process.ProcessName;
                }
                catch
                {
                    // Skip processes that exit or deny access.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return map;
        }

        private static void ParseEndpoint(string endpoint, out string address, out int port)
        {
            address = endpoint;
            port = 0;

            if (string.IsNullOrWhiteSpace(endpoint) || endpoint == "*")
            {
                return;
            }

            if (endpoint == "*:*")
            {
                address = "*";
                return;
            }

            if (endpoint.StartsWith("[", StringComparison.Ordinal) && endpoint.Contains("]:", StringComparison.Ordinal))
            {
                var closing = endpoint.IndexOf("]:", StringComparison.Ordinal);
                address = endpoint.Substring(1, closing - 1);
                var portToken = endpoint.Substring(closing + 2);
                if (int.TryParse(portToken, out var parsed))
                {
                    port = parsed;
                }

                return;
            }

            var lastColon = endpoint.LastIndexOf(':');
            if (lastColon <= 0)
            {
                address = endpoint;
                return;
            }

            address = endpoint.Substring(0, lastColon);
            var portPart = endpoint.Substring(lastColon + 1);
            if (portPart == "*")
            {
                port = 0;
                return;
            }

            if (int.TryParse(portPart, out var portValue))
            {
                port = portValue;
            }
        }
    }
}
