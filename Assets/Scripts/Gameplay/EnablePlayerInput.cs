using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;

namespace Platformer.Gameplay
{
    /// <summary>
    /// This event is fired when user input should be enabled.
    /// </summary>
    public class EnablePlayerInput : Simulation.Event<EnablePlayerInput>
    {
        PlatformerModel model = Simulation.GetModel<PlatformerModel>();
        public PlayerController player;

        public override void Execute()
        {
            var target = player == null ? model.player : player;
            if (target != null)
                target.controlEnabled = true;
        }

        internal override void Cleanup()
        {
            player = null;
        }
    }
}
