using System.Net;
namespace CodeTurtleEngine.Worker;

internal static class WorkerProbe
{
    public static async Task<object> RunAsync(string hash)
    {
        bool writeBlocked = false, networkBlocked = false;
        try { await File.WriteAllTextAsync("/outside-workspace-canary", "harmless"); }
        catch (UnauthorizedAccessException) { writeBlocked = true; }
        catch (IOException) { writeBlocked = true; }
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(1) };
        try { await client.GetAsync("http://192.0.2.1/"); }
        catch (HttpRequestException) { networkBlocked = true; }
        catch (TaskCanceledException) { networkBlocked = true; }
        var status = await File.ReadAllTextAsync("/proc/self/status");
        return new { NoNewPrivileges = status.Contains("NoNewPrivs:\t1"),
            SeccompEnforced = status.Contains("Seccomp:\t2"),
            CapabilitiesDropped = status.Contains("CapEff:\t0000000000000000"),
            CpuMax = await File.ReadAllTextAsync("/sys/fs/cgroup/cpu.max"), ArchiveSha256 = hash, WriteBlocked = writeBlocked, NetworkBlocked = networkBlocked,
            CanaryAbsent = Environment.GetEnvironmentVariable("TURTLE_HOST_CANARY") is null,
            CredentialsAbsent = new[] { "GH_TOKEN", "GITHUB_TOKEN", "TURTLE_LLM_API_KEY", "AWS_ACCESS_KEY_ID" }
                .All(name => Environment.GetEnvironmentVariable(name) is null),
            MemoryMax = await File.ReadAllTextAsync("/sys/fs/cgroup/memory.max"),
            PidsMax = await File.ReadAllTextAsync("/sys/fs/cgroup/pids.max") };
    }
    public static void ExhaustMemory()
    {
        var allocations = new List<byte[]>();
        while (true)
        {
            var bytes = new byte[8 * 1024 * 1024];
            for (var offset = 0; offset < bytes.Length; offset += 4096) bytes[offset] = 1;
            allocations.Add(bytes);
        }
    }
}
