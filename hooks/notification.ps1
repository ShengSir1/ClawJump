# hooks/notification.ps1

$body = @{
    type = "notification"
    message = "Claude Code needs attention"
    time = (Get-Date).ToString("s")
} | ConvertTo-Json

try {
    Invoke-RestMethod `
        -Uri "http://127.0.0.1:47653/event" `
        -Method Post `
        -Body $body `
        -ContentType "application/json"
} catch {
    Write-Host "claw-jump is not running"
}