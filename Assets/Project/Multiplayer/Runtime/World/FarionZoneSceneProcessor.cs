using FishNet.Managing.Scened;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.World
{
    public sealed class FarionZoneSceneProcessor : DefaultSceneProcessor
    {
        bool zoneLoad;

        public override void LoadStart(LoadQueueData queueData)
        {
            base.LoadStart(queueData);
            zoneLoad = MultiplayerZoneSceneLoad.TryReadZoneId(
                queueData.SceneLoadData,
                out _);
        }

        public override void BeginLoadAsync(
            string sceneName,
            LoadSceneParameters parameters)
        {
            if (zoneLoad)
            {
                parameters.localPhysicsMode = LocalPhysicsMode.Physics3D;
            }

            base.BeginLoadAsync(sceneName, parameters);
        }
    }
}
