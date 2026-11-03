using System.Linq;
using System.Reflection;
using Lobby;
using Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;


namespace Builders
{
	/// <summary>
	/// Builds the session's NetworkManager prefab, which both scenes create from when it doesn't
	/// exist yet: the NetworkManager with both transports (offline for local play, Unity Transport
	/// for Relay) and the GameSession. Also makes the lobby prefab and the list of every networked
	/// prefab NGO is allowed to spawn.
	/// </summary>
	public static class NetworkManagerBuilder
	{
		public const string NetworkManagerPrefabPath = "Assets/Prefabs/NetworkManager.prefab";

		private const string LobbyStatePrefabPath = "Assets/Prefabs/LobbyState.prefab";

		private const string NetworkPrefabsListPath = "Assets/Prefabs/NetworkPrefabs.asset";

		public static void Build()
		{
			ArenaSceneBuilder.EnsureFolder("Assets/Prefabs");

			var lobbyStatePrefab = CreateLobbyStatePrefab();

			var prefabsList = CreateNetworkPrefabsList();

			var root = new GameObject("NetworkManager");

			var networkManager = root.AddComponent<NetworkManager>();

			var offlineTransport = root.AddComponent<OfflineTransport>();

			root.AddComponent<UnityTransport>();

			networkManager.NetworkConfig = new NetworkConfig
			{
				// The GameSession picks the transport for each way of playing
				NetworkTransport = offlineTransport,
				EnableSceneManagement = true,
			};

			networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);

			var gameSession = root.AddComponent<GameSession>();
			ArenaSceneBuilder.SetSerializedReference(gameSession, "lobbyStatePrefab", lobbyStatePrefab.GetComponent<NetworkObject>());

			PrefabUtility.SaveAsPrefabAsset(root, NetworkManagerPrefabPath);

			Object.DestroyImmediate(root);
		}

		/// <summary>
		/// The saved NetworkManager prefab. Load it after making a new scene: making one unloads
		/// assets nothing references yet, which leaves an earlier copy pointing at nothing.
		/// </summary>
		public static NetworkManager LoadPrefab()
			=> AssetDatabase.LoadAssetAtPath<NetworkManager>(NetworkManagerPrefabPath);

		private static GameObject CreateLobbyStatePrefab()
		{
			var root = new GameObject("LobbyState");

			root.AddComponent<NetworkObject>();
			root.AddComponent<LobbyState>();

			var prefab = PrefabUtility.SaveAsPrefabAsset(root, LobbyStatePrefabPath);

			Object.DestroyImmediate(root);

			return prefab;
		}

		// NGO tells prefabs apart across machines by a hash it makes when it validates a prefab asset
		// in the editor. A builder's prefab starts as a plain scene object, so NGO either never hashes
		// it (batch mode) or hashes the temporary object, giving every prefab the same id. This hashes
		// the saved asset's GUID and file id instead, which are unique and the same on every machine.
		private static void GenerateObjectId(NetworkObject networkObject)
		{
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(networkObject, out var guid, out long localId);

			var assetId = $"{guid}-{localId}";

			var hash = (uint)typeof(NetworkObject).Assembly
				.GetType("Unity.Netcode.XXHash")
				.GetMethod("Hash32", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null)
				.Invoke(null, new object[] { assetId });

			typeof(NetworkObject)
				.GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(networkObject, hash);

			EditorUtility.SetDirty(networkObject);
		}

		// Every prefab with a NetworkObject, so the arena's and the lobby's are all included
		private static NetworkPrefabsList CreateNetworkPrefabsList()
		{
			var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);

			if (list == null)
			{
				list = ScriptableObject.CreateInstance<NetworkPrefabsList>();

				AssetDatabase.CreateAsset(list, NetworkPrefabsListPath);
			}

			foreach (var existing in list.PrefabList.ToList())
				list.Remove(existing);

			var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
				.Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
				.Where(prefab => prefab != null && prefab.GetComponent<NetworkObject>() != null);

			foreach (var prefab in prefabs)
			{
				GenerateObjectId(prefab.GetComponent<NetworkObject>());

				list.Add(new NetworkPrefab { Prefab = prefab });
			}

			EditorUtility.SetDirty(list);

			AssetDatabase.SaveAssets();

			return list;
		}
	}
}
