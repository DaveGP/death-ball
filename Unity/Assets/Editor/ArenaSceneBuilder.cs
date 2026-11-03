using System.Collections.Generic;
using System.Linq;
using Cameras;
using Character.Actions;
using Character.Health;
using Character.Specials;
using Character.Stamina;
using Character.Movement;
using Character.Networking;
using Character.Visuals;
using Data.Enums;
using Data.ScriptableObjects;
using Hazards;
using Hud;
using Match;
using Networking;
using Players;
using SystemManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;


namespace Builders
{
	/// <summary>
	/// Builds the grey-box arena scene and character prefab from scratch. Run again after
	/// changing it; it overwrites the scene, prefab, stats and materials it made before.
	/// </summary>
	public static class ArenaSceneBuilder
	{
		public const string ScenePath = "Assets/Scenes/Arena.unity";

		private const string CharacterPrefabsFolder = "Assets/Prefabs/Characters";

		private const string BallPrefabPath = "Assets/Prefabs/Ball.prefab";

		private const string BallPhysicsMaterialPath = "Assets/Materials/BallPhysics.asset";

		private const string MatchStatePrefabPath = "Assets/Prefabs/MatchState.prefab";

		// Goal rings stand inset from each end wall, facing the field
		private const float GoalInset = 3f;

		private const float GoalHeight = 2f;

		// The rim sits just outside the scoring radius, so the ball can clip it and bounce out
		private const float GoalScoringRadius = 1f;

		private const float GoalRimRadius = 1.15f;

		private const int GoalRimSegments = 16;

		private const string StatsPath = "Assets/ScriptableObjects/DefaultCharacterStats.asset";

		private const string TypeStatsFolder = "Assets/ScriptableObjects/Stats";

		private const string SpecialsFolder = "Assets/ScriptableObjects/Specials";

		private const string MaterialsFolder = "Assets/Materials";



		private const float ArenaWidth = 30f;

		private const float ArenaLength = 50f;

		private const int PlayersPerTeam = 4;

		[MenuItem("Death Ball/Build Arena Scene")]
		public static void Build()
		{
			EnsureLayer("Ground");
			EnsureLayer("Character");
			EnsureLayer("Ball");

			var defaultStats = CreateStats();

			var lineup = new List<GameObject>();

			foreach (var playerType in PlayerTypes)
				lineup.Add(CreateCharacterPrefab(playerType, CreateTypeStats(playerType, defaultStats)));

			var ballPrefab = CreateBallPrefab();

			var matchStatePrefab = CreateMatchStatePrefab();

			// After the networked prefabs, so its list of them is up to date
			NetworkManagerBuilder.Build();

			CreateScene(lineup, ballPrefab, matchStatePrefab);

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

		// Each type's stats start as a copy of the defaults with the type's changes, made once
		// and then left alone, so they can be tuned in the inspector
		private static CharacterStats CreateTypeStats(PlayerTypeSpec playerType, CharacterStats defaultStats)
		{
			EnsureFolder(TypeStatsFolder);

			var path = $"{TypeStatsFolder}/{playerType.Name}.asset";

			var stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);

			if (stats != null)
				return stats;

			stats = Object.Instantiate(defaultStats);

			playerType.TuneStats(stats);

			AssetDatabase.CreateAsset(stats, path);

			return stats;
		}

		private static GameObject CreateCharacterPrefab(PlayerTypeSpec playerType, CharacterStats stats)
		{
			EnsureFolder(CharacterPrefabsFolder);

			var root = new GameObject(playerType.Name)
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

			root.AddComponent<NetworkObject>();

			// Whoever owns the body moves it: a remote player's machine, or the server for everyone else
			var networkTransform = root.AddComponent<NetworkTransform>();
			networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
			networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;

			// Makes the copies on machines that don't move the body kinematic, so they only follow
			root.AddComponent<NetworkRigidbody>();

			var movement = root.AddComponent<MovementController>();
			SetSerializedReference(movement, "stats", stats);

			root.AddComponent<ActionController>();
			root.AddComponent<CharacterMotor>();
			var networkController = root.AddComponent<CharacterNetworkController>();

			var serializedController = new SerializedObject(networkController);
			serializedController.FindProperty("playerType").enumValueIndex = (int)playerType.Type;
			serializedController.ApplyModifiedPropertiesWithoutUndo();

			var health = root.AddComponent<CharacterHealth>();
			SetSerializedReference(health, "stats", stats);

			var stamina = root.AddComponent<CharacterStamina>();
			SetSerializedReference(stamina, "stats", stats);

			var special = root.AddComponent<CharacterSpecial>();
			SetSerializedReference(special, "stats", stats);
			SetSerializedReference(special, "move", playerType.CreateSpecial());

			var material = CreateMaterial("Character", Color.grey);

			var visuals = new List<Object>
			{
				CreateVisual(PrimitiveType.Capsule, "Body", root.transform, Vector3.up, playerType.BodyScale, material),

				// Shows which way the body is facing
				CreateVisual(PrimitiveType.Cube, "Facing", root.transform, new Vector3(0f, 1.5f, playerType.BodyScale.z / 2f), new Vector3(0.3f, 0.2f, 0.3f), material)
			};

			if (playerType.Shield)
				visuals.Add(CreateVisual(PrimitiveType.Cube, "Shield", root.transform, new Vector3(0f, 1.05f, playerType.BodyScale.z / 2f + 0.08f), new Vector3(0.75f, 0.9f, 0.1f), material));

			var teamColour = root.AddComponent<TeamColour>();
			SetSerializedArray(teamColour, "renderers", visuals.ToArray());

			CreateStatusBars(root.transform);

			var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{CharacterPrefabsFolder}/{playerType.Name}.prefab");

			Object.DestroyImmediate(root);

			return prefab;
		}

		// Health on top, with thinner stamina and special bars under it
		private static void CreateStatusBars(Transform parent)
		{
			const float width = 1f;
			const float border = 0.03f;

			var bars = new GameObject("Status Bars").transform;
			bars.SetParent(parent, false);
			bars.localPosition = Vector3.up * 2.4f;

			var background = CreateMaterial("Health Bar Background", new Color(0.05f, 0.05f, 0.05f), unlit: true);
			var fillMaterial = CreateMaterial("Health Bar Fill", Color.white, unlit: true);

			var rows = new (string Name, string Property, float Height)[]
			{
				("Health", "health", 0.12f),
				("Stamina", "stamina", 0.05f),
				("Special", "special", 0.05f),
			};

			var totalHeight = border;

			foreach (var row in rows)
				totalHeight += row.Height + border;

			CreateVisual(PrimitiveType.Quad, "Background", bars, Vector3.down * (totalHeight / 2f - 0.06f - border), new Vector3(width + border * 2f, totalHeight, 1f), background);

			var statusBars = bars.gameObject.AddComponent<StatusBars>();

			var serializedBars = new SerializedObject(statusBars);

			var top = 0.06f;

			foreach (var row in rows)
			{
				// Just in front of the background, towards the camera
				var fill = CreateVisual(PrimitiveType.Quad, row.Name, bars, new Vector3(0f, top - row.Height / 2f, -0.01f), new Vector3(width, row.Height, 1f), fillMaterial);

				serializedBars.FindProperty($"{row.Property}.Fill").objectReferenceValue = fill.transform;
				serializedBars.FindProperty($"{row.Property}.Renderer").objectReferenceValue = fill;

				top -= row.Height + border;
			}

			serializedBars.ApplyModifiedPropertiesWithoutUndo();
		}

		// Made once, then left alone for tuning
		private static SpecialMove CreateSpecial<T>(string name, Color effectColour, float effectRadius) where T : SpecialMove
		{
			EnsureFolder(SpecialsFolder);

			var path = $"{SpecialsFolder}/{name.Replace(" ", "")}.asset";

			var special = AssetDatabase.LoadAssetAtPath<T>(path);

			if (special != null)
				return special;

			special = ScriptableObject.CreateInstance<T>();
			special.DisplayName = name;
			special.EffectColour = effectColour;
			special.EffectRadius = effectRadius;

			AssetDatabase.CreateAsset(special, path);

			return special;
		}

		private sealed class PlayerTypeSpec
		{
			public PlayerType Type;

			public string Name => Type.ToString();

			public System.Action<CharacterStats> TuneStats;

			public System.Func<SpecialMove> CreateSpecial;

			public Vector3 BodyScale;

			public bool Shield;
		}

		// One of each per team, in spawn point order from left to right
		private static readonly PlayerTypeSpec[] PlayerTypes =
		{
			new()
			{
				Type = PlayerType.Bruiser,
				BodyScale = new Vector3(1f, 1.05f, 1f),
				CreateSpecial = () => CreateSpecial<SlamSpecial>("Slam", new Color(1f, 0.6f, 0.1f), 3.5f),
				TuneStats = stats =>
				{
					stats.MaxHealth = 240;
					stats.RunSpeed = 4.8f;
					stats.SprintSpeed = 7f;
					stats.MaxStamina = 80f;
					stats.MeleeDamageMultiplier = 1.3f;
					stats.ThrowSpeedMultiplier = 0.9f;
				}
			},
			new()
			{
				Type = PlayerType.Runner,
				BodyScale = new Vector3(0.6f, 1.05f, 0.6f),
				CreateSpecial = () => CreateSpecial<SecondWindSpecial>("Second Wind", new Color(0.3f, 1f, 0.4f), 1.5f),
				TuneStats = stats =>
				{
					stats.MaxHealth = 170;
					stats.WalkSpeed = 2.8f;
					stats.RunSpeed = 6.2f;
					stats.SprintSpeed = 9f;
					stats.MaxStamina = 130f;
					stats.StaminaRegen = 32f;
					stats.DodgeStaminaCost = 15f;
					stats.MeleeDamageMultiplier = 0.85f;
				}
			},
			new()
			{
				Type = PlayerType.Thrower,
				BodyScale = new Vector3(0.75f, 1f, 0.75f),
				CreateSpecial = () => CreateSpecial<RocketSpecial>("Rocket", new Color(1f, 0.25f, 0.2f), 1.5f),
				TuneStats = stats =>
				{
					stats.MaxHealth = 180;
					stats.ThrowSpeedMultiplier = 1.2f;
					stats.MeleeDamageMultiplier = 0.8f;
				}
			},
			new()
			{
				Type = PlayerType.Enforcer,
				BodyScale = new Vector3(0.85f, 1f, 0.85f),
				Shield = true,
				CreateSpecial = () => CreateSpecial<IronWallSpecial>("Iron Wall", new Color(0.6f, 0.8f, 1f), 1.8f),
				TuneStats = stats =>
				{
					stats.MaxHealth = 220;
					stats.RunSpeed = 5.2f;
					stats.SprintSpeed = 7.6f;
					stats.BlockedDamageMultiplier = 0.05f;
					stats.BlockStaminaPerDamage = 0.6f;
				}
			},
		};

		private static GameObject CreateBallPrefab()
		{
			var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);

			root.name = "Ball";
			root.layer = LayerMask.NameToLayer("Ball");
			root.transform.localScale = Vector3.one * 0.5f;
			root.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Ball", new Color(0.75f, 0.75f, 0.8f), 0.9f);
			root.GetComponent<Collider>().sharedMaterial = CreateBallPhysicsMaterial();

			var rigidbody = root.AddComponent<Rigidbody>();
			rigidbody.mass = 4f;
			rigidbody.linearDamping = 0.02f;
			rigidbody.angularDamping = 0.5f;
			rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
			rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

			root.AddComponent<NetworkObject>();

			var networkTransform = root.AddComponent<NetworkTransform>();
			networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;

			root.AddComponent<NetworkRigidbody>();
			root.AddComponent<Ball>();

			var prefab = PrefabUtility.SaveAsPrefabAsset(root, BallPrefabPath);

			Object.DestroyImmediate(root);

			return prefab;
		}

		private static GameObject CreateMatchStatePrefab()
		{
			var root = new GameObject("MatchState");

			root.AddComponent<NetworkObject>();
			root.AddComponent<MatchState>();

			var prefab = PrefabUtility.SaveAsPrefabAsset(root, MatchStatePrefabPath);

			Object.DestroyImmediate(root);

			return prefab;
		}

		private static PhysicsMaterial CreateBallPhysicsMaterial()
		{
			var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BallPhysicsMaterialPath);

			if (material == null)
			{
				material = new PhysicsMaterial("Ball");

				AssetDatabase.CreateAsset(material, BallPhysicsMaterialPath);
			}

			material.bounciness = 0.5f;
			material.bounceCombine = PhysicsMaterialCombine.Maximum;
			material.dynamicFriction = 0.4f;
			material.staticFriction = 0.4f;

			EditorUtility.SetDirty(material);

			return material;
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

		private static void CreateScene(List<GameObject> lineup, GameObject ballPrefab, GameObject matchStatePrefab)
		{
			EnsureFolder("Assets/Scenes");

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

			CreateLighting();

			CreateArena();

			CreateHazards();

			// Makes the session's NetworkManager if the arena is played directly, and starts local play
			var starter = new GameObject("Network Starter").AddComponent<NetworkStarter>();
			SetSerializedReference(starter, "networkManagerPrefab", NetworkManagerBuilder.LoadPrefab());
			SetSerializedBool(starter, "startLocalIfNotConnected", true);

			var home = CreateTeam(TeamSide.Home, lineup, -1f);
			var away = CreateTeam(TeamSide.Away, lineup, 1f);
			SetSerializedReference(home, "opponents", away);
			SetSerializedReference(away, "opponents", home);

			// Each team shoots at the goal at the other team's end
			SetSerializedReference(home, "attackGoal", CreateGoal(TeamSide.Away, 1f));
			SetSerializedReference(away, "attackGoal", CreateGoal(TeamSide.Home, -1f));

			var ballSpawn = new GameObject("Ball Spawn").transform;
			ballSpawn.position = Vector3.up;

			var match = new GameObject("Match").AddComponent<MatchController>();
			SetSerializedArray(match, "teams", new Object[] { home, away });
			SetSerializedReference(match, "ballPrefab", ballPrefab.GetComponent<NetworkObject>());
			SetSerializedReference(match, "ballSpawn", ballSpawn);
			SetSerializedReference(match, "matchStatePrefab", matchStatePrefab.GetComponent<NetworkObject>());

			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			cameraObject.AddComponent<Camera>();
			cameraObject.AddComponent<AudioListener>();
			cameraObject.AddComponent<ArenaCamera>();

			// Lets the arena be played directly; when loaded from the menu, the menu's copy is kept instead
			new GameObject("Local Players").AddComponent<LocalPlayerManager>();

			new GameObject("Systems").AddComponent<SystemsRunner>();

			CreateHud();

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

		// Hazards on staggered cycles so they don't all go off together
		private static void CreateHazards()
		{
			var hazards = new GameObject("Hazards").transform;

			var hazardMaterial = CreateMaterial("Hazard", Color.white);

			// Electrified panels on both side walls, either side of halfway
			const float panelLength = 8f;
			const float panelHeight = 2.5f;

			var panelIndex = 0;

			foreach (var side in new[] { -1f, 1f })
			{
				foreach (var z in new[] { -10f, 10f })
				{
					var panel = new GameObject($"Electric Wall {panelIndex + 1}").transform;
					panel.SetParent(hazards, false);
					panel.SetPositionAndRotation(
						new Vector3(side * (ArenaWidth / 2f - 0.05f), panelHeight / 2f, z),
						Quaternion.LookRotation(Vector3.right * -side));

					var visual = CreateVisual(PrimitiveType.Cube, "Panel", panel, Vector3.zero, new Vector3(panelLength, panelHeight, 0.15f), hazardMaterial);

					var wall = panel.gameObject.AddComponent<ElectricWall>();
					SetHazard(wall, period: 12f, warning: 2f, active: 3f, offset: panelIndex * 3f, visual,
						new Color(0.2f, 0.25f, 0.35f), new Color(0.4f, 0.8f, 1f), new Color(0.6f, 0.95f, 1f));

					var serialized = new SerializedObject(wall);
					serialized.FindProperty("size").vector2Value = new Vector2(panelLength, panelHeight);
					serialized.ApplyModifiedPropertiesWithoutUndo();

					panelIndex++;
				}
			}

			// Floor vents in the wide lanes of each half
			const float ventSize = 3f;

			var blastMaterial = CreateTransparentMaterial("Vent Blast", new Color(1f, 0.45f, 0.1f, 0.45f));

			var ventIndex = 0;

			foreach (var x in new[] { -9f, 9f })
			{
				foreach (var z in new[] { -15f, 15f })
				{
					var vent = new GameObject($"Floor Vent {ventIndex + 1}").transform;
					vent.SetParent(hazards, false);
					vent.position = new Vector3(x, 0.01f, z);

					var grate = CreateVisual(PrimitiveType.Cube, "Grate", vent, Vector3.zero, new Vector3(ventSize, 0.02f, ventSize), hazardMaterial);

					var blast = CreateVisual(PrimitiveType.Cube, "Blast", vent, Vector3.up * 1.5f, new Vector3(ventSize, 3f, ventSize), blastMaterial);
					blast.gameObject.SetActive(false);

					var floorVent = vent.gameObject.AddComponent<FloorVent>();
					SetHazard(floorVent, period: 10f, warning: 1.5f, active: 1f, offset: ventIndex * 2.5f, grate,
						new Color(0.2f, 0.2f, 0.2f), new Color(1f, 0.6f, 0.1f), new Color(1f, 0.3f, 0.05f));

					SetSerializedReference(floorVent, "blast", blast.gameObject);

					ventIndex++;
				}
			}

			// Barriers either side of halfway, taking turns to rise
			var barrierIndex = 0;

			foreach (var x in new[] { -9f, 9f })
			{
				var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
				barrier.name = $"Rising Barrier {barrierIndex + 1}";
				barrier.transform.SetParent(hazards, false);
				barrier.transform.position = new Vector3(x, 1f, 0f);
				barrier.transform.localScale = new Vector3(2f, 2f, 2f);
				barrier.GetComponent<Renderer>().sharedMaterial = hazardMaterial;

				barrier.AddComponent<Rigidbody>().isKinematic = true;

				var rising = barrier.AddComponent<RisingBarrier>();
				SetHazard(rising, period: 18f, warning: 1.5f, active: 8f, offset: barrierIndex * 9f, barrier.GetComponent<Renderer>(),
					new Color(0.45f, 0.45f, 0.5f), new Color(1f, 0.85f, 0.2f), new Color(0.55f, 0.55f, 0.6f));

				barrierIndex++;
			}
		}

		private static void SetHazard(Hazard hazard, float period, float warning, float active, float offset, Renderer renderer, Color off, Color warn, Color live)
		{
			var serialized = new SerializedObject(hazard);
			serialized.FindProperty("period").floatValue = period;
			serialized.FindProperty("warning").floatValue = warning;
			serialized.FindProperty("active").floatValue = active;
			serialized.FindProperty("offset").floatValue = offset;
			serialized.FindProperty("offColour").colorValue = off;
			serialized.FindProperty("warningColour").colorValue = warn;
			serialized.FindProperty("activeColour").colorValue = live;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			SetSerializedArray(hazard, "renderers", new Object[] { renderer });
		}

		private static Material CreateTransparentMaterial(string name, Color colour)
		{
			var material = CreateMaterial(name, colour, unlit: true);

			material.SetFloat("_Surface", 1f);
			material.SetFloat("_Blend", 0f);
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
			material.SetInt("_ZWrite", 0);
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.renderQueue = (int)RenderQueue.Transparent;

			EditorUtility.SetDirty(material);

			return material;
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

		// A ring on a post, facing the field. The rim is solid so near misses bounce off it.
		private static Goal CreateGoal(TeamSide owner, float end)
		{
			var goalObject = new GameObject($"Goal {owner}");

			var goalTransform = goalObject.transform;

			goalTransform.SetPositionAndRotation(
				new Vector3(0f, GoalHeight, end * (ArenaLength / 2f - GoalInset)),
				Quaternion.LookRotation(Vector3.forward * -end));

			var goal = goalObject.AddComponent<Goal>();

			var serializedGoal = new SerializedObject(goal);
			serializedGoal.FindProperty("owner").enumValueIndex = (int)owner;
			serializedGoal.FindProperty("radius").floatValue = GoalScoringRadius;
			serializedGoal.ApplyModifiedPropertiesWithoutUndo();

			var colour = owner == TeamSide.Home ? new Color(0.8f, 0.15f, 0.1f) : new Color(0.1f, 0.3f, 0.85f);

			var material = CreateMaterial($"Goal {owner}", colour);

			const float rimThickness = 0.2f;

			var segmentLength = 2f * Mathf.PI * GoalRimRadius / GoalRimSegments + 0.05f;

			for (var i = 0; i < GoalRimSegments; i++)
			{
				var angle = 360f * i / GoalRimSegments;

				var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);

				segment.name = $"Rim {i + 1}";
				segment.isStatic = true;
				segment.transform.SetParent(goalTransform, false);
				segment.transform.localPosition = Quaternion.Euler(0f, 0f, angle) * Vector3.right * GoalRimRadius;
				segment.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
				segment.transform.localScale = new Vector3(rimThickness, segmentLength, rimThickness);
				segment.GetComponent<Renderer>().sharedMaterial = material;
			}

			var rimBottom = GoalRimRadius + rimThickness / 2f;

			var postHeight = GoalHeight - rimBottom;

			var post = CreateVisual(PrimitiveType.Cylinder, "Post", goalTransform, Vector3.down * (rimBottom + postHeight / 2f), new Vector3(0.25f, postHeight / 2f, 0.25f), material);

			post.gameObject.isStatic = true;

			return goal;
		}

		private static void CreateHud()
		{
			var canvasObject = new GameObject("HUD");

			var canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var scaler = canvasObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);

			var scoreText = CreateText("Score", canvasObject.transform, 48, new Vector2(0.5f, 1f), new Vector2(0f, -50f));
			var clockText = CreateText("Clock", canvasObject.transform, 36, new Vector2(0.5f, 1f), new Vector2(0f, -105f));
			var bannerText = CreateText("Banner", canvasObject.transform, 72, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f));

			var hud = canvasObject.AddComponent<ScoreHud>();
			SetSerializedReference(hud, "scoreText", scoreText);
			SetSerializedReference(hud, "clockText", clockText);
			SetSerializedReference(hud, "bannerText", bannerText);
		}

		private static Text CreateText(string name, Transform parent, int fontSize, Vector2 anchor, Vector2 position)
		{
			var textObject = new GameObject(name);

			var rectTransform = textObject.AddComponent<RectTransform>();
			rectTransform.SetParent(parent, false);
			rectTransform.anchorMin = anchor;
			rectTransform.anchorMax = anchor;
			rectTransform.anchoredPosition = position;
			rectTransform.sizeDelta = new Vector2(1200f, fontSize * 2.5f);

			var text = textObject.AddComponent<Text>();
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.fontSize = fontSize;
			text.fontStyle = FontStyle.Bold;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = Color.white;
			text.raycastTarget = false;

			textObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

			return text;
		}

		// Side is -1 for the home end of the arena and 1 for the away end
		private static TeamController CreateTeam(TeamSide side, List<GameObject> lineup, float end)
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
			serializedTeam.ApplyModifiedPropertiesWithoutUndo();

			SetSerializedArray(team, "lineup", lineup.Select(prefab => (Object)prefab.GetComponent<NetworkObject>()).ToArray());

			SetSerializedArray(team, "spawnPoints", spawnPoints.ToArray());

			return team;
		}

		private static Material CreateMaterial(string name, Color colour, float smoothness = 0.5f, bool unlit = false)
		{
			EnsureFolder(MaterialsFolder);

			var path = $"{MaterialsFolder}/{name}.mat";

			var material = AssetDatabase.LoadAssetAtPath<Material>(path);

			if (material == null)
			{
				material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));

				AssetDatabase.CreateAsset(material, path);
			}

			material.SetColor("_BaseColor", colour);
			material.SetFloat("_Smoothness", smoothness);

			EditorUtility.SetDirty(material);

			return material;
		}

		private static void AddSceneToBuild()
		{
			var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

			scenes.RemoveAll(s => s.path == ScenePath || !System.IO.File.Exists(s.path));

			// After the main menu if there is one, which comes first
			var menuIndex = scenes.FindIndex(s => s.path == MainMenuSceneBuilder.ScenePath);

			scenes.Insert(menuIndex + 1, new EditorBuildSettingsScene(ScenePath, true));

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

		internal static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;

			var slash = path.LastIndexOf('/');

			AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
		}

		internal static void SetSerializedReference(Object target, string property, Object value)
		{
			var serializedObject = new SerializedObject(target);
			serializedObject.FindProperty(property).objectReferenceValue = value;
			serializedObject.ApplyModifiedPropertiesWithoutUndo();
		}

		internal static void SetSerializedBool(Object target, string property, bool value)
		{
			var serializedObject = new SerializedObject(target);
			serializedObject.FindProperty(property).boolValue = value;
			serializedObject.ApplyModifiedPropertiesWithoutUndo();
		}

		internal static void SetSerializedArray(Object target, string property, Object[] values)
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
