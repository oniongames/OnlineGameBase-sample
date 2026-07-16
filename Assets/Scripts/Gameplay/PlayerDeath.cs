using System;
using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player has died.
    /// </summary>
    /// <typeparam name="PlayerDeath"></typeparam>
    public class PlayerDeath : Simulation.Event<PlayerDeath>
    {
        PlatformerModel model = Simulation.GetModel<PlatformerModel>();
        [NonSerialized]
        internal PlayerController player;

        public override void Execute()
        {
            var target = player == null ? model.player : player;
            if (target == null || target.health == null || !target.health.IsAlive)
                return;

            target.health.Die();
            target.controlEnabled = false;

            if (target == model.player)
            {
                model.virtualCamera.Follow = null;
                model.virtualCamera.LookAt = null;
            }

            if (target.audioSource && target.ouchAudio)
                target.audioSource.PlayOneShot(target.ouchAudio);

            target.animator.SetTrigger("hurt");
            target.animator.SetBool("dead", true);

            if (target == model.player)
                Simulation.Schedule<PlayerSpawn>(2).player = target;
        }

        internal override void Cleanup()
        {
            player = null;
        }
    }
}
