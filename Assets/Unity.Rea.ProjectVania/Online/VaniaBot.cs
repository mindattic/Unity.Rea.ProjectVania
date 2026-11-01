using System.Threading.Tasks;
using Unity.Multiplayer; // IBotDriver, NetBot, [NetBotDriver]
using Unity.Rea.ProjectVania.Core.Input;
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.Rea.ProjectVania.Online
{
    /// <summary>
    /// The game's bot (U.VAN-T31): launched with <c>-bot A</c> (<c>mp-host game smoke</c>), the kit joins with no menus
    /// and hands the connected session here. It plays as a player does, through its <see cref="VaniaNetPlayer"/>: once its
    /// replica is running it holds D (role A) or A (any other role), and passes when the authority's steps, run in its own
    /// replica, have carried its character that way.
    /// </summary>
    [NetBotDriver, Preserve]
    public sealed class VaniaBot : IBotDriver
    {
        /// <summary>Pixels the bot's character must run before it passes.</summary>
        public const float PassDistance = 32f;

        public static Key KeyFor(string role) => role == "A" ? Key.D : Key.A;

        public async Task RunAsync(NetBot bot)
        {
            var me = await bot.LocalPlayer<VaniaNetPlayer>();
            await bot.Until(() => me.Following, "the authority's game to reach this replica");
            var world = me.Screen.World;
            await bot.Until(() => world.Driven(me.PlayerId) != null, "a character for seat " + me.Seat);
            var start = world.Driven(me.PlayerId).Position.X;
            var key = KeyFor(bot.Role);
            me.Script = frame => frame.Set(key);
            while (Mathf.Abs(world.Driven(me.PlayerId).Position.X - start) < PassDistance && bot.SecondsLeft > 0f)
            {
                await bot.Seconds(0.1f);
            }

            me.Script = null;
            var moved = world.Driven(me.PlayerId).Position.X - start;
            if (Mathf.Abs(moved) < PassDistance)
            {
                bot.Fail("seat " + me.Seat + "'s character never ran (moved " + moved + " px)");
                return;
            }

            bot.Pass("seat " + me.Seat + " ran " + moved + " px in the authority's game");
        }
    }
}
