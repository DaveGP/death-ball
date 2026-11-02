using UnityEngine;
using Players;
using SystemManagement;


namespace DependencyInjection
{
	public static class Bootstrapper
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Init()
		{
			InitialiseSystemManager();

			InitialisePlayers();

			Debug.Log("Bootstrapper: Services registered.");
		}

		private static void InitialiseSystemManager()
		{
			ServiceLocator.Register<SystemManager>(new SystemManager());
		}

		// Local players are created as devices join, see LocalPlayerManager
		private static void InitialisePlayers()
		{
			ServiceLocator.Register<LocalPlayerRegistry>(new LocalPlayerRegistry());
		}
	}
}
