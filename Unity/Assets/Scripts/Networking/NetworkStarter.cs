using Lobby;
using Unity.Netcode;
using UnityEngine;


namespace Networking
{
	/// <summary>
	/// Makes sure the session's NetworkManager exists, creating it from its prefab the first time
	/// any scene loads. It then lasts for the whole session, so later scenes find it already there.
	///
	/// In the arena, also starts local play when the scene is played directly in the editor, so it
	/// can be tried without going through the menu.
	/// </summary>
	public class NetworkStarter : MonoBehaviour
	{
		[SerializeField]
		private NetworkManager networkManagerPrefab;

		[SerializeField]
		private bool startLocalIfNotConnected;

		private void Awake()
		{
			if (NetworkManager.Singleton == null)
				Instantiate(networkManagerPrefab);
		}

		private void Start()
		{
			var networkManager = NetworkManager.Singleton;

			if (startLocalIfNotConnected && !networkManager.IsListening)
				networkManager.GetComponent<GameSession>().PlayLocal();
		}
	}
}
