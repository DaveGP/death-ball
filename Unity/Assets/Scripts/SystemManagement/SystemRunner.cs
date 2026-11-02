using UnityEngine;
using DependencyInjection;


namespace SystemManagement
{
	// One for the whole session: the menu brings it, and the copy in a game scene loaded
	// from the menu removes itself, so systems never update twice in a frame
	public class SystemsRunner : MonoBehaviour
	{
		private static SystemsRunner instance;

		private SystemManager systemManager;

		private void Awake()
		{
			if (instance != null)
			{
				Destroy(gameObject);
				return;
			}

			instance = this;

			DontDestroyOnLoad(gameObject);

			systemManager = ServiceLocator.Get<SystemManager>();
		}

		private void Update()
		{
			systemManager.Update();
		}

		private void FixedUpdate()
		{
			systemManager.FixedUpdate();
		}

		private void LateUpdate()
		{
			systemManager.LateUpdate();
		}
	}
}