using System;
using System.Globalization;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Gui
{
    /// <summary>
    /// <c>GuiManager.Draw</c>'s debug text (RECONSTRUCTION 9.1), written every frame in every state, built from the world
    /// exactly as the original concatenated it: the driven character's name, position and cell at (100, 100); the camera's
    /// mode and position at (300, 100); the GUI cursor's position at (600, 100); "Coords:" and the editor's selected cell
    /// at (700, 100); the frame rate at (W - 100, 10). Numbers are written as .NET 3.5 wrote a float (seven significant
    /// digits), not as today's shortest round-trip form.
    /// </summary>
    public static class HudReadout
    {
        /// <summary>
        /// What the original's readout always showed: <c>1 / (float)ElapsedGameTime.TotalSeconds</c> with XNA's fixed step
        /// of 166667 ticks, 59.99988.
        /// </summary>
        public static readonly float OriginalFrameRate = 1f / (float)(FixedStepper.XnaTargetElapsedTicks / 10_000_000.0);

        /// <summary><c>float.ToString()</c> under .NET Framework: "G7", with no negative zero.</summary>
        public static string Number(float value)
        {
            if (value == 0f)
            {
                return "0";
            }

            return value.ToString("G7", CultureInfo.InvariantCulture);
        }

        /// <summary><c>Name + "\n" + X + ", " + Y + "\n" + cellX + ", " + cellY</c> of the character <paramref name="player"/> drives; empty if none.</summary>
        public static string Player(GameWorld world, PlayerId player)
        {
            var driven = world?.Driven(player);
            if (driven == null)
            {
                return string.Empty;
            }

            var cell = world.Map != null ? world.Map.GetGridCoordinates(driven.Position) : new CellIndex(0, 0);
            return driven.Name + "\n" + Number(driven.Position.X) + ", " + Number(driven.Position.Y) + "\n" + cell.X + ", " + cell.Y;
        }

        /// <summary><c>"Mode: " + Camera.Mode + "\n" + X + ", " + Y</c>, the camera's (negative) position; empty without a camera.</summary>
        public static string Camera(GameWorld world, PlayerId player, HudReadoutData data)
        {
            var camera = world?.Camera(player);
            if (camera == null)
            {
                return string.Empty;
            }

            return data.ModePrefix + camera.Mode + "\n" + Number(camera.Position.X) + ", " + Number(camera.Position.Y);
        }

        /// <summary><c>cursor.Position.X + ", " + cursor.Position.Y</c>.</summary>
        public static string Cursor(float x, float y) => Number(x) + ", " + Number(y);

        /// <summary>
        /// <c>"Coords:" + MapEditor.SelectedCell</c>: the cell under the editor's cursor, which the editor sets only on its
        /// World-mode steps, so it keeps the last cell after the editor closes (or while Space is held) and is (0, 0) before
        /// the editor's first step.
        /// </summary>
        public static string Cell(GameWorld world, PlayerId player, HudReadoutData data)
        {
            var cell = world?.Editor(player)?.SelectedCell ?? new CellIndex(0, 0);
            return data.CellPrefix + cell.X + ", " + cell.Y;
        }

        /// <summary><c>fps.ToString()</c>.</summary>
        public static string FrameRate(float fps) => Number(fps);

        /// <summary>
        /// The GUI cursor in Screen mode (<c>Cursor.Update</c>): the mouse, kept inside (0, 0) .. (W - width, H - height)
        /// of its 13 x 18 picture, in the original's order (left, right, top, bottom).
        /// </summary>
        public static (float X, float Y) ClampCursor(float mouseX, float mouseY, HudData data)
        {
            var x = mouseX;
            var y = mouseY;
            if (x < 0) x = 0;
            if (x > data.Screen.X - data.Readout.CursorSize.X) x = data.Screen.X - data.Readout.CursorSize.X;
            if (y < 0) y = 0;
            if (y > data.Screen.Y - data.Readout.CursorSize.Y) y = data.Screen.Y - data.Readout.CursorSize.Y;
            return (x, y);
        }
    }
}
