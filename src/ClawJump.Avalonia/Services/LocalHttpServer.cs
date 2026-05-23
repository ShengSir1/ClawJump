using ClawJump.Avalonia.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace ClawJump.Avalonia.Services;

public class LocalHttpServer
{
    private readonly int _port;
    private IHost? _host;

    public event Action<HookEvent>? OnHookEventReceived;

    public bool IsRunning => _host != null;

    public LocalHttpServer(int port)
    {
        _port = port;
    }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls($"http://127.0.0.1:{_port}");

        var app = builder.Build();

        app.MapGet("/health", () =>
        {
            return Results.Ok(new
            {
                code = 200,
                app = "Claw Jump",
                status = "running",
                port = _port,
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        });

        app.MapPost("/event", async (HttpContext context) =>
        {
            try
            {
                var hookEvent = await JsonSerializer.DeserializeAsync<HookEvent>(
                    context.Request.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (hookEvent == null)
                {
                    return Results.BadRequest(new
                    {
                        code = 400,
                        message = "Invalid event body"
                    });
                }

                OnHookEventReceived?.Invoke(hookEvent);

                return Results.Ok(new
                {
                    code = 200,
                    message = "ok"
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        await app.StartAsync();

        _host = app;
    }

    public async Task StopAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }
    }
}