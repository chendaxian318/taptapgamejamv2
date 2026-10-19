using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using HabitatIsland;
using UnityEngine;

public static class BackBasketPreview
{
    public static async Task<string> Run()
    {
        var game = HabitatGame.Instance;
        if (!game || game.Busy) throw new Exception("Requires idle main game.");
        var visual = game.View.PlayerVisual;
        if (!visual.BackBasketRoot) throw new Exception("No back basket.");
        var camera = game.View.GameCamera;
        var facing = (Transform)typeof(HabitatPlayerVisual).GetField("facingRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(visual);
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        float size = camera.orthographicSize; bool enabled = game.enabled;
        string folder = Path.GetFullPath("Logs/back-basket-20261016"); Directory.CreateDirectory(folder);
        try
        {
            game.enabled = false;
            float[] angles = { 0f, 90f, -35f, 180f };
            string[] names = { "fitting-back", "fitting-side", "fitting-three-quarter", "fitting-front" };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 target = visual.transform.position + Vector3.up * .69f;
                Quaternion orbit = facing.rotation * Quaternion.Euler(15f, angles[i], 0f);
                camera.transform.SetPositionAndRotation(target - orbit * Vector3.forward * 5f, orbit);
                camera.orthographicSize = .95f;
                await Task.Delay(100);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, names[i] + ".png"));
                await Task.Delay(100);
            }
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size; game.enabled = enabled; }
        return "Captured four real-camera basket fitting views; original camera and Game.Update restored.";
    }
}
