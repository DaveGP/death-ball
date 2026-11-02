using System.Collections.Generic;
using Cameras;
using Character.Actions;
using Character.Movement;
using Character.Networking;
using Character.Visuals;
using Data.Enums;
using Data.ScriptableObjects;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using Match;
using Networking;
using Players;
using SystemManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;


namespace Builders
{
	/// <summary>
	/// Builds the grey-box arena scene and character prefab from scratch. Run again after
	/// changing it; it overwrites the scene, prefab, stats and materials it made before.
	/// </summary>
	public static class ArenaSceneBuilder
	{
		private const string ScenePath = "Assets/Scenes/Arena.unity";

		private const string PrefabPath = "Assets/Prefabs/Character.prefab";

		private const string StatsPath = "Assets/ScriptableObjects/DefaultCharacterStats.asset";

		private const string MaterialsFolder = "Assets/Materials";

		private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

		private const float ArenaWidth = 30f;

		private const float ArenaLength = 50f;

		private const int PlayersPerTeam = 4;

		[MenuItem("Death Ball/Build Arena Scene")]
		public static void Build()
		{
			EnsureLayer("Ground");
			EnsureLayer("Character");

			var stats = CreateStats();

			var prefab = CreateCharacterPrefab(stats);

			CreateScene(prefab);

			AssetDatabase.SaveAssets();

			Debug.Log("ArenaSceneBuilder: Arena scene built.");
		}

		private static CharacterStats CreateStats()
		{
			EnsureFolder("Assets/ScriptableObjects");

			var stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(StatsPath);

			if (stats != null)
				return stats;

			stats = ScriptableObject.CreateInstance<CharacterStats>();

			AssetDatabase.CreateAsset(stats, StatsPath);

			return stats;
		}

		private static GameObject CreateCharacterPrefab(CharacterStats stats)
		{
			EnsureFolder("Assets/Prefabs");

			var root = new GameObject("Character")
			{
				layer = LayerMask.NameToLayer("Character")
			};

			var rigidbody = root.AddComponent<Rigidbody>();
			rigidbody.mass = 80f;
			rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
			rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
			rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

			var capsule = root.AddComponent<CapsuleCollider>();
			capsule.height = 2f;
			capsule.radius = 0.4f;
			capsule.center = Vector3.up;

			var networkObject = root.AddComponent<NetworkObject>();
			SetSerializedBool(networkObject, "_preventDespawnOnDisconnect", true);

			root.AddComponent<NetworkTransform>();

			var movement = root.AddComponent<MovementController>();
			SetSerializedReference(movement, "stats", stats);

			root.AddComponent<ActionController>();
			root.AddComponent<CharacterMotor>();
			root.AddComponent<CharacterNetworkController>();

			var material = CreateMaterial("Character", Color.grey);

			var bodyVisual = CreateVisual(PrimitiveType.Capsule, "Body", root.transform, Vector3.up, new Vector3(0.8f, 1f, 0.8f), material);

			// Shows which way the body is facing
			var noseVisual = CreateVisual(PrimitiveType.Cube, "Facing", root.transform, new Vector3(0f, 1.5f, 0.4f), new Vector3(0.3f, 0.2f, 0.3f), material);

			var teamColour = root.AddComponent<TeamColour>();
			SetSerializedArray(teamColour, "renderers", new Object[] { bodyVisual, noseVisual });

			var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

			Object.DestroyImmediate(root);

			return prefab;
		}

		private static Renderer CreateVisual(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
		{
			var visual = GameObject.CreatePrimitive(type);

			visual.name = name;
			visual.layer = parent.gameObject.layer;
			visual.transform.SetParent(parent, false);
			visual.transform.localPosition = position;
			visual.transform.localScale = scale;

			Object.DestroyImmediate(visual.GetComponent<Collider>());

			var renderer = visual.GetComponent<Renderer>();
			renderer.sharedMaterial = material;

			return renderer;
		}

		private static void CreateScene(GameObject characterPrefab)
		{
			EnsureFolder("Assets/Scenes");

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

			CreateLighting();

			CreateArena();

			var networkManagerObject = new GameObject("NetworkManager");
			var networkManager = networkManagerObject.AddComponent<NetworkManager>();
			networkManagerObject.AddComponent<NetworkStarter>();

			// FishNet keeps this collection up to date with every NetworkObject prefab in the project
			SetSerializedReference(networkManager, "_spawnablePrefabs", AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath));

			var home = CreateTeam(TeamSide.Home, characterPrefab, -1f);
			var away = CreateTeam(TeamSide.Away, characterPrefab, 1f);
			SetSerializedReference(home, "opponents", away);
			SetSerializedReference(away, "opponents", home);

			var match = new GameObject("Match").AddComponent<MatchController>();
			SetSerializedReference(match, "networkManager", networkManager);
			SetSerializedArray(match, "teams", new Object[] { home, away });

			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			cameraObject.AddComponent<Camera>();
			cameraObject.AddComponent<AudioListener>();
			var arenaCamera = cameraObject.AddComponent<ArenaCamera>();

			var players = new GameObject("Local Players").AddComponent<LocalPlayerManager>();
			SetSerializedReference(players, "inputView", arenaCamera);

			new GameObject("Systems").AddComponent<SystemsRunner>();

			EditorSceneManager.SaveScene(scene, ScenePath);

			AddSceneToBuild();
		}

		private static void CreateLighting()
		{
			var light = new GameObject("Directional Light").AddComponent<Light>();
			light.type = LightType.Directional;
			light.shadows = LightShadows.Soft;
			light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f);
		}

		private static void CreateArena()
		{
			var arena = new GameObject("Arena");

			var ground = LayerMask.NameToLayer("Ground");

			var floorMaterial = CreateMaterial("Floor", new Color(0.25f, 0.25f, 0.27f));
			var wallMaterial = CreateMaterial("Wall", new Color(0.45f, 0.45f, 0.5f));
			var lineMaterial = CreateMaterial("Line", new Color(0.8f, 0.8f, 0.2f));

			CreateBlock("Floor", arena.transform, new Vector3(0f, -0.5f, 0f), new Vector3(ArenaWidth, 1f, ArenaLength), floorMaterial, ground);
			CreateBlock("Halfway Line", arena.transform, new Vector3(0f, 0.005f, 0f), new Vector3(ArenaWidth, 0.01f, 0.2f), lineMaterial, ground, false);

			const float wallHeight = 3f;
			const float wallThickness = 1f;

			CreateBlock("Wall Left", arena.transform, new Vector3(-(ArenaWidth + wallThickness) / 2f, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, ArenaLength + wallThickness * 2f), wallMaterial, 0);
			CreateBlock("Wall Right", arena.transform, new Vector3((ArenaWidth + wallThickness) / 2f, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, ArenaLength + wallThickness * 2f), wallMaterial, 0);
			CreateBlock("Wall Home", arena.transform, new Vector3(0f, wallHeight / 2f, -(ArenaLength + wallThickness) / 2f), new Vector3(ArenaWidth, wallHeight, wallThickness), wallMaterial, 0);
			CreateBlock("Wall Away", arena.transform, new Vector3(0f, wallHeight / 2f, (ArenaLength + wallThickness) / 2f), new Vector3(ArenaWidth, wallHeight, wallThickness), wallMaterial, 0);
		}

		private static void CreateBlock(string name, Transform parent, Vector3 position, Vector3 scale, Material material, int layer, bool collider = true)
		{
			var block = GameObject.CreatePrimitive(PrimitiveType.Cube);

			block.name = name;
			block.layer = layer;
			block.isStatic = true;
			block.transform.SetParent(parent, false);
			block.transform.localPosition = position;
			block.transform.localScale = scale;
			block.GetComponent<Renderer>().sharedMaterial = material;

			if (!collider)
				Object.DestroyImmediate(block.GetComponent<Collider>());
		}

		// Side is -1 for the home end of the arena and 1 for the away end
		private static TeamController CreateTeam(TeamSide side, GameObject characterPrefab, float end)
		{
			var teamObject = new GameObject($"Team {side}");

			var team = teamObject.AddComponent<TeamController>();

			var spawnPoints = new List<Object>();

			for (var i = 0; i < PlayersPerTeam; i++)
			{
				var spawnPoint = new GameObject($"Spawn Point {i + 1}").transform;

				var x = (i - (PlayersPerTeam - 1) / 2f) * 4f;

				spawnPoint.SetParent(teamObject.transform, false);
				spawnPoint.position = new Vector3(x, 0.05f, end * ArenaLength * 0.25f);
				spawnPoint.rotation = Quaternion.LookRotation(Vector3.forward * -end);

				spawnPoints.Add(spawnPoint);
			}

			var serializedTeam = new SerializedObject(team);
			serializedTeam.FindProperty("side").enumValueIndex = (int)side;
			serializedTeam.FindProperty("characterPrefab").objectReferenceValue = characterPrefab.GetComponent<NetworkObject>();
			serializedTeam.ApplyModifiedPropertiesWithoutUndo();

			SetSerializedArray(team, "spawnPoints", spawnPoints.ToArray());

			return team;
		}

		private static Material CreateMaterial(string name, Color colour)
		{
			EnsureFolder(MaterialsFolder);

			var path = $"{MaterialsFolder}/{name}.mat";

			var material = AssetDatabase.LoadAssetAtPath<Material>(path);

			if (material == null)
			{
				material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

				AssetDatabase.CreateAsset(material, path);
			}

			material.SetColor("_BaseColor", colour);

			EditorUtility.SetDirty(material);

			return material;
		}

		private static void AddSceneToBuild()
		{
			var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

			scenes.RemoveAll(s => s.path == ScenePath || !System.IO.File.Exists(s.path));

			scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

			EditorBuildSettings.scenes = scenes.ToArray();
		}

		private static void EnsureLayer(string layerName)
		{
			if (LayerMask.NameToLayer(layerName) >= 0)
				return;

			var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

			var layers = tagManager.FindProperty("layers");

			for (var i = 8; i < layers.arraySize; i++)
			{
				var layer = layers.GetArrayElementAtIndex(i);

				if (!string.IsNullOrEmpty(layer.stringValue))
					continue;

				layer.stringValue = layerName;

				tagManager.ApplyModifiedPropertiesWithoutUndo();

				return;
			}

			Debug.LogError($"ArenaSceneBuilder: No free layer for {layerName}");
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;

			var slash = path.LastIndexOf('/');

			AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
		}

		private static void SetSerializedReference(Object target, string property, Object value)
		{
			var serializedObject = new SerializedObject(target);
			serializedObject.FindProperty(property).objectReferenceValue = value;
			serializedObject.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetSerializedBool(Object target, string property, bool value)
		{
			var serializedObject = new SerializedObject(target);
			serializedObject.FindProperty(property).boolValue = value;
			serializedObject.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetSerializedArray(Object target, string property, Object[] values)
		{
			var serializedObject = new SerializedObject(target);
			var array = serializedObject.FindProperty(property);

			array.arraySize = values.Length;

			for (var i = 0; i < values.Length; i++)
				array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

			serializedObject.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
