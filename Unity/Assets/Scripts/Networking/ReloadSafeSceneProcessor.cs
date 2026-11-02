using FishNet.Managing.Scened;
using UnityEngine.SceneManagement;


namespace Networking
{
	/// <summary>
	/// FishNet's scene processor, but it makes its holding scenes again if they have been unloaded.
	///
	/// FishNet keeps the scene it parks networked objects in while it changes scenes, and only makes
	/// a new one when the old one has no name. Going back to the menu after a match is a plain Unity
	/// scene load, which unloads that holding scene, but the old handle keeps its name. The next time
	/// a client joined a match, its bodies, ball and match state were parked in the unloaded scene and
	/// destroyed, and the arena never finished loading.
	/// </summary>
	public class ReloadSafeSceneProcessor : DefaultSceneProcessor
	{
		public override Scene GetMovedObjectsScene()
		{
			if (!MovedObjectsScene.IsValid() || !MovedObjectsScene.isLoaded)
				MovedObjectsScene = default;

			return base.GetMovedObjectsScene();
		}

		public override Scene GetDelayedDestroyScene()
		{
			if (!DelayedDestroyScene.IsValid() || !DelayedDestroyScene.isLoaded)
				DelayedDestroyScene = default;

			return base.GetDelayedDestroyScene();
		}
	}
}
