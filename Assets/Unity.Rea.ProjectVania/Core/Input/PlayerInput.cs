using System;
using System.Collections.Generic;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.Simulation;

namespace Unity.Rea.ProjectVania.Core.Input
{
    /// <summary>Where a step's control state comes from: the Input System in the game, a script in tests.</summary>
    public interface IInputDevice
    {
        /// <summary>Fills <paramref name="frame"/> (already cleared) with every control's state for the step about to run.</summary>
        void Sample(InputFrame frame);
    }

    /// <summary>
    /// One driver's controls: before each 60 Hz step it samples the device, keeps the previous step's frame for the
    /// edge tests, routes by the world's state at the start of the step and submits the commands, all carrying this
    /// driver's <see cref="PlayerId"/>. The original read its devices once per frame, which here is once per step.
    /// </summary>
    public sealed class PlayerInput : IStepInput
    {
        readonly InputData _bindings;
        readonly IInputDevice _device;
        readonly List<ICommand> _commands = new List<ICommand>();

        public PlayerInput(PlayerId player, InputData bindings, IInputDevice device)
        {
            if (!player.IsValid)
            {
                throw new ArgumentException("Input must drive a named player.", nameof(player));
            }

            Player = player;
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public PlayerId Player { get; }

        public InputState State { get; } = new InputState();

        /// <summary>The commands the last step's input issued, in order.</summary>
        public IReadOnlyList<ICommand> LastCommands => _commands;

        public void BeforeStep(GameWorld world)
        {
            State.Shift();
            _device.Sample(State.Current);
            _commands.Clear();
            InputRouter.Route(world.State, State, _bindings, Player, _commands);
            foreach (var command in _commands)
            {
                world.Submit(command);
            }
        }
    }
}
