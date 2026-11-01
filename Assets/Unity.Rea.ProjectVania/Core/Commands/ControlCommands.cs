namespace Unity.Rea.ProjectVania.Core.Commands
{
    // The commands the controls issue (RECONSTRUCTION 3), one per InputManager action of the original. The input
    // router decides when each is issued; the world applies them in the order issued, within the step, so a toggle
    // reads the state left by the command before it exactly as the original's handlers did. Commands whose system is
    // built by a later story (player physics A6, camera A9, menu A10, console A11, HUD A12, editor A14) are accepted
    // and applied by that story.

    /// <summary>Run the driven character left (-1) or right (+1) this step (<c>Command_Move</c>).</summary>
    public readonly struct MoveCharacter : ICommand
    {
        public PlayerId Player { get; }
        public int Direction { get; }
        public MoveCharacter(PlayerId player, int direction) { Player = player; Direction = direction; }
    }

    /// <summary>No direction held this step: stop moving (<c>Command_Stand</c>, sets movement to 0).</summary>
    public readonly struct StandCharacter : ICommand
    {
        public PlayerId Player { get; }
        public StandCharacter(PlayerId player) { Player = player; }
    }

    /// <summary>Down held alone this step (<c>Command_Kneel</c>; movement is kept, which is what makes a slide).</summary>
    public readonly struct KneelCharacter : ICommand
    {
        public PlayerId Player { get; }
        public KneelCharacter(PlayerId player) { Player = player; }
    }

    /// <summary>Jump (<c>Command_Jump</c>; ignored while the character is already jumping).</summary>
    public readonly struct JumpCharacter : ICommand
    {
        public PlayerId Player { get; }
        public JumpCharacter(PlayerId player) { Player = player; }
    }

    /// <summary>Drive and follow the previous (-1) or next (+1) character, wrapping (<c>CyclePlayerLeft/Right</c>).</summary>
    public readonly struct CycleCharacter : ICommand
    {
        public PlayerId Player { get; }
        public int Direction { get; }
        public CycleCharacter(PlayerId player, int direction) { Player = player; Direction = direction; }
    }

    /// <summary>Open the main menu, or close it to Play from the menu (<c>Action_ToggleMainMenu</c>).</summary>
    public readonly struct ToggleMainMenu : ICommand
    {
        public PlayerId Player { get; }
        public ToggleMainMenu(PlayerId player) { Player = player; }
    }

    /// <summary>Slide the console down or up (<c>Command_ToggleConsole</c>).</summary>
    public readonly struct ToggleConsole : ICommand
    {
        public PlayerId Player { get; }
        public ToggleConsole(PlayerId player) { Player = player; }
    }

    /// <summary>Enter the map editor, or leave it to Play from the editor (<c>ToggleMapEditor</c>).</summary>
    public readonly struct ToggleMapEditor : ICommand
    {
        public PlayerId Player { get; }
        public ToggleMapEditor(PlayerId player) { Player = player; }
    }

    /// <summary>R: back to Play, camera to LoosePlayer, every character reset (<c>Reset</c>).</summary>
    public readonly struct ResetGame : ICommand
    {
        public PlayerId Player { get; }
        public ResetGame(PlayerId player) { Player = player; }
    }

    /// <summary>Switch between windowed and fullscreen (<c>ToggleFullScreen</c>).</summary>
    public readonly struct ToggleFullscreen : ICommand
    {
        public PlayerId Player { get; }
        public ToggleFullscreen(PlayerId player) { Player = player; }
    }

    /// <summary>Slide the quick bar in or out (<c>QuickBar.ToggleQuickBar</c>).</summary>
    public readonly struct ToggleQuickBar : ICommand
    {
        public PlayerId Player { get; }
        public ToggleQuickBar(PlayerId player) { Player = player; }
    }

    /// <summary>Free -> LockPlayer -> LoosePlayer -> LooseCursor -> Free (<c>CycleCameraMode</c>).</summary>
    public readonly struct CycleCameraMode : ICommand
    {
        public PlayerId Player { get; }
        public CycleCameraMode(PlayerId player) { Player = player; }
    }

    public enum CameraNudge
    {
        ScrollLeft,
        ScrollRight,
        ScrollUp,
        ScrollDown,
        ZoomIn,
        ZoomOut,
        RotateLeft,
        RotateRight,
    }

    /// <summary>One step of a camera key or one wheel notch; the camera applies camera.json's amount.</summary>
    public readonly struct NudgeCamera : ICommand
    {
        public PlayerId Player { get; }
        public CameraNudge Nudge { get; }
        public NudgeCamera(PlayerId player, CameraNudge nudge) { Player = player; Nudge = nudge; }
    }

    public enum MenuInput
    {
        /// <summary>To the parent level, or close the menu from the root.</summary>
        Back,
        Select,
        Up,
        Down,
    }

    public readonly struct NavigateMenu : ICommand
    {
        public PlayerId Player { get; }
        public MenuInput Input { get; }
        public NavigateMenu(PlayerId player, MenuInput input) { Player = player; Input = input; }
    }

    public enum ConsoleInput
    {
        /// <summary>Enter: add the line to history, run it, clear it.</summary>
        Submit,
        /// <summary>Delete: clear the line.</summary>
        Clear,
        /// <summary>Backspace: remove the last character.</summary>
        Backspace,
        /// <summary>Up: recall the next history line.</summary>
        Recall,
        /// <summary>Append <see cref="EditConsole.Text"/>.</summary>
        Append,
    }

    public readonly struct EditConsole : ICommand
    {
        public PlayerId Player { get; }
        public ConsoleInput Input { get; }
        public string Text { get; }
        public EditConsole(PlayerId player, ConsoleInput input, string text = null) { Player = player; Input = input; Text = text; }
    }

    public enum EditorCursorMode
    {
        /// <summary>The cursor is the camera centre; the mouse scrolls the camera.</summary>
        World,
        /// <summary>Space held: the cursor follows the mouse to pick a palette tile.</summary>
        Screen,
    }

    /// <summary>Space held or not, every editor step.</summary>
    public readonly struct SetEditorCursor : ICommand
    {
        public PlayerId Player { get; }
        public EditorCursorMode Mode { get; }
        public SetEditorCursor(PlayerId player, EditorCursorMode mode) { Player = player; Mode = mode; }
    }

    /// <summary>
    /// Where the mouse is this editor step and which of its buttons are held (<c>Mouse.GetState()</c>, read by the
    /// editor's update after the camera has moved): on the 800 x 600 reference screen, pixels, y down; and whether the
    /// window has the focus, without which the editor does not warp the mouse back to the screen centre.
    /// </summary>
    public readonly struct PointEditor : ICommand
    {
        public PlayerId Player { get; }
        public float X { get; }
        public float Y { get; }
        public bool Left { get; }
        public bool Right { get; }
        public bool WindowActive { get; }

        public PointEditor(PlayerId player, float x, float y, bool left, bool right, bool windowActive = true)
        {
            Player = player;
            X = x;
            Y = y;
            Left = left;
            Right = right;
            WindowActive = windowActive;
        }
    }

    /// <summary>
    /// An editor direction press: step the palette's selected tile (unless Ctrl) and move the cursor a cell, scrolling
    /// the camera (unless Shift). Exactly one of <see cref="Dx"/>, <see cref="Dy"/> is non-zero.
    /// </summary>
    public readonly struct StepEditor : ICommand
    {
        public PlayerId Player { get; }
        public int Dx { get; }
        public int Dy { get; }
        public bool SelectTile { get; }
        public bool MoveCell { get; }

        public StepEditor(PlayerId player, int dx, int dy, bool selectTile, bool moveCell)
        {
            Player = player;
            Dx = dx;
            Dy = dy;
            SelectTile = selectTile;
            MoveCell = moveCell;
        }
    }

    /// <summary>A pad connected this step: the HUD shows "XBox 360 Gamepad Connected" (Play and MapEditor only).</summary>
    public readonly struct AnnounceGamepad : ICommand
    {
        public PlayerId Player { get; }
        public AnnounceGamepad(PlayerId player) { Player = player; }
    }

    /// <summary>
    /// The window mode should flip (presentation applies it). <see cref="Player"/> is who asked: online (U.VAN-T31) only
    /// that player's window flips; in local play it is always the one driver.
    /// </summary>
    public readonly struct FullscreenToggled : IGameEvent
    {
        public long Tick { get; }
        public PlayerId Player { get; }
        public FullscreenToggled(long tick, PlayerId player) { Tick = tick; Player = player; }
    }

    /// <summary>The console was asked to slide (presentation plays its cue).</summary>
    public readonly struct ConsoleToggled : IGameEvent
    {
        public long Tick { get; }
        public bool Opening { get; }
        public ConsoleToggled(long tick, bool opening) { Tick = tick; Opening = opening; }
    }
}
