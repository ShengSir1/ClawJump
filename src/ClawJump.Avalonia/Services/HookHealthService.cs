using ClawJump.Avalonia.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClawJump.Avalonia.Services;

public static class HookHealthService
{
    private static readonly string[] RequiredHookEvents =
    [
        "Stop",
        "Notification",
        "UserPromptSubmit"
    ];

    public static async Task<HookHealthStatus> CheckAsync(
        int port,
        bool serverObjectRunning,
        CancellationToken cancellationToken = default)
    {
        var items = new List<HealthCheckItem>();
        var endpointOk = await CheckServerAsync(port, serverObjectRunning, items, cancellationToken);
        var hookScriptExists = CheckHookScript(port, items, out var hookScriptPortMatches);
        var settingsExists = CheckClaudeSettings(
            items,
            out var settingsParseable,
            out var stopHookConfigured,
            out var notificationHookConfigured,
            out var userPromptSubmitHookConfigured);

        return new HookHealthStatus(
            DateTime.Now,
            port,
            serverObjectRunning,
            endpointOk,
            hookScriptExists,
            hookScriptPortMatches,
            settingsExists,
            settingsParseable,
            stopHookConfigured,
            notificationHookConfigured,
            userPromptSubmitHookConfigured,
            items);
    }

    private static async Task<bool> CheckServerAsync(
        int port,
        bool serverObjectRunning,
        List<HealthCheckItem> items,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        try
        {
            using var response = await httpClient.GetAsync(
                $"http://127.0.0.1:{port}/health",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                items.Add(new HealthCheckItem(
                    "本地服务",
                    HealthSeverity.Error,
                    $"/health 返回 HTTP {(int)response.StatusCode}。"));
                return false;
            }

            items.Add(new HealthCheckItem(
                "本地服务",
                serverObjectRunning ? HealthSeverity.Ok : HealthSeverity.Warning,
                serverObjectRunning
                    ? $"本地服务运行中，端口 {port} 可访问。"
                    : $"端口 {port} 可访问，但当前进程未记录服务运行状态。"));
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            items.Add(new HealthCheckItem(
                "本地服务",
                HealthSeverity.Error,
                serverObjectRunning
                    ? $"本地服务对象存在，但端口 {port} 不可访问。"
                    : $"本地服务未运行或端口 {port} 不可访问。"));
            return false;
        }
    }

    private static bool CheckHookScript(
        int port,
        List<HealthCheckItem> items,
        out bool portMatches)
    {
        portMatches = false;

        if (!File.Exists(HookScriptService.HookScriptPath))
        {
            items.Add(new HealthCheckItem(
                "Hook 脚本",
                HealthSeverity.Warning,
                "Hook 脚本不存在。"));
            return false;
        }

        var script = File.ReadAllText(HookScriptService.HookScriptPath);
        portMatches = script.Contains($"http://127.0.0.1:{port}/event", StringComparison.OrdinalIgnoreCase);

        items.Add(new HealthCheckItem(
            "Hook 脚本",
            portMatches ? HealthSeverity.Ok : HealthSeverity.Warning,
            portMatches
                ? "Hook 脚本存在，端口匹配。"
                : "Hook 脚本存在，但端口与当前配置不匹配。"));

        return true;
    }

    private static bool CheckClaudeSettings(
        List<HealthCheckItem> items,
        out bool parseable,
        out bool stopHookConfigured,
        out bool notificationHookConfigured,
        out bool userPromptSubmitHookConfigured)
    {
        parseable = false;
        stopHookConfigured = false;
        notificationHookConfigured = false;
        userPromptSubmitHookConfigured = false;

        if (!File.Exists(HookScriptService.ClaudeSettingsPath))
        {
            items.Add(new HealthCheckItem(
                "Claude Hook",
                HealthSeverity.Warning,
                "Claude settings.json 不存在。"));
            return false;
        }

        var json = File.ReadAllText(HookScriptService.ClaudeSettingsPath);

        if (string.IsNullOrWhiteSpace(json))
        {
            parseable = true;
            items.Add(new HealthCheckItem(
                "Claude Hook",
                HealthSeverity.Warning,
                "Claude settings.json 为空。"));
            return true;
        }

        JsonObject root;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject parsedRoot)
            {
                items.Add(new HealthCheckItem(
                    "Claude Hook",
                    HealthSeverity.Error,
                    "Claude settings.json 根节点不是 JSON 对象。"));
                return true;
            }

            root = parsedRoot;
            parseable = true;
        }
        catch (JsonException ex)
        {
            items.Add(new HealthCheckItem(
                "Claude Hook",
                HealthSeverity.Error,
                $"Claude settings.json 解析失败：{ex.Message}"));
            return true;
        }

        if (root["hooks"] is not JsonObject hooksObj)
        {
            items.Add(new HealthCheckItem(
                "Claude Hook",
                HealthSeverity.Warning,
                "Claude settings.json 中未找到 hooks 配置。"));
            return true;
        }

        stopHookConfigured = HasClawJumpHook(hooksObj, "Stop");
        notificationHookConfigured = HasClawJumpHook(hooksObj, "Notification");
        userPromptSubmitHookConfigured = HasClawJumpHook(hooksObj, "UserPromptSubmit");

        var configuredCount = new[]
        {
            stopHookConfigured,
            notificationHookConfigured,
            userPromptSubmitHookConfigured
        }.Count(configured => configured);

        if (configuredCount == RequiredHookEvents.Length)
        {
            items.Add(new HealthCheckItem(
                "Claude Hook",
                HealthSeverity.Ok,
                "Claude Hook 配置完整。"));
            return true;
        }

        var missingEvents = RequiredHookEvents
            .Where(eventName => !HasClawJumpHook(hooksObj, eventName));

        items.Add(new HealthCheckItem(
            "Claude Hook",
            HealthSeverity.Warning,
            $"Claude Hook 缺少配置：{string.Join("、", missingEvents)}。"));
        return true;
    }

    private static bool HasClawJumpHook(JsonObject hooksObj, string eventName)
    {
        if (hooksObj[eventName] is not JsonArray eventArray)
        {
            return false;
        }

        foreach (var command in GetCommandStrings(eventArray))
        {
            if (command.Contains("claw-jump-hook.ps1", StringComparison.OrdinalIgnoreCase) &&
                command.Contains(eventName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetCommandStrings(JsonArray eventArray)
    {
        foreach (var item in eventArray)
        {
            if (item is not JsonObject matcherObj || matcherObj["hooks"] is not JsonArray hooksArray)
            {
                continue;
            }

            foreach (var hookItem in hooksArray)
            {
                if (hookItem is JsonObject hookObj && hookObj["command"]?.GetValue<string>() is { } command)
                {
                    yield return command;
                }
            }
        }
    }
}
