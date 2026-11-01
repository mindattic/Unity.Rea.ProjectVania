using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Characters;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Editing;
using Unity.Rea.ProjectVania.Core.Gui;
using Unity.Rea.ProjectVania.Core.Menus;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Viewing;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Simulation
{
    /// <summary>Something that issues commands at the start of every step (a driver's controls, a replay).</summary>
    public interface IStepInput
    {
        /// <summary>Called before each step, with the world as the step finds it; submits that step's commands.</summary>
        void BeforeStep(GameWorld world);
    }

    /// <summary>
    /// The rules and state of the game. State is read through get-only properties and changes only inside
    /// Step, by applying queued commands, each producing events. Frame order within a step
    /// follows the original (RECONSTRUCTION 1): input commands first, then the systems in order.
    /// </summary>
    public sealed class GameWorld : IScriptHost
    {
        readonly Queue<ICommand> _pending = new Queue<ICommand>();
        readonly List<IGameEvent> _events = new List<IGameEvent>();
        readonly FixedStepper _clock = new FixedStepper();

        /// <summary>
        /// A world with no map and no characters (the state and command tests that need no grid), with menu.json's main
        /// menu if given (the menu tests) and console.json's console if given (the console tests).
        /// </summary>
        public GameWorld(MenuData menu = null, ConsoleData console = null, HudData hud = null)
        {
            if (menu != null) Menu = new MainMenu(menu);
            if (console != null) Console = new GameConsole(console);
            if (hud != null) Hud = new Hud(hud);
        }

        /// <summary>A world on <paramref name="map"/> (the bridge level, <see cref="Map.FromSettings"/>) with no characters.</summary>
        public GameWorld(Map map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// A world on <paramref name="map"/> with one character at each of player.json's spawn positions
        /// (<c>SceneManager.LoadContent</c>: two, at (500, 100) and (550, 100)), each <paramref name="unitSize"/> cells;
        /// with <paramref name="camera"/>, a camera for each character's player (<see cref="Camera"/>).
        /// </summary>
        public GameWorld(Map map, PlayerData player, Pair unitSize, CameraData camera = null)
            : this(map)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            foreach (var spawn in player.SpawnPositions)
            {
                _characters.Add(new PlayerCharacter(player, unitSize, new PixelVector(spawn.X, spawn.Y)));
            }

            AddCameras(camera);
        }

        /// <summary>
        /// The shipped world: one <paramref name="character"/> at each of player.json's spawn positions, each given a
        /// random name from <paramref name="names"/>' table for its gender drawn with <paramref name="random"/>
        /// (<c>new Player(position, gender)</c> and <c>g.RandomName</c>, RECONSTRUCTION 4.1); with <paramref name="camera"/>,
        /// a camera for each character's player (<see cref="Camera"/>); with <paramref name="menu"/>, the main menu (<see cref="Menu"/>);
        /// with <paramref name="console"/>, the console (<see cref="Console"/>); with <paramref name="audio"/>, its starting mute;
        /// with <paramref name="hud"/>, the HUD and its six start-up messages (<see cref="Hud"/>); with <paramref name="audio"/> and
        /// <paramref name="animations"/>, each character's run and wall-slide cues (<see cref="CharacterCues"/>); with
        /// <paramref name="editor"/>, the map editor (<see cref="Editor"/>), its drag box cut from <paramref name="animations"/>' frames.
        /// </summary>
        public GameWorld(Map map, PlayerData player, Character character, NameData names, Random random, CameraData camera = null, MenuData menu = null,
            ConsoleData console = null, AudioData audio = null, HudData hud = null, AnimationData animations = null, EditorData editor = null)
            : this(map, player, Repeat(character, player), names, random, camera, menu, console, audio, hud, animations, editor)
        {
        }

        /// <summary>
        /// The shipped world (owner decision, U.VAN-T25): characters.json's <paramref name="roster"/> (Morris, Shanoa, Leo),
        /// character i at player.json's spawn position i, each named from its own gender's table; otherwise as the
        /// single-character constructor. The roster may not be longer than the spawn positions.
        /// </summary>
        public GameWorld(Map map, PlayerData player, IReadOnlyList<Character> roster, NameData names, Random random, CameraData camera = null, MenuData menu = null,
            ConsoleData console = null, AudioData audio = null, HudData hud = null, AnimationData animations = null, EditorData editor = null)
            : this(map)
        {
            _editorData = editor;
            _frameSize = animations?.FrameSize ?? 0;
            if (menu != null) Menu = new MainMenu(menu);
            if (console != null) Console = new GameConsole(console);
            if (hud != null) Hud = new Hud(hud);
            Muted = audio != null && audio.Muted;
            _player = player ?? throw new ArgumentNullException(nameof(player));
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (roster.Count > player.SpawnPositions.Count) throw new ArgumentException("The roster has more characters than player.json has spawn positions.", nameof(roster));
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (random == null) throw new ArgumentNullException(nameof(random));
            for (var i = 0; i < roster.Count; i++)
            {
                var character = roster[i] ?? throw new ArgumentNullException(nameof(roster), "The roster names no character at " + i + ".");
                var spawn = player.SpawnPositions[i];
                var name = RandomName.Pick(names.For(character.Gender), random);
                _characters.Add(new PlayerCharacter(player, character, new PixelVector(spawn.X, spawn.Y), name));
                if (audio != null) _characterCues.Add(new CharacterCues(animations?.Find(character.Sheet), audio, FixedStepper.StepsPerSecond));
            }

            AddCameras(camera);
        }

        // The single-character worlds (the tests' and the original's): that character at every spawn position.
        static IReadOnlyList<Character> Repeat(Character character, PlayerData player)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (player == null) throw new ArgumentNullException(nameof(player));
            var roster = new Character[player.SpawnPositions.Count];
            for (var i = 0; i < roster.Length; i++) roster[i] = character;
            return roster;
        }

        // Player n starts on character n (DrivenIndex), so each character's player gets a camera; a world built without
        // camera data has none, and camera commands then do nothing.
        void AddCameras(CameraData camera)
        {
            _camera = camera;
            if (camera == null) return;
            for (var i = 0; i < _characters.Count; i++)
            {
                _cameras.Add(i, new WorldCamera(camera));
            }
        }

        readonly List<PlayerCharacter> _characters = new List<PlayerCharacter>();

        // Each character's sounds (step2 and drag), one per character, or none for a world built without audio.json.
        readonly List<CharacterCues> _characterCues = new List<CharacterCues>();
        readonly PlayerData _player;
        ActionKind[] _actionsBefore = Array.Empty<ActionKind>();

        // Which character each player drives, once [ or ] has moved them off their own (the original's
        // SceneManager.playerIndex, one per player instead of one global).
        readonly Dictionary<int, int> _driven = new Dictionary<int, int>();

        // Each player's camera (the original's one CameraManager, one per player instead of one global), stepped in
        // player order, and where each player's map-editor cursor is (MapEditor.Cursor.Position, which LooseCursor follows).
        readonly SortedDictionary<int, WorldCamera> _cameras = new SortedDictionary<int, WorldCamera>();
        CameraData _camera;

        // Each player's map editor (the original's one MapEditor, one per player), made the first time that player's
        // commands reach it and stepped in player order while the game is in the MapEditor state.
        readonly SortedDictionary<int, MapEditor> _editors = new SortedDictionary<int, MapEditor>();
        readonly EditorData _editorData;
        readonly int _frameSize;

        /// <summary>The collision grid (RECONSTRUCTION 5), or null for a world built without one.</summary>
        public Map Map { get; }

        /// <summary>
        /// The main menu (RECONSTRUCTION 9.2), or null for a world built without menu.json; without one, Esc still opens
        /// and closes the MainMenu state and Back closes it, but nothing is selected and no cue is named.
        /// </summary>
        public MainMenu Menu { get; }

        /// <summary>
        /// The console (RECONSTRUCTION 10), or null for a world built without console.json; without one, ~ flips the
        /// Console state at once and typing does nothing.
        /// </summary>
        public GameConsole Console { get; }

        /// <summary>
        /// The always-on HUD (RECONSTRUCTION 9.1): the quick bar, the fade lens and the message stack, or null for a world
        /// built without hud.json; without one, messages and fades are still named as events but nothing keeps them.
        /// </summary>
        public Hud Hud { get; }

        /// <summary>
        /// The lens' fade (<c>Gui.Lenses.Fade</c>), Off at start: the HUD lens' state, which the script sets and the lens
        /// itself moves to Off when a fade in finishes; without a HUD, what the script last set.
        /// </summary>
        public FadeState Fade => Hud != null ? Hud.Lens.State : _fade;

        FadeState _fade;

        /// <summary>Whether the game's sound is muted (<c>Audio.Muted</c>): no cue is named and no song started while it is.</summary>
        public bool Muted { get; private set; }

        /// <summary>Whether a song is playing, paused or stopped (<c>MediaPlayer.State</c>).</summary>
        public MusicState Music { get; private set; }

        /// <summary>Every character, all simulated each Play step whether driven or not (RECONSTRUCTION 4).</summary>
        public IReadOnlyList<PlayerCharacter> Characters => _characters;

        /// <summary>
        /// The index into <see cref="Characters"/> of the character <paramref name="player"/>'s commands drive and the
        /// camera follows (the original's <c>Scene.PlayerIndex</c>): player n starts on character n and [ / ] move it
        /// (<see cref="CycleCharacter"/>). -1 if there is none.
        /// </summary>
        public int DrivenIndex(PlayerId player)
        {
            if (!player.IsValid) return -1;
            if (_driven.TryGetValue(player.Value, out var index)) return index;
            return player.Value < _characters.Count ? player.Value : -1;
        }

        /// <summary>The character <paramref name="player"/> drives (<c>Scene.CurrentPlayer</c>), or null if there is none.</summary>
        public PlayerCharacter Driven(PlayerId player)
        {
            var index = DrivenIndex(player);
            return index >= 0 ? _characters[index] : null;
        }

        /// <summary>
        /// <paramref name="player"/>'s camera (<c>CameraManager</c>, RECONSTRUCTION 7), or null if the world was built
        /// without camera data or the player has none. It changes only as the world applies that player's commands.
        /// </summary>
        public WorldCamera Camera(PlayerId player) =>
            player.IsValid && _cameras.TryGetValue(player.Value, out var camera) ? camera : null;

        /// <summary>
        /// Where <paramref name="player"/>'s map-editor cursor is (<c>MapEditor.Cursor.Position</c>, the original's (0, 0)
        /// until the editor first opens), which the LooseCursor camera mode follows. Entering the editor puts it on the
        /// driven character, as <c>ToggleMapEditor</c> did; then each editor step moves it (<see cref="MapEditor"/>).
        /// </summary>
        public PixelVector EditorCursor(PlayerId player) => Editor(player)?.Cursor ?? new PixelVector(0f, 0f);

        /// <summary>
        /// <paramref name="player"/>'s map editor (RECONSTRUCTION 12), or null until that player first uses it. A world built
        /// without editor.json still keeps where its cursor was put, and does nothing else in the editor.
        /// </summary>
        public MapEditor Editor(PlayerId player) =>
            player.IsValid && _editors.TryGetValue(player.Value, out var editor) ? editor : null;

        MapEditor EditorFor(PlayerId player)
        {
            if (!_editors.TryGetValue(player.Value, out var editor))
            {
                editor = new MapEditor(_editorData, _frameSize);
                _editors.Add(player.Value, editor);
            }

            return editor;
        }

        /// <summary>Steps taken so far.</summary>
        public long Tick { get; private set; }

        /// <summary>The original starts in Play (RECONSTRUCTION 2).</summary>
        public GameState State { get; private set; } = GameState.Play;

        /// <summary>
        /// Whether the console is down or sliding down (the original's <c>Console.Visible</c>). It is separate from the
        /// state: Esc from the console opens the menu with the console still down, and closing the menu returns to Play
        /// under it.
        /// </summary>
        public bool ConsoleOpen => Console != null ? Console.Visible : _consoleOpen;

        bool _consoleOpen;

        // Who last toggled the console, named on the state changes its slide makes.
        PlayerId _consolePlayer = PlayerId.One;

        // Who typed the line the console is running, named on the script's exit.
        PlayerId _scriptPlayer = PlayerId.One;

        /// <summary>Events produced by the last <see cref="Advance"/> call, oldest first. Cleared on the next call.</summary>
        public IReadOnlyList<IGameEvent> Events => _events;

        /// <summary>Interpolation factor between the previous and the next state, for rendering.</summary>
        public double Alpha => _clock.Alpha;

        public int PendingCommands => _pending.Count;

        /// <summary>Queues a command for the next step. Rejects a null command.</summary>
        public void Submit(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!command.Player.IsValid) throw new ArgumentException("A command must name its player.", nameof(command));
            _pending.Enqueue(command);
        }

        /// <summary>
        /// Feeds frame time in, runs every fixed step now due and returns how many ran. <paramref name="input"/>, if
        /// given, issues its commands before each step, reading the state that step starts from.
        /// </summary>
        public int Advance(double deltaSeconds, IStepInput input = null) => Run(_clock.Advance(deltaSeconds), input);

        /// <summary>
        /// Runs exactly <paramref name="steps"/> fixed steps, whatever the clock says, and returns them; the events of the
        /// last call are cleared first, as <see cref="Advance"/> does. A world that follows another's steps (online, a
        /// replica of the authority's, U.VAN-T31) runs only the steps it has been sent, through this.
        /// </summary>
        public int Run(int steps, IStepInput input = null)
        {
            _events.Clear();
            for (int i = 0; i < steps; i++)
            {
                input?.BeforeStep(this);
                Step();
            }

            return steps;
        }

        /// <summary>
        /// One 1/60 s step in the original's frame order (RECONSTRUCTION 1): apply every queued command in order (the
        /// InputManager), then, only in Play, update every character (the SceneManager), then, in every state, every
        /// camera, following the character already moved (the CameraManager), then the console's slide and blink (the
        /// GuiManager), then advance the tick.
        /// </summary>
        void Step()
        {
            if (_actionsBefore.Length != _characters.Count) _actionsBefore = new ActionKind[_characters.Count];
            for (int i = 0; i < _characters.Count; i++)
            {
                _actionsBefore[i] = _characters[i].Action;
                _characters[i].BeginStep();
            }

            foreach (var camera in _cameras.Values) camera.BeginStep();

            while (_pending.Count > 0) Apply(_pending.Dequeue());

            if (State == GameState.Play)
            {
                for (var i = 0; i < _characters.Count; i++)
                {
                    _characters[i].Step(Map, FixedStepper.OriginalElapsedSeconds);
                    // CheckActionState's Action_Run / Action_Wallslide sounds, each character in turn as the original's
                    // foreach updated them.
                    if (i < _characterCues.Count) Cue(_characterCues[i].Step(_characters[i], Tick, FixedStepper.OriginalElapsedSeconds, Muted));
                }
            }

            foreach (var entry in _cameras)
            {
                entry.Value.Step(CameraTarget(new PlayerId(entry.Key), entry.Value.Mode), Map);
            }

            if (Console != null)
            {
                switch (Console.Update())
                {
                    case SlideResult.Play:
                        ChangeState(_consolePlayer, GameState.Play);
                        break;
                    case SlideResult.Console:
                        ChangeState(_consolePlayer, GameState.Console);
                        break;
                }
            }

            // The rest of GuiManager.Update: the quick bar, the lenses and the message stack.
            Hud?.Update();

            // MapEditor.Update, the last component, only in the editor: each player's cursor, drag, paint and palette pick.
            if (State == GameState.MapEditor)
            {
                foreach (var entry in _editors)
                {
                    entry.Value.Step(Camera(new PlayerId(entry.Key)), Map, _characters, _camera != null ? _camera.EditorScrollSteps.X : 0f);
                }
            }

            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Action != _actionsBefore[i])
                {
                    _events.Add(new CharacterActionChanged(Tick, i, _actionsBefore[i], _characters[i].Action));
                }
            }

            Tick++;
        }

        void Apply(ICommand command)
        {
            switch (command)
            {
                case SetGameState set:
                    ChangeState(set.Player, set.State);
                    break;

                // Action_ToggleMainMenu: from the menu always back to Play (even if it was opened from the editor or the
                // console), playing menu_close; from anything else into the menu, playing menu_open.
                case ToggleMainMenu toggle:
                    ToggleMenu(toggle.Player);
                    break;

                // ToggleMapEditor: from the editor to Play with the camera back on LoosePlayer; from anything else (even
                // the menu) into the editor with the camera Free and the editor cursor on the driven character.
                case ToggleMapEditor toggle:
                    if (State == GameState.MapEditor)
                    {
                        ChangeState(toggle.Player, GameState.Play);
                        Camera(toggle.Player)?.SetMode(CameraMode.LoosePlayer);
                    }
                    else
                    {
                        ChangeState(toggle.Player, GameState.MapEditor);
                        Camera(toggle.Player)?.SetMode(CameraMode.Free);
                        var driven = Driven(toggle.Player);
                        if (driven != null) EditorFor(toggle.Player).Open(driven.Position);
                    }

                    break;

                // Reset: always back to Play, the camera back on LoosePlayer (every player's, as every character resets)
                // and every character to the reset position, Falling (RECONSTRUCTION 4.9).
                case ResetGame reset:
                    ChangeState(reset.Player, GameState.Play);
                    foreach (var camera in _cameras.Values) camera.SetMode(CameraMode.LoosePlayer);
                    foreach (var character in _characters)
                    {
                        character.Reset(new PixelVector(_player.ResetPosition.X, _player.ResetPosition.Y));
                    }

                    break;

                // The driven character's controls (Command_Move, Command_Stand, Command_Kneel, Command_Jump).
                case MoveCharacter move:
                    Driven(move.Player)?.Move(move.Direction);
                    break;

                case StandCharacter stand:
                    Driven(stand.Player)?.StopMoving();
                    break;

                case KneelCharacter kneel:
                    Driven(kneel.Player)?.Kneel();
                    break;

                case JumpCharacter jump:
                    Driven(jump.Player)?.Jump();
                    break;

                case CycleCharacter cycle:
                    Cycle(cycle.Player, cycle.Direction);
                    break;

                // Command_ToggleConsole plays the console's cue (the long one once it is full height), even while a slide
                // ignores the toggle, then starts a slide the other way; the slide itself sets the state (Step).
                case ToggleConsole toggle:
                    ToggleTheConsole(toggle.Player);
                    break;

                case EditConsole edit when Console != null:
                    _scriptPlayer = edit.Player;
                    Edit(edit);
                    break;

                case EndSong _:
                    Music = MusicState.Stopped;
                    break;

                case NavigateMenu navigate when State == GameState.MainMenu:
                    Navigate(navigate.Player, navigate.Input);
                    break;

                case CycleCameraMode cycle:
                    Camera(cycle.Player)?.CycleMode();
                    break;

                case NudgeCamera nudge:
                    Camera(nudge.Player)?.Nudge(nudge.Nudge);
                    break;

                // An editor direction press steps the palette's tile unless Ctrl (TilePalette.SelectTileLeft ...), then
                // scrolls the camera a cell step unless Shift (Cursor.MoveCellLeft ...: ScrollLeft(4)).
                case StepEditor step:
                    if (step.SelectTile) EditorFor(step.Player).StepTile(step.Dx, step.Dy);
                    if (step.MoveCell) Camera(step.Player)?.ScrollCell(step.Dx, step.Dy);
                    break;

                // Space held or not, every editor step (CheckInput_MapEditor).
                case SetEditorCursor set:
                    EditorFor(set.Player).SetMode(set.Mode);
                    break;

                // The mouse the editor's update reads this step.
                case PointEditor point:
                    EditorFor(point.Player).Point(point);
                    break;

                case ToggleFullscreen fullscreen:
                    _events.Add(new FullscreenToggled(Tick, fullscreen.Player));
                    break;

                // U (Play and the editor): QuickBar.ToggleQuickBar, ignored while it slides.
                case ToggleQuickBar _:
                    Hud?.QuickBar.Toggle();
                    break;

                // A pad connecting (Play and the editor): MessageHandler.InvokeMessage("XBox 360 Gamepad Connected", Peru).
                case AnnounceGamepad _ when Hud != null:
                    var notice = Hud.Data.Messages.GamepadNotice;
                    Hud.Messages.Invoke(notice.Text, notice.Colour.ToRgba());
                    break;
            }
        }

        void ToggleTheConsole(PlayerId player)
        {
            if (Console == null)
            {
                _consoleOpen = !_consoleOpen;
                _events.Add(new ConsoleToggled(Tick, _consoleOpen));
                ChangeState(player, _consoleOpen ? GameState.Console : GameState.Play);
                return;
            }

            Cue(Console.ToggleCue);
            if (Console.Sliding)
            {
                return;
            }

            _consolePlayer = player;
            Console.Toggle();
            _events.Add(new ConsoleToggled(Tick, Console.Visible));
        }

        /// <summary><c>CheckInput_Console</c>'s edits; Enter runs the line as a script after adding it to the history.</summary>
        void Edit(EditConsole edit)
        {
            switch (edit.Input)
            {
                case ConsoleInput.Submit:
                    ConsoleScript.Execute(Console.Submit(), Console, this);
                    Console.FinishSubmit();
                    break;
                case ConsoleInput.Clear:
                    Console.Clear();
                    break;
                case ConsoleInput.Backspace:
                    Console.Backspace();
                    break;
                case ConsoleInput.Recall:
                    Console.Recall();
                    break;
                case ConsoleInput.Append:
                    Console.Append(edit.Text);
                    break;
            }
        }

        // The script's reach outside the console (IScriptHost).
        void IScriptHost.Message(string text, SettingsColour colour)
        {
            Hud?.Messages.Add(text, colour.ToRgba());
            _events.Add(new MessageRequested(Tick, text, colour.ToRgba()));
        }

        void IScriptHost.Fade(FadeState fade)
        {
            _fade = fade;
            Hud?.Lens.Set(fade);
            _events.Add(new FadeRequested(Tick, fade));
        }

        // AudioManager.PlayMusic: nothing while muted.
        void IScriptHost.PlaySong(string song)
        {
            if (Muted) return;
            Music = MusicState.Playing;
            _events.Add(new SongRequested(Tick, song));
        }

        void IScriptHost.PauseMusic()
        {
            Music = MusicState.Paused;
            _events.Add(new MusicPauseChanged(Tick, true));
        }

        // MediaPlayer.Resume: only a paused song resumes; with none, nothing changes.
        void IScriptHost.ResumeMusic()
        {
            if (Music != MusicState.Paused) return;
            Music = MusicState.Playing;
            _events.Add(new MusicPauseChanged(Tick, false));
        }

        void IScriptHost.SetMuted(bool muted)
        {
            Muted = muted;
            _events.Add(new MuteChanged(Tick, muted));
        }

        void IScriptHost.Quit() => _events.Add(new QuitRequested(Tick, _scriptPlayer));

        void ToggleMenu(PlayerId player)
        {
            var opening = State != GameState.MainMenu;
            Cue(opening ? Menu?.Data.OpenCue : Menu?.Data.CloseCue);
            ChangeState(player, opening ? GameState.MainMenu : GameState.Play);
        }

        /// <summary>
        /// <c>CheckInput_MainMenu</c>'s menu actions. Back: at the root it closes the menu (<c>Action_ToggleMainMenu</c>),
        /// otherwise it goes up a level, silently (<c>Action_GoToParentMenuLevel</c>). Up / Down move with wrap and play
        /// <c>menu_move</c>. Select plays <c>menu_select</c>, then does the item's action: Open shows its level, Quit asks
        /// the program to exit (<c>Game.Exit()</c>), ToggleFullscreen flips the window (<c>Action_ToggleFullscreen</c>).
        /// </summary>
        void Navigate(PlayerId player, MenuInput input)
        {
            if (Menu == null)
            {
                if (input == MenuInput.Back) ToggleMenu(player);
                return;
            }

            switch (input)
            {
                case MenuInput.Back:
                    if (Menu.AtRoot) ToggleMenu(player);
                    else Menu.Back();
                    break;

                case MenuInput.Up:
                    Cue(Menu.Data.MoveCue);
                    Menu.Up();
                    break;

                case MenuInput.Down:
                    Cue(Menu.Data.MoveCue);
                    Menu.Down();
                    break;

                case MenuInput.Select:
                    Cue(Menu.Data.SelectCue);
                    switch (Menu.Select())
                    {
                        case MenuAction.Quit:
                            _events.Add(new QuitRequested(Tick, player));
                            break;
                        case MenuAction.ToggleFullscreen:
                            _events.Add(new FullscreenToggled(Tick, player));
                            break;
                    }

                    break;
            }
        }

        // Audio.PlayAmbience / PlaySfx: nothing while muted.
        void Cue(string cue)
        {
            if (!Muted && !string.IsNullOrEmpty(cue)) _events.Add(new CueRequested(Tick, cue));
        }

        /// <summary>What a camera in <paramref name="mode"/> follows: the editor cursor for LooseCursor, else the driven character.</summary>
        PixelVector? CameraTarget(PlayerId player, CameraMode mode)
        {
            if (mode == CameraMode.LooseCursor) return EditorCursor(player);
            var driven = Driven(player);
            return driven != null ? driven.Position : (PixelVector?)null;
        }

        /// <summary>
        /// <c>CyclePlayerLeft</c> / <c>CyclePlayerRight</c>: drive the previous or next character, wrapping at either end.
        /// The character left behind lets go of its direction (BIBLE defect 16 fixed): in the original it kept the
        /// <c>movement</c> it last had, so a character switched away from mid-run ran on, undriven, forever.
        /// </summary>
        void Cycle(PlayerId player, int direction)
        {
            var from = DrivenIndex(player);
            if (from < 0 || direction == 0) return;

            var last = _characters.Count - 1;
            var to = direction < 0
                ? (from > 0 ? from - 1 : last)
                : (from < last ? from + 1 : 0);
            if (to == from) return;

            _characters[from].StopMoving();
            _driven[player.Value] = to;
            _events.Add(new DrivenCharacterChanged(Tick, player, from, to));
        }

        void ChangeState(PlayerId player, GameState to)
        {
            if (to == State)
            {
                return;
            }

            var from = State;
            State = to;
            _events.Add(new GameStateChanged(Tick, player, from, to));
        }
    }
}
