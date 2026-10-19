// Portable, opt-in connection to an already-started local MCP server.
// The package's own setup window owns Python/uv installation and server startup.
using System;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

internal static class TidelightMCPBootstrap
{
    private const string DefaultBaseUrl = "http://127.0.0.1:8088";
    private static bool busy;

    [MenuItem("Tools/Habitat Island/Connect Unity MCP")]
    [MenuItem("Tools/Tidelight MVP/Connect Unity MCP")]
    private static async void ConnectMenu()
    {
        if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string baseUrl = Environment.GetEnvironmentVariable("HABITAT_MCP_URL");
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = DefaultBaseUrl;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri endpoint) ||
            !endpoint.IsLoopback || endpoint.Scheme != Uri.UriSchemeHttp || endpoint.AbsolutePath != "/")
        {
            Debug.LogWarning("[Habitat MCP] HABITAT_MCP_URL must be a local HTTP base URL, e.g. http://127.0.0.1:8088 (without /mcp).");
            return;
        }
        Type locator = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor", false);
        Type cacheType = Type.GetType("MCPForUnity.Editor.Services.EditorConfigurationCache, MCPForUnity.Editor", false);
        if (locator == null || cacheType == null)
        {
            Debug.LogWarning("[Habitat MCP] Resolve the project's MCP package first. See Docs/工具连接与验证.txt.");
            return;
        }
        busy = true;
        try
        {
            if (!await PortReady(endpoint))
            {
                Debug.LogWarning("[Habitat MCP] Start the local HTTP server from Window > MCP for Unity, then reconnect. No workstation-specific Python path is required.");
                EditorApplication.ExecuteMenuItem("Window/MCP for Unity/Local Setup Window");
                return;
            }
            var config = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            cacheType.GetMethod("SetUseHttpTransport").Invoke(config, new object[] { true });
            cacheType.GetMethod("SetHttpTransportScope").Invoke(config, new object[] { "local" });
            cacheType.GetMethod("SetHttpBaseUrl").Invoke(config, new object[] { baseUrl.TrimEnd('/') });
            object bridge = locator.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            bool running = (bool)bridge.GetType().GetProperty("IsRunning").GetValue(bridge);
            bool connected = running || await (Task<bool>)bridge.GetType().GetMethod("StartAsync").Invoke(bridge, null);
            if (connected) Debug.Log("[Habitat MCP] Connected to " + baseUrl);
            else Debug.LogWarning("[Habitat MCP] Bridge did not connect. Check the MCP for Unity window.");
        }
        catch (Exception e) { Debug.LogWarning("[Habitat MCP] " + e.GetBaseException().Message); }
        finally { busy = false; }
    }

    private static async Task<bool> PortReady(Uri endpoint)
    {
        try
        {
            using (var socket = new TcpClient())
            {
                Task connect = socket.ConnectAsync(endpoint.Host, endpoint.Port);
                if (await Task.WhenAny(connect, Task.Delay(1500)) != connect) return false;
                await connect;
                return socket.Connected;
            }
        }
        catch { return false; }
    }
}
