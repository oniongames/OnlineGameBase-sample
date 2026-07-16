using UnityEngine;
using UnityEngine.SceneManagement;

namespace Platformer.Multiplayer
{
    public static class OogbPlatformerMultiplayerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene")
                return;

            if (Object.FindFirstObjectByType<OogbPlatformerMultiplayerController>() != null)
                return;

            var controllerObject = new GameObject("OOGB Platformer Multiplayer Controller");
            controllerObject.AddComponent<OogbPlatformerMultiplayerController>();
        }
    }
}
