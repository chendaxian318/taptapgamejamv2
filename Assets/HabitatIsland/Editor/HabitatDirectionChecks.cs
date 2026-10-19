using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HabitatIsland.Editor
{
    /// <summary>Pure movement regressions plus an isolated synchronous check of the live input wiring.</summary>
    public static class HabitatDirectionChecks
    {
        static int count;
        static StringBuilder report;
        static readonly Vector2Int[] Arrows = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        static readonly string[] Labels = { "W / Up", "S / Down", "A / Left", "D / Right" };
        static readonly Vector2Int[,] Expected =
        {
            { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0) },
            { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) },
            { new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(-1, 0) },
            { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) }
        };

        static void Check(bool condition, string label)
        {
            count++;
            if (!condition) throw new InvalidOperationException("Direction check failed: " + label);
            report.AppendLine("PASS " + count + ": " + label);
        }

        static Quaternion CameraRotation(float yaw)
        {
            float radians = yaw * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(radians), Mathf.Tan(48f * Mathf.Deg2Rad), -Mathf.Cos(radians));
            return Quaternion.LookRotation(-offset, Vector3.up);
        }

        static void CheckProjection(Vector2Int step, Vector2Int input, Quaternion cameraRotation, string label)
        {
            Vector3 projected = Quaternion.Inverse(cameraRotation) * new Vector3(step.x, 0f, step.y);
            Vector2 screen = new Vector2(projected.x, projected.y);
            Check(Mathf.Abs(step.x) + Mathf.Abs(step.y) == 1 && Vector2.Dot(screen.normalized, input) > .94f,
                label + " chooses the grid axis closest to the requested screen direction");
        }

        static HabitatState FlatBoard()
        {
            var state = new HabitatState { width = 7, depth = 7, tiles = new Tile[49], playerX = 3, playerZ = 3 };
            for (int i = 0; i < state.tiles.Length; i++) state.tiles[i] = new Tile { height = 1 };
            return state;
        }

        public static string Run()
        {
            count = 0;
            report = new StringBuilder();
            for (int heading = 0; heading < 4; heading++)
            {
                int yaw = 14 + heading * 90;
                Quaternion rotation = CameraRotation(yaw);
                for (int arrow = 0; arrow < Arrows.Length; arrow++)
                {
                    Vector2Int input = Arrows[arrow];
                    Vector2Int step = HabitatGame.ScreenDirectionToGrid(input.x, input.y, rotation);
                    string label = "camera orbit " + yaw + ", " + Labels[arrow];
                    Check(step == Expected[heading, arrow], label + " maps to the expected board neighbour");
                    CheckProjection(step, input, rotation, label);

                    foreach (int turn in new[] { -90, 90 })
                    {
                        Vector2Int turned = HabitatGame.ScreenDirectionToGrid(input.x, input.y, rotation, turn);
                        int nextHeading = (heading + (turn > 0 ? 1 : 3)) % 4;
                        string turnLabel = label + ", same-frame " + (turn > 0 ? "C" : "Q");
                        Check(turned == Expected[nextHeading, arrow], turnLabel + " uses the new camera heading");
                        CheckProjection(turned, input, CameraRotation(yaw + turn), turnLabel);
                    }
                }

                Vector2Int climbStep = HabitatGame.ScreenDirectionToGrid(0, 1, rotation);
                HabitatState climb = FlatBoard();
                int x = climb.playerX + climbStep.x, z = climb.playerZ + climbStep.y;
                Tile crate = HabitatRules.TileAt(climb, x, z);
                crate.height = 2; crate.prop = PropKind.Crate;
                HabitatRules.TileAt(climb, x + climbStep.x, z + climbStep.y).prop = PropKind.Obstacle;
                Check(!HabitatRules.MovePlayer(climb, climbStep.x, climbStep.y),
                    "orbit " + yaw + ": ordinary movement cannot push the blocked two-layer crate");
                Check(!HabitatRules.MovePlayer(climb, climbStep.x, climbStep.y, true) && climb.playerX == 3 && climb.playerZ == 3,
                    "orbit " + yaw + ": ordinary crate rejects a two-layer Shift climb");
                crate.prop = PropKind.ClimbBlock;
                Check(HabitatRules.MovePlayer(climb, climbStep.x, climbStep.y, true) && climb.playerX == x && climb.playerZ == z,
                    "orbit " + yaw + ": climbing block allows the two-layer screen-up climb");
                Check(crate.prop == PropKind.ClimbBlock && climb.facingX == climbStep.x && climb.facingZ == climbStep.y,
                    "orbit " + yaw + ": climbing preserves the block and sets world-facing correctly");
            }

            HabitatState board = FlatBoard();
            Check(HabitatRules.MovePlayer(board, 1, 0) && board.playerX == 4 && board.playerZ == 3,
                "direct MovePlayer east remains a board-coordinate API");
            Check(!HabitatRules.MovePlayer(board, 1, 1) && board.playerX == 4 && board.playerZ == 3,
                "direct MovePlayer still rejects diagonal movement");
            Check(HabitatGame.ScreenDirectionToGrid(0, 0, CameraRotation(90)) == Vector2Int.zero,
                "neutral input remains neutral");
            string result = "PASS: " + count + " camera-relative direction checks";
            report.Insert(0, result + Environment.NewLine);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "direction-checks.txt"), report.ToString());
            return result;
        }

        static void PressAndHandle(HabitatGame game, Keyboard keyboard, Mouse mouse, MethodInfo handler, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            foreach (Key key in keys)
                if (!keyboard[key].isPressed) throw new InvalidOperationException("Input event not delivered for " + key);
            handler.Invoke(game, null);
        }

        public static string RunInputChecks()
        {
            HabitatGame game = HabitatGame.Instance;
            if (!Application.isPlaying || game == null || game.Hud == null || game.View == null || game.View.GameCamera == null)
                throw new InvalidOperationException("Enter Play mode with a ready Habitat game before running input checks.");
            if (game.Hud.ModalOpen || (game.Fishing != null && game.Fishing.IsOpen))
                throw new InvalidOperationException("Close the open modal or fishing panel before running input checks.");

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo handler = typeof(HabitatGame).GetMethod("HandleInput", flags);
            FieldInfo historyField = typeof(HabitatGame).GetField("history", flags);
            FieldInfo nextMoveField = typeof(HabitatGame).GetField("nextMove", flags);
            FieldInfo yawField = typeof(HabitatView).GetField("cameraYaw", flags);
            FieldInfo framingField = typeof(HabitatView).GetField("framingDirty", flags);
            FieldInfo visibilityField = typeof(HabitatView).GetField("nextVisibilityCheck", flags);
            var history = (Stack<HabitatState>)historyField.GetValue(game);
            HabitatState[] savedHistory = history.ToArray();
            HabitatState savedState = game.State;
            int savedActions = game.ActionCount, savedTool = game.tool;
            bool savedPaused = game.paused;
            object savedNextMove = nextMoveField.GetValue(game), savedYaw = yawField.GetValue(game.View);
            object savedFraming = framingField.GetValue(game.View), savedVisibility = visibilityField.GetValue(game.View);
            Transform cameraTransform = game.View.GameCamera.transform;
            Vector3 savedPosition = cameraTransform.position;
            Quaternion savedRotation = cameraTransform.rotation;
            Keyboard originalKeyboard = Keyboard.current, keyboard = null;
            Mouse originalMouse = Mouse.current, mouse = null;
            count = 0; report = new StringBuilder();
            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>("Habitat direction check keyboard");
                mouse = InputSystem.AddDevice<Mouse>("Habitat direction check mouse");
                game.paused = true;
                Key[] keys = { Key.W, Key.S, Key.A, Key.D, Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow };
                for (int heading = 0; heading < 4; heading++)
                {
                    float yaw = 14f + heading * 90f;
                    for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                    {
                        game.State = FlatBoard();
                        yawField.SetValue(game.View, yaw); cameraTransform.rotation = CameraRotation(yaw);
                        nextMoveField.SetValue(game, float.PositiveInfinity);
                        PressAndHandle(game, keyboard, mouse, handler, keys[keyIndex]);
                        Vector2Int expected = Expected[heading, keyIndex % 4];
                        Check(game.State.playerX == 3 + expected.x && game.State.playerZ == 3 + expected.y,
                            "HandleInput orbit " + yaw + ", " + keys[keyIndex] + " moves to the screen-aligned neighbour");
                        Check(game.State.facingX == expected.x && game.State.facingZ == expected.y
                            && game.State.selectedX == 3 + expected.x * 2 && game.State.selectedZ == 3 + expected.y * 2,
                            "HandleInput orbit " + yaw + ", " + keys[keyIndex] + " keeps facing and tool target in front");
                    }
                    foreach (Key turnKey in new[] { Key.Q, Key.C })
                    {
                        game.State = FlatBoard();
                        yawField.SetValue(game.View, yaw); cameraTransform.rotation = CameraRotation(yaw);
                        nextMoveField.SetValue(game, float.PositiveInfinity);
                        PressAndHandle(game, keyboard, mouse, handler, turnKey, Key.UpArrow);
                        int nextHeading = (heading + (turnKey == Key.C ? 1 : 3)) % 4;
                        Vector2Int expected = Expected[nextHeading, 0];
                        Check(game.State.playerX == 3 + expected.x && game.State.playerZ == 3 + expected.y
                            && Mathf.Abs(Mathf.DeltaAngle((float)yawField.GetValue(game.View), 14f + nextHeading * 90f)) < .01f,
                            "HandleInput orbit " + yaw + ": same-frame " + turnKey + " + Up uses the new heading");
                    }
                    foreach (Key shiftKey in new[] { Key.LeftShift, Key.RightShift })
                    {
                        game.State = FlatBoard();
                        yawField.SetValue(game.View, yaw); cameraTransform.rotation = CameraRotation(yaw);
                        nextMoveField.SetValue(game, float.PositiveInfinity);
                        Vector2Int expected = Expected[heading, 0];
                        Tile crate = HabitatRules.TileAt(game.State, 3 + expected.x, 3 + expected.y);
                        crate.height = 2; crate.prop = PropKind.Crate;
                        HabitatRules.TileAt(game.State, 3 + expected.x * 2, 3 + expected.y * 2).prop = PropKind.Obstacle;
                        PressAndHandle(game, keyboard, mouse, handler, shiftKey, Key.W);
                        Check(game.State.playerX == 3 && game.State.playerZ == 3 && crate.prop == PropKind.Crate,
                            "HandleInput orbit " + yaw + ": " + shiftKey + " + W rejects the two-layer ordinary crate");
                        crate.prop = PropKind.ClimbBlock;
                        PressAndHandle(game, keyboard, mouse, handler, shiftKey, Key.W);
                        Check(game.State.playerX == 3 + expected.x && game.State.playerZ == 3 + expected.y && crate.prop == PropKind.ClimbBlock,
                            "HandleInput orbit " + yaw + ": " + shiftKey + " + W climbs the two-layer climbing block");
                    }
                    game.State = FlatBoard();
                    yawField.SetValue(game.View, yaw); cameraTransform.rotation = CameraRotation(yaw);
                    nextMoveField.SetValue(game, float.PositiveInfinity);
                    Vector2Int retreat = Expected[heading, 1];
                    HabitatRules.TileAt(game.State, 3 - retreat.x, 3 - retreat.y).prop = PropKind.Crate;
                    int undoBefore = game.UndoCount, actionsBefore = game.ActionCount;
                    PressAndHandle(game, keyboard, mouse, handler, Key.LeftCtrl, Key.S);
                    Check(game.State.playerX == 3 + retreat.x && game.State.playerZ == 3 + retreat.y && HabitatRules.TileAt(game.State, 3, 3).prop == PropKind.Crate,
                        "HandleInput orbit " + yaw + ": Ctrl+S retreats and pulls the screen-up box");
                    Check(game.State.facingX == -retreat.x && game.State.facingZ == -retreat.y && game.State.selectedX == 3 && game.State.selectedZ == 3,
                        "HandleInput orbit " + yaw + ": pull keeps facing and selection on the moved box");
                    Check(game.UndoCount == undoBefore + 1 && game.ActionCount == actionsBefore + 1,
                        "HandleInput orbit " + yaw + ": pull records exactly one undoable action");
                    game.Undo();
                    Check(game.State.playerX == 3 && game.State.playerZ == 3 && HabitatRules.TileAt(game.State, 3 - retreat.x, 3 - retreat.y).prop == PropKind.Crate,
                        "HandleInput orbit " + yaw + ": undo restores player and box together");
                }
                string result = "PASS: " + count + " live HandleInput direction checks";
                report.Insert(0, result + Environment.NewLine);
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification"));
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "direction-input-checks.txt"), report.ToString());
                return result;
            }
            finally
            {
                game.State = savedState; game.ActionCount = savedActions; game.tool = savedTool; game.paused = savedPaused;
                nextMoveField.SetValue(game, savedNextMove);
                history.Clear();
                for (int i = savedHistory.Length - 1; i >= 0; i--) history.Push(savedHistory[i]);
                cameraTransform.SetPositionAndRotation(savedPosition, savedRotation);
                yawField.SetValue(game.View, savedYaw); framingField.SetValue(game.View, savedFraming); visibilityField.SetValue(game.View, savedVisibility);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
                if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent();
            }
        }
    }
}
