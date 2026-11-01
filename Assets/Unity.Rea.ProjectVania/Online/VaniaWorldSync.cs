using Unity.Multiplayer;      // NetEntity, NetSession
using Unity.Multiplayer.Core; // [ToClients]
using Unity.Rea.ProjectVania.Core.Net;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Online
{
    /// <summary>
    /// The one authority's steps, sent to every peer (U.VAN-T31). The server spawns one per session, keyed
    /// <see cref="Key"/>, and every frame, after the game has stepped, sends the steps the authority recorded since the
    /// last frame (<see cref="LockstepAuthority.TakeNew"/>) to the clients, whose replicas run them. Nothing on a client
    /// changes a world any other way.
    /// </summary>
    [DefaultExecutionOrder(1000)] // after GameHost.Update has run this frame's steps
    public partial class VaniaWorldSync : NetEntity
    {
        /// <summary>The network key the server spawns it under.</summary>
        public const string Key = "vania:world";

        // server -> every peer: StepCodec.EncodeSteps of the steps just run. Reliable and in order.
        [ToClients]
        public partial void Publish(byte[] steps);

        /// <summary>Every peer: step messages received.</summary>
        public int Received { get; private set; }

        /// <summary>The world sync of <paramref name="session"/>, once it has spawned there (null before).</summary>
        public static VaniaWorldSync For(NetSession session) =>
            session != null && session.Objects.TryGet(Key, out VaniaWorldSync sync) ? sync : null;

        /// <summary>Server: spawns the world sync for <paramref name="session"/> from <paramref name="prefab"/> (a prefab with a NetKey).</summary>
        public static VaniaWorldSync Serve(NetSession session, GameObject prefab)
        {
            var existing = For(session);
            return existing != null ? existing : session.Objects.Spawn(prefab, Key).GetComponent<VaniaWorldSync>();
        }

        private void OnPublish(byte[] steps)
        {
            Received++;
            if (IsServer)
            {
                return;
            }

            var screen = Bootstrap.Current;
            if (screen != null && screen.Replica != null)
            {
                screen.Replica.Receive(StepCodec.DecodeSteps(steps), screen.World);
            }
        }

        private void Update()
        {
            var authority = VaniaOnline.Authority;
            if (!IsSpawned || !IsServer || authority == null)
            {
                return;
            }

            var steps = authority.TakeNew();
            if (steps.Count > 0)
            {
                Publish(StepCodec.EncodeSteps(steps));
            }
        }
    }
}
