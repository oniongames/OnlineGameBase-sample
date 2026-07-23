using System;
using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player is spawned after dying.
    /// </summary>
    public class PlayerSpawn : Simulation.Event<PlayerSpawn>
    {
        PlatformerModel model = Simulation.GetModel<PlatformerModel>();
        [NonSerialized]
        internal PlayerController player;

        public override void Execute()
        {
            var target = player == null ? model.player : player;
            target.collider2d.enabled = true;
            target.controlEnabled = false;
            if (target.audioSource && target.respawnAudio)
                target.audioSource.PlayOneShot(target.respawnAudio);
            target.health.Increment();
            target.Teleport(model.spawnPoint.transform.position);
            target.jumpState = PlayerController.JumpState.Grounded;
            target.animator.SetBool("dead", false);
            if (target == model.player)
            {
                model.virtualCamera.Follow = target.transform;
                model.virtualCamera.LookAt = target.transform;
            }

            Simulation.Schedule<EnablePlayerInput>(2f).player = target;
        }

        internal override void Cleanup()
        {
            player = null;
        }
    }
}
