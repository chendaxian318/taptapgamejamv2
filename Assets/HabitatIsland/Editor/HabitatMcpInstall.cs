using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

internal static class HabitatMcpInstall
{
    static AddRequest request;
    static double deadline;
    // Project packages are restored by UPM on first open. This is a manual repair action.
    [MenuItem("Tools/Habitat Island/Install Unity MCP package")]
    static void Install()
    {
        if (request != null && !request.IsCompleted) return;
        if (UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == "com.coplaydev.unity-mcp")) return;
        request = Client.Add("https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0");
        deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update += Poll;
    }
    static void Poll()
    {
        if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Poll;
        if (request.Status == StatusCode.Success) Debug.Log("[HabitatIsland] Unity MCP installed: " + request.Result.version);
        else Debug.LogWarning("[HabitatIsland] MCP install: " + request.Error?.message);
    }
}
