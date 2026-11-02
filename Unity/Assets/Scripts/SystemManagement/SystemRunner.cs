using UnityEngine;
using DependencyInjection;


namespace SystemManagement
{
	public class SystemsRunner : MonoBehaviour
	{
		private SystemManager systemManager;

		private void Awake()
		{
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