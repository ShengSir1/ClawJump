using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace ClawJump.Avalonia.Services;

public record HookMergeResult(
    string SettingsPath,
    string? BackupPath,
    string HookScriptPath);

public record HookCleanupResult(
    string SettingsPath,
    string? BackupPath,
    int RemovedCount,
    bool SettingsFileExists,
    bool Changed);

public static class HookScriptService
{
    private static readonly string[] HookEventNames =
    [
        "Stop",
        "Notification",
        "UserPromptSubmit"
    ];

    public static string HookDirectory =>
        Path.Combine(ConfigService.ConfigDirectory, "hooks");

    public static string HookScriptPath =>
        Path.Combine(HookDirectory, "claw-jump-hook.ps1");

    public static string ClaudeSettingsSnippetPath =>
        Path.Combine(ConfigService.ConfigDirectory, "claude-settings-snippet.json");

    public static string ClaudeConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude");

    public static string ClaudeSettingsPath =>
        Path.Combine(ClaudeConfigDirectory, "settings.json");

    public static void Generate(int port)
    {
        Directory.CreateDirectory(HookDirectory);
        Directory.CreateDirectory(ConfigService.ConfigDirectory);

        File.WriteAllText(
            HookScriptPath,
            BuildPowerShellScript(port),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        File.WriteAllText(
            ClaudeSettingsSnippetPath,
            BuildClaudeSettingsSnippet(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static HookMergeResult MergeToClaudeSettings(int port)
    {
        Generate(port);

        Directory.CreateDirectory(ClaudeConfigDirectory);

        string? backupPath = null;

        if (File.Exists(ClaudeSettingsPath))
        {
            backupPath = Path.Combine(
                ClaudeConfigDirectory,
                $"settings.json.bak-{DateTime.Now:yyyyMMddHHmmss}");

            File.Copy(ClaudeSettingsPath, backupPath, overwrite: false);
        }

        var root = LoadClaudeSettingsRoot();

        var hooksObj = GetOrCreateObject(root, "hooks");

        foreach (var eventName in HookEventNames)
        {
            UpsertHook(
                hooksObj,
                eventName,
                BuildCommand(eventName));
        }

        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });

        File.WriteAllText(
            ClaudeSettingsPath,
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new HookMergeResult(
            ClaudeSettingsPath,
            backupPath,
            HookScriptPath);
    }

    public static HookCleanupResult CleanupClaudeSettings(bool createBackup = true)
    {
        if (!File.Exists(ClaudeSettingsPath))
        {
            return new HookCleanupResult(
                ClaudeSettingsPath,
                null,
                0,
                SettingsFileExists: false,
                Changed: false);
        }

        var root = LoadClaudeSettingsRoot();

        if (root["hooks"] is not JsonObject hooksObj)
        {
            return new HookCleanupResult(
                ClaudeSettingsPath,
                null,
                0,
                SettingsFileExists: true,
                Changed: false);
        }

        var removedCount = 0;

        foreach (var eventName in HookEventNames)
        {
            if (hooksObj[eventName] is not JsonArray eventArray)
            {
                continue;
            }

            for (var i = eventArray.Count - 1; i >= 0; i--)
            {
                if (eventArray[i] is not JsonObject matcherItem || matcherItem["hooks"] is not JsonArray innerHooks)
                {
                    continue;
                }

                var beforeCount = innerHooks.Count;
                RemoveOldClawJumpHooks(innerHooks);
                removedCount += beforeCount - innerHooks.Count;

                var matcher = matcherItem["matcher"]?.GetValue<string>() ?? "";

                if (matcher == "" && innerHooks.Count == 0)
                {
                    eventArray.RemoveAt(i);
                }
            }

            if (eventArray.Count == 0)
            {
                hooksObj.Remove(eventName);
            }
        }

        if (removedCount == 0)
        {
            return new HookCleanupResult(
                ClaudeSettingsPath,
                null,
                0,
                SettingsFileExists: true,
                Changed: false);
        }

        if (hooksObj.Count == 0)
        {
            root.Remove("hooks");
        }

        string? backupPath = null;

        if (createBackup)
        {
            backupPath = Path.Combine(
                ClaudeConfigDirectory,
                $"settings.json.bak-{DateTime.Now:yyyyMMddHHmmss}");

            File.Copy(ClaudeSettingsPath, backupPath, overwrite: false);
        }

        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });

        File.WriteAllText(
            ClaudeSettingsPath,
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new HookCleanupResult(
            ClaudeSettingsPath,
            backupPath,
            removedCount,
            SettingsFileExists: true,
            Changed: true);
    }

    public static void OpenClaudeConfigDirectory()
    {
        Directory.CreateDirectory(ClaudeConfigDirectory);

        Process.Start(new ProcessStartInfo
        {
            FileName = ClaudeConfigDirectory,
            UseShellExecute = true
        });
    }

    public static void OpenGeneratedHookDirectory()
    {
        Directory.CreateDirectory(ConfigService.ConfigDirectory);

        Process.Start(new ProcessStartInfo
        {
            FileName = ConfigService.ConfigDirectory,
            UseShellExecute = true
        });
    }

    public static void OpenClaudeSettingsFile()
    {
        Directory.CreateDirectory(ClaudeConfigDirectory);

        if (!File.Exists(ClaudeSettingsPath))
        {
            File.WriteAllText(
                ClaudeSettingsPath,
                "{}",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = ClaudeSettingsPath,
            UseShellExecute = true
        });
    }

    private static JsonObject LoadClaudeSettingsRoot()
    {
        if (!File.Exists(ClaudeSettingsPath))
        {
            return new JsonObject();
        }

        var json = File.ReadAllText(ClaudeSettingsPath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        var node = JsonNode.Parse(json);

        if (node is not JsonObject root)
        {
            throw new InvalidOperationException("Claude settings.json 根节点不是 JSON 对象，无法自动合并。");
        }

        return root;
    }

    private static JsonObject GetOrCreateObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] == null)
        {
            var obj = new JsonObject();
            root[propertyName] = obj;
            return obj;
        }

        if (root[propertyName] is JsonObject existingObj)
        {
            return existingObj;
        }

        throw new InvalidOperationException($"{propertyName} 不是 JSON 对象，无法自动合并。");
    }

    private static void UpsertHook(JsonObject hooksObj, string eventName, string command)
    {
        JsonArray eventArray;

        if (hooksObj[eventName] == null)
        {
            eventArray = new JsonArray();
            hooksObj[eventName] = eventArray;
        }
        else if (hooksObj[eventName] is JsonArray existingArray)
        {
            eventArray = existingArray;
        }
        else
        {
            throw new InvalidOperationException($"hooks.{eventName} 不是数组，无法自动合并。");
        }

        var matcherItem = FindEmptyMatcherItem(eventArray);

        if (matcherItem == null)
        {
            matcherItem = new JsonObject
            {
                ["matcher"] = "",
                ["hooks"] = new JsonArray()
            };

            eventArray.Add(matcherItem);
        }

        JsonArray innerHooks;

        if (matcherItem["hooks"] == null)
        {
            innerHooks = new JsonArray();
            matcherItem["hooks"] = innerHooks;
        }
        else if (matcherItem["hooks"] is JsonArray existingInnerHooks)
        {
            innerHooks = existingInnerHooks;
        }
        else
        {
            throw new InvalidOperationException($"hooks.{eventName}[].hooks 不是数组，无法自动合并。");
        }

        RemoveOldClawJumpHooks(innerHooks);

        innerHooks.Add(new JsonObject
        {
            ["type"] = "command",
            ["command"] = command
        });
    }

    private static JsonObject? FindEmptyMatcherItem(JsonArray eventArray)
    {
        foreach (var item in eventArray)
        {
            if (item is not JsonObject obj)
            {
                continue;
            }

            var matcher = obj["matcher"]?.GetValue<string>() ?? "";

            if (matcher == "")
            {
                return obj;
            }
        }

        return null;
    }

    private static void RemoveOldClawJumpHooks(JsonArray innerHooks)
    {
        for (var i = innerHooks.Count - 1; i >= 0; i--)
        {
            if (innerHooks[i] is not JsonObject hookObj)
            {
                continue;
            }

            var command = hookObj["command"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            if (command.Contains("claw-jump-hook.ps1", StringComparison.OrdinalIgnoreCase))
            {
                innerHooks.RemoveAt(i);
            }
        }
    }

    private static string BuildCommand(string eventType)
    {
        return $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{HookScriptPath}\" {eventType}";
    }

    private static string BuildPowerShellScript(int port)
    {
        return $$"""
param(
    [string]$EventType = "unknown"
)

$ErrorActionPreference = "SilentlyContinue"

try {
    $stdinJson = ""

    if ([Console]::IsInputRedirected) {
        $stdinJson = [Console]::In.ReadToEnd()
    }

    $body = @{
        type = $EventType
        message = "Claude Code hook triggered: $EventType"
        source = "claude-code"
        time = (Get-Date).ToString("s")
        rawInput = $stdinJson
    } | ConvertTo-Json -Depth 10

    Invoke-RestMethod `
        -Uri "http://127.0.0.1:{{port}}/event" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -TimeoutSec 2 | Out-Null

    exit 0
}
catch {
    # 不要阻塞 Claude Code 主流程
    exit 0
}
""";
    }

    private static string BuildClaudeSettingsSnippet()
    {
        var settings = new
        {
            hooks = new
            {
                Stop = new object[]
                {
                    new
                    {
                        matcher = "",
                        hooks = new object[]
                        {
                            new
                            {
                                type = "command",
                                command = BuildCommand("Stop")
                            }
                        }
                    }
                },
                Notification = new object[]
                {
                    new
                    {
                        matcher = "",
                        hooks = new object[]
                        {
                            new
                            {
                                type = "command",
                                command = BuildCommand("Notification")
                            }
                        }
                    }
                },
                UserPromptSubmit = new object[]
                {
                    new
                    {
                        matcher = "",
                        hooks = new object[]
                        {
                            new
                            {
                                type = "command",
                                command = BuildCommand("UserPromptSubmit")
                            }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            });
    }
}