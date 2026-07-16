using System;
using Platformer.Core;
using Platformer.Mechanics;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when a player enters a trigger with a DeathZone component.
    /// </summary>
    /// <typeparam name="PlayerEnteredDeathZone"></typeparam>
    public class PlayerEnteredDeathZone : Simulation.Event<PlayerEnteredDeathZone>
    {
        public DeathZone deathzone;
        [NonSerialized]
        internal PlayerController player;

        public override void Execute()
        {
            Simulation.Schedule<PlayerDeath>(0).player = player;
        }

        internal override void Cleanup()
        {
            deathzone = null;
            player = null;
        }
    }
}
