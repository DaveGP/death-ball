using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arenas;
using Hazards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace Builders
{
	/// <summary>
	/// Makes The Night Market, the Ninth Life's home ground (see Documentation/Teams.md), by reskinning a copy of
	/// the Arena scene. Everything that plays stays as the Arena has it: only materials and hazard colours change
	/// inside the walls, and scenery goes outside them with its colliders removed.
	/// The scene uses the Synty packs, so like them it stays out of git, and this tool is how another machine with
	/// the packs gets it. The camera looks north at a fixed 55 degrees and never sees the sky, so what's south of the
	/// pitch has to stay low, and the skyline and dragon are placed where that view can actually reach them.
	/// </summary>
	public static class NightMarketBuilder
	{
		public const string Folder = "Assets/ThirdParty/Synty/DeathBallArenas/NightMarket";

		public const string ScenePath = Folder + "/NightMarket.unity";

		private const string Pack = "Assets/ThirdParty/Synty/PolygonCyberCity";

		private const float WallTop = 3f;

		// The outside faces of the Arena's walls
		private const float EastWest = 16f;

		private const float NorthSouth = 26f;

		// Canopies hang above the top of the walls, so they read over them from the camera
		private const float CanopyHeight = 3.6f;

		private static readonly Color Yellow = new(1f, 0.74f, 0.16f);

		private static readonly Color Cyan = new(0.18f, 0.86f, 0.95f);

		private static readonly Dictionary<string, (float X, float Z)[]> CanopyTips = new()
		{
			["SM_Prop_Canopy_01"] = new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) },
			["SM_Prop_Canopy_02"] = new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) },
			["SM_Prop_Canopy_03"] = new[] { (-1f, 1f), (0f, 1f), (1f, 1f), (-0.5f, -1f), (0.5f, -1f) },
			["SM_Prop_Canopy_04"] = new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) },
			["SM_Prop_Canopy_05"] = new[] { (0f, 1f), (-1f, -1f), (1f, -1f) },
			["SM_Prop_Canopy_06"] = new[] { (0f, 1f), (-1f, -1f), (1f, -1f) },
		};

		// The spare characters from the teams, with fewer triangles than the rest
		private static readonly string[] CrowdModels =
		{
			"SM_Chr_Street_Male_01", "SM_Chr_Rich_Female_01", "SM_Chr_Robot_01", "SM_Chr_Racer_Female_01", "SM_Chr_Rich_Male_01", "SM_Chr_Cyborg_Female_01",
		};

		private static readonly Dictionary<string, GameObject> Prefabs = new();

		private static Transform scenery;

		[MenuItem("Death Ball/Build Night Market Arena")]
		public static void Build()
		{
			if (EditorApplication.isPlaying || !AssetDatabase.IsValidFolder(Pack))
			{
				Debug.LogError($"{nameof(NightMarketBuilder)}: stop playing, and check the Cyber City pack is in {Pack}");
				return;
			}

			var previousScene = EditorSceneManager.GetActiveScene().path;

			EnsureFolder(Folder);
			EnsureFolder($"{Folder}/Materials");
			EnsureFolder($"{Folder}/Textures");

			Prefabs.Clear();

			AssetDatabase.DeleteAsset(ScenePath);

			if (!AssetDatabase.CopyAsset(ArenaSceneBuilder.ScenePath, ScenePath))
			{
				Debug.LogError($"{nameof(NightMarketBuilder)}: couldn't copy {ArenaSceneBuilder.ScenePath}");
				return;
			}

			var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

			ReskinArena();
			ReskinGoals();
			ReskinHazards();

			scenery = new GameObject("Scenery").transform;

			BuildRailway();
			BuildStalls();
			BuildSkyline();
			BuildSigns();

			// Shadows on the dark ground out here barely show, and would double what the scenery costs to draw
			foreach (var renderer in scenery.GetComponentsInChildren<Renderer>(true))
				renderer.shadowCastingMode = ShadowCastingMode.Off;

			MakeHologramsWorkInBrowsers(scene);

			SetLighting();

			EditorSceneManager.SaveScene(scene);
			AssetDatabase.SaveAssets();

			AddToBuildSettings();

			if (!string.IsNullOrEmpty(previousScene))
				EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

			Debug.Log($"{nameof(NightMarketBuilder)}: built {ScenePath}");
		}

		// The pack's hologram shader asks for shader model 4.5, which WebGL doesn't have, so browsers draw
		// it solid magenta. It doesn't use anything that needs 4.5, so holograms here use a copy that asks
		// for less. The pack's own files stay as they shipped.
		private static void MakeHologramsWorkInBrowsers(UnityEngine.SceneManagement.Scene scene)
		{
			var packShader = Shader.Find("SyntyStudios_Hologram_01");

			if (packShader == null)
				return;

			var shaderPath = $"{Folder}/Hologram.shader";

			var source = System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(packShader))
				.Replace("Shader \"SyntyStudios_Hologram_01\"", "Shader \"DeathBall/Hologram\"")
				.Replace("#pragma target 4.5", "#pragma target 3.5");

			System.IO.File.WriteAllText(shaderPath, source);

			AssetDatabase.ImportAsset(shaderPath);

			var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

			var copies = new Dictionary<Material, Material>();

			foreach (var root in scene.GetRootGameObjects())
			{
				foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
				{
					var materials = renderer.sharedMaterials;

					var changed = false;

					for (var i = 0; i < materials.Length; i++)
					{
						if (materials[i] == null || materials[i].shader != packShader)
							continue;

						if (!copies.TryGetValue(materials[i], out var copy))
							copies[materials[i]] = copy = HologramCopy(materials[i], shader);

						materials[i] = copy;

						changed = true;
					}

					if (changed)
						renderer.sharedMaterials = materials;
				}
			}
		}

		private static Material HologramCopy(Material original, Shader shader)
		{
			var path = $"{Folder}/Materials/{original.name}.mat";

			AssetDatabase.DeleteAsset(path);

			var copy = new Material(original) { shader = shader };

			AssetDatabase.CreateAsset(copy, path);

			return copy;
		}

		// The game can only load a scene that's in the list
		private static void AddToBuildSettings()
		{
			var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

			if (scenes.TrueForAll(s => s.path != ScenePath))
				scenes.Add(new EditorBuildSettingsScene(ScenePath, true));

			EditorBuildSettings.scenes = scenes.ToArray();
		}

		private static void ReskinArena()
		{
			var tiles = TilesTexture();
			var wet = Tiled(MatteMaterial("Wet Paving", new Color(0.14f, 0.15f, 0.2f)), tiles, new Vector2(7.5f, 12.5f));
			var outside = Tiled(MatteMaterial("Market Ground", new Color(0.08f, 0.09f, 0.13f)), tiles, new Vector2(80f, 80f));
			var wall = MatteMaterial("Market Wall", new Color(0.1f, 0.11f, 0.16f));
			var water = LitMaterial("Puddle", new Color(0.015f, 0.04f, 0.07f), smoothness: 0.96f);
			var line = EmissiveMaterial("Neon Yellow", Yellow, 2.2f);
			var neonCyan = EmissiveMaterial("Neon Cyan", Cyan, 2.2f);

			Find("Floor").GetComponent<Renderer>().sharedMaterial = wet;
			Find("Halfway Line").GetComponent<Renderer>().sharedMaterial = line;

			foreach (var name in new[] { "Wall Left", "Wall Right", "Wall Home", "Wall Away" })
				Find(name).GetComponent<Renderer>().sharedMaterial = wall;

			var parent = new GameObject("Wall Neon").transform;

			// Along the inside top edge of the walls, cyan down the sides and yellow across the ends, the same in both halves
			NeonStrip(parent, new Vector3(-15.1f, WallTop + 0.04f, 0f), new Vector3(0.14f, 0.08f, 50f), neonCyan);
			NeonStrip(parent, new Vector3(15.1f, WallTop + 0.04f, 0f), new Vector3(0.14f, 0.08f, 50f), neonCyan);
			NeonStrip(parent, new Vector3(0f, WallTop + 0.04f, -25.1f), new Vector3(30f, 0.08f, 0.14f), line);
			NeonStrip(parent, new Vector3(0f, WallTop + 0.04f, 25.1f), new Vector3(30f, 0.08f, 0.14f), line);

			// Outside the walls the market carries on, so there's no edge to the world
			var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
			ground.name = "Market Ground";
			Object.DestroyImmediate(ground.GetComponent<Collider>());
			ground.isStatic = true;
			ground.transform.SetPositionAndRotation(Vector3.down * 0.02f, Quaternion.Euler(90f, 0f, 0f));
			ground.transform.localScale = new Vector3(320f, 320f, 1f);
			ground.GetComponent<Renderer>().sharedMaterial = outside;

			// Rain standing on the pitch. Flat, with nothing to catch on, and mirrored so neither half differs.
			var puddles = new GameObject("Puddles").transform;

			foreach (var (x, z, scale, kind) in new[] { (-3.5f, 10f, 3.2f, 1), (6f, 20f, 2.4f, 2), (-11f, 5f, 2.6f, 3), (2.5f, 4.5f, 2f, 2) })
			{
				foreach (var side in new[] { 1f, -1f })
				{
					var puddle = Instantiate($"SM_Prop_Water_Puddle_0{kind}", puddles, new Vector3(side * x, 0f, side * z), side > 0f ? 0f : 180f, scale);
					var position = puddle.transform.position;
					puddle.transform.position = new Vector3(position.x, 0.006f, position.z);

					foreach (var renderer in puddle.GetComponentsInChildren<Renderer>())
						renderer.sharedMaterial = water;
				}
			}
		}

		private static void NeonStrip(Transform parent, Vector3 position, Vector3 size, Material material)
		{
			var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
			strip.name = "Strip";
			Object.DestroyImmediate(strip.GetComponent<Collider>());
			strip.isStatic = true;
			strip.transform.SetParent(parent, false);
			strip.transform.position = position;
			strip.transform.localScale = size;
			strip.GetComponent<Renderer>().sharedMaterial = material;
		}

		// The rings keep their team colours, so you can still see whose is whose. They just glow now.
		private static void ReskinGoals()
		{
			ReskinGoal("Goal Home", new Color(1f, 0.16f, 0.1f));
			ReskinGoal("Goal Away", new Color(0.12f, 0.36f, 1f));
		}

		private static void ReskinGoal(string name, Color colour)
		{
			var goal = Find(name);
			var material = EmissiveMaterial(name, colour, 1.8f);

			foreach (var renderer in goal.GetComponentsInChildren<Renderer>())
				renderer.sharedMaterial = material;

			var light = PointLight($"{name} Glow", goal.transform.position, colour, 8f, 5f);
			light.transform.SetParent(goal.transform, true);
		}

		private static void ReskinHazards()
		{
			var stripes = StripesTexture();

			var bars = UnlitMaterial("Electric Bars", stripes, new Vector2(8f, 1f));
			var shutter = UnlitMaterial("Shutter Stripes", stripes, new Vector2(2f, 1f));
			var grating = UnlitMaterial("Extraction Grate", GrateTexture(), Vector2.one);
			var steam = TransparentMaterial("Kitchen Blast", new Color(1f, 0.62f, 0.25f, 0.42f));

			foreach (var wall in Object.FindObjectsByType<ElectricWall>(FindObjectsInactive.Include))
			{
				wall.transform.Find("Panel").GetComponent<Renderer>().sharedMaterial = bars;
				SetColours(wall, new Color(0.05f, 0.11f, 0.17f), new Color(1f, 0.82f, 0.1f), new Color(0.75f, 1.9f, 2.2f));
			}

			foreach (var vent in Object.FindObjectsByType<FloorVent>(FindObjectsInactive.Include))
			{
				vent.transform.Find("Grate").GetComponent<Renderer>().sharedMaterial = grating;

				SetColours(vent, new Color(0.34f, 0.36f, 0.4f), new Color(1f, 0.72f, 0.1f), new Color(2.4f, 0.75f, 0.16f));

				// Only shows while the vent is live, since it sits under the blast the hazard switches on
				var blast = vent.transform.Find("Blast");
				blast.GetComponent<Renderer>().sharedMaterial = steam;
				var burst = Instantiate("FX_Steam_01", blast, vent.transform.position, 0f);
				burst.name = "Steam";
			}

			foreach (var barrier in Object.FindObjectsByType<RisingBarrier>(FindObjectsInactive.Include))
			{
				barrier.GetComponent<Renderer>().sharedMaterial = shutter;
				SetColours(barrier, new Color(0.3f, 0.26f, 0.12f), new Color(1f, 0.82f, 0.1f), new Color(1f, 0.78f, 0.2f));
			}
		}

		private static void BuildRailway()
		{
			var parent = Group("Railway");
			const float railX = -21.5f;
			const float railHeight = 7.4f;

			foreach (var z in new[] { -75f, -50f, -25f, 0f, 25f, 50f })
			{
				var rail = Place("SM_Bld_Train_Rail_Straight_01", parent, new Vector3(railX, railHeight, z + 12.5f), 90f);
				rail.name = "Rail";
			}

			foreach (var z in new[] { -62.5f, -37.5f, -12.5f, 12.5f, 37.5f, 62.5f })
			{
				var strut = Instantiate("SM_Bld_Train_Rail_Strut_01", parent, new Vector3(railX, 0f, z), 0f);
				strut.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
				strut.transform.localScale = new Vector3(railHeight / 5f, 1.4f, 1.4f);
				Align(strut, new Vector3(railX, 0f, z));
			}

			Place("SM_Veh_Train_Cockpit_01", parent, new Vector3(railX, railHeight - 0.15f, 16.5f), 0f);
			Place("SM_Veh_Train_Middle_01", parent, new Vector3(railX, railHeight - 0.15f, 4f), 0f);
			Place("SM_Veh_Train_Middle_01", parent, new Vector3(railX, railHeight - 0.15f, -8.7f), 0f);
		}

		private static void BuildStalls()
		{
			var parent = Group("Stalls");

			// East: the main run of stalls
			Place("SM_Veh_Food_Truck_01", parent, new Vector3(20f, 0f, -14f), 180f);
			Instantiate("FX_Steam_01", parent, new Vector3(19f, 7f, -14f), 0f);
			Place("SM_Veh_Food_Trailer_01", parent, new Vector3(19.5f, 0f, 15f), 180f);
			Instantiate("FX_Steam_01", parent, new Vector3(19f, 5f, 15f), 0f);
			Canopy(parent, "SM_Prop_Canopy_03", new Vector3(19f, CanopyHeight, 1f), 90f);
			Canopy(parent, "SM_Prop_Canopy_04", new Vector3(20.4f, CanopyHeight, -23.5f), 90f);
			Canopy(parent, "SM_Prop_Canopy_05", new Vector3(19.2f, CanopyHeight, 23.5f), 90f);

			foreach (var z in new[] { -5f, -2f, 1f, 4f, 7f })
				Place("SM_Prop_Food_Shelf_01", parent, new Vector3(17.2f, 0f, z), 90f);

			Place("SM_Prop_Vending_Machine_01", parent, new Vector3(17f, 0f, -8f), 90f);
			Place("SM_Prop_Vending_Machine_02", parent, new Vector3(17f, 0f, 9.5f), 90f);

			// West: under the railway
			Canopy(parent, "SM_Prop_Canopy_01", new Vector3(-18.6f, CanopyHeight, -13f), 90f);
			Canopy(parent, "SM_Prop_Canopy_02", new Vector3(-18.6f, CanopyHeight, 1f), 90f);
			Canopy(parent, "SM_Prop_Canopy_06", new Vector3(-18.6f, CanopyHeight, 14f), 90f);

			foreach (var z in new[] { -18f, -14f, -10f, 12f, 16f })
				Place("SM_Prop_Food_Shelf_01", parent, new Vector3(-17.2f, 0f, z), -90f);

			Place("SM_Prop_Vending_Machine_01", parent, new Vector3(-17f, 0f, 1f), -90f);
			Place("SM_Prop_Vending_Machine_02", parent, new Vector3(-17f, 0f, 3f), -90f);
			Instantiate("FX_Steam_01", parent, new Vector3(-19f, 3f, -14f), 0f);

			// North: tall enough to show over the end wall
			Place("SM_Veh_Food_Truck_01", parent, new Vector3(0f, 0f, 32f), 90f);
			Canopy(parent, "SM_Prop_Canopy_05", new Vector3(-10f, CanopyHeight + 1f, 29.5f), 0f);
			Canopy(parent, "SM_Prop_Canopy_06", new Vector3(10f, CanopyHeight + 1f, 29.5f), 0f);
			Instantiate("FX_Steam_01", parent, new Vector3(0f, 7f, 31f), 0f);

			// Hanging along the front of each run of stalls
			foreach (var z in new[] { -20f, -11f, -4f, 4f, 11f, 20f })
			{
				Place("SM_Prop_Lantern_01", parent, new Vector3(16.5f, CanopyHeight - 1.1f, z), 90f);
				Place("SM_Prop_Lantern_02", parent, new Vector3(-16.5f, CanopyHeight - 1.1f, z), -90f);
			}

			// Holograms on all four corners. South of the pitch only low enough to be under the camera's view of it.
			Place("SM_Env_Hologram_Cherry_Tree_02", parent, new Vector3(-21f, 0f, 33f), 20f);
			Place("SM_Env_Hologram_Cherry_Tree_01", parent, new Vector3(21f, 0f, 33f), -40f);
			Place("SM_Env_Hologram_Cherry_Tree_01", parent, new Vector3(-20f, 0f, -32f), 200f);
			Place("SM_Env_Hologram_Cherry_Tree_02", parent, new Vector3(21f, 0f, -33f), 160f);
			Instantiate("FX_Leaves_Blossom_01", parent, new Vector3(-21f, 6f, 33f), 0f);
			Instantiate("FX_Leaves_Blossom_01", parent, new Vector3(21f, 6f, 33f), 0f);

			BuildClawMachines(parent);

			BuildCrowd(parent);

			// Circles over the east stalls, offset from its pivot so it flies a real circle, clear of the pitch. It's low for
			// a dragon so the camera can see it, and only when it pulls back for couch players.
			var circle = new GameObject("Dragon Circle").AddComponent<Spin>();
			circle.transform.SetParent(parent, false);
			circle.transform.position = new Vector3(30f, 9f, 0f);

			var dragon = Instantiate("Hologram_Dragon_01", circle.transform, circle.transform.position + Vector3.back * 7f, 0f, 0.4f);

			foreach (var light in dragon.GetComponentsInChildren<Light>(true))
				Object.DestroyImmediate(light);
		}

		// Spare characters standing at the rail, baked into plain meshes so the crowd costs no animation
		private static void BuildCrowd(Transform parent)
		{
			var meshes = EnsureCrowdFolder();

			var spots = new (float X, float Z, float Yaw)[]
			{
				(17.3f, -18f, -90f), (17.5f, -11f, -80f), (18.6f, -9.4f, -110f), (17.4f, 12f, -95f), (18.5f, 19f, -70f), (17.3f, 21f, -90f),
				(-17.3f, -22f, 90f), (-17.4f, -5.5f, 85f), (-18.4f, -3.5f, 110f), (-17.4f, 6.5f, 95f), (-18.5f, 8.5f, 70f), (-17.3f, 21f, 90f),
				(-11f, -29.5f, 180f), (-9.5f, -30f, 200f), (10f, -29.5f, 170f), (11.5f, -30.2f, 180f),
			};

			for (var i = 0; i < spots.Length; i++)
			{
				var spot = spots[i];
				var person = BakedCharacter(CrowdModels[i % CrowdModels.Length], meshes, parent);

				person.transform.SetPositionAndRotation(new Vector3(spot.X, 0f, spot.Z), Quaternion.Euler(0f, spot.Yaw, 0f));

				// Baked poses sit a little below their pivot, so stand them on the ground
				person.transform.position += Vector3.up * -BoundsOf(person).min.y;
			}
		}

		private static GameObject BakedCharacter(string model, string folder, Transform parent)
		{
			var source = Object.Instantiate(LoadPrefab(model));
			source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

			var animator = source.GetComponentInChildren<Animator>();
			animator.enabled = false;

			// Out of the T-pose, arms down
			var left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
			var right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
			left.rotation = Quaternion.AngleAxis(72f, Vector3.forward) * left.rotation;
			right.rotation = Quaternion.AngleAxis(-72f, Vector3.forward) * right.rotation;

			var person = new GameObject(model);
			person.transform.SetParent(parent, false);

			foreach (var renderer in source.GetComponentsInChildren<Renderer>())
			{
				var part = new GameObject(renderer.name);
				part.transform.SetParent(person.transform, false);
				part.isStatic = true;

				Mesh mesh;

				if (renderer is SkinnedMeshRenderer skinned)
				{
					part.transform.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);

					var path = $"{folder}/{model}_{renderer.name}.asset";
					mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

					if (mesh == null)
					{
						mesh = new Mesh { name = renderer.name };
						skinned.BakeMesh(mesh, true);
						AssetDatabase.CreateAsset(mesh, path);
					}
				}
				else
				{
					part.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
					part.transform.localScale = renderer.transform.lossyScale;
					mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
				}

				part.AddComponent<MeshFilter>().sharedMesh = mesh;
				part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
			}

			Object.DestroyImmediate(source);

			return person;
		}

		private static string EnsureCrowdFolder()
		{
			var path = $"{Folder}/Crowd";

			AssetDatabase.DeleteAsset(path);
			EnsureFolder(path);

			return path;
		}

		// Behind the home ring, turned to face the camera, under the top of the wall so they can't get in the way
		private static void BuildClawMachines(Transform parent)
		{
			for (var x = -6f; x <= 6f; x += 3f)
			{
				var machine = Place("SM_Prop_Claw_Machine_01", parent, new Vector3(x, 0f, -27.3f), 180f);
				Place("SM_Prop_Claw_Machine_01_Insert_01", parent, new Vector3(x, 0.7f, -27.2f), 180f);
				machine.name = "Claw Machine";
			}

			// Spilled out in piles at their feet, on a fixed scatter so a rebuild doesn't change the scene
			for (var i = 0; i < 26; i++)
			{
				var x = -7.2f + i * 0.58f;
				var z = -28.6f - i % 4 * 0.42f;
				var stack = i % 5 == 0 ? 0.45f : 0f;

				Place("SM_Prop_Plushie_01", parent, new Vector3(x, stack, z), i * 47f, 1.5f);
			}
		}

		private static void BuildSkyline()
		{
			var parent = Group("Skyline");

			foreach (var (kind, x, z, yaw) in new[]
			{
				(1, -62f, -46f, 0f), (2, -68f, -10f, 90f), (3, -64f, 26f, 0f), (4, -58f, 60f, 90f),
				(3, 62f, -48f, 0f), (4, 60f, -16f, 90f), (1, 66f, 20f, 0f), (2, 62f, 56f, 90f),
				(2, -24f, 82f, 0f), (1, 2f, 88f, 90f), (4, 28f, 84f, 0f),
				(4, -30f, -92f, 0f), (3, 26f, -94f, 0f),
			})
			{
				Place($"SM_Bld_Background_Building_0{kind}", parent, new Vector3(x, 0f, z), yaw);
			}
		}

		// Above the walls on the three sides the camera faces. South of the pitch they'd only ever show their backs.
		private static void BuildSigns()
		{
			var parent = Group("Holo Signs");

			var spots = new List<(Vector3 Position, float Yaw)>();

			foreach (var z in new[] { -21f, -13f, -5f, 5f, 13f, 21f })
			{
				spots.Add((new Vector3(-16.4f, WallTop + 1.3f, z), 90f));
				spots.Add((new Vector3(16.4f, WallTop + 1.3f, z), -90f));
			}

			foreach (var x in new[] { -12f, -6f, 0f, 6f, 12f })
				spots.Add((new Vector3(x, WallTop + 1.3f, 26.4f), 180f));

			for (var i = 0; i < spots.Count; i++)
			{
				var sign = Instantiate($"SM_Prop_Holo_Sign_{i + 1:00}", parent, spots[i].Position, spots[i].Yaw, 2.2f);
				sign.transform.position = spots[i].Position;
			}
		}

		private static void Canopy(Transform parent, string name, Vector3 position, float yaw)
		{
			var canopy = Place(name, parent, position, yaw);

			var bounds = BoundsOf(canopy);

			var rotation = Quaternion.Euler(0f, yaw, 0f);

			// The sail's tips, as fractions of its size before it's turned
			var unturned = rotation * new Vector3(bounds.size.x, 0f, bounds.size.z);
			var halfWidth = Mathf.Abs(unturned.x) / 2f;
			var halfDepth = Mathf.Abs(unturned.z) / 2f;

			foreach (var (x, z) in CanopyTips[name])
			{
				var tip = rotation * new Vector3(x * halfWidth, 0f, z * halfDepth);
				var foot = new Vector3(bounds.center.x + tip.x, 0f, bounds.center.z + tip.z);

				var pole = Instantiate("SM_Prop_Canopy_Pole_01", parent, foot, 0f);
				pole.transform.localScale = new Vector3(1f, bounds.center.y / 7.1f, 1f);
				Align(pole, foot);
			}
		}

		private static void SetLighting()
		{
			var moon = Find("Directional Light").GetComponent<Light>();
			moon.color = new Color(0.62f, 0.72f, 1f);
			moon.intensity = 1.05f;

			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.34f, 0.37f, 0.5f);
			RenderSettings.skybox = null;

			RenderSettings.fog = true;
			RenderSettings.fogMode = FogMode.Linear;
			RenderSettings.fogColor = new Color(0.025f, 0.04f, 0.1f);
			RenderSettings.fogStartDistance = 45f;
			RenderSettings.fogEndDistance = 150f;

			// Light from the stalls, spread evenly so neither half is darker. No shadows, for the web build.
			foreach (var side in new[] { -1f, 1f })
			{
				PointLight("Stall Light", new Vector3(side * 11f, 6f, side * 13f), Yellow, 22f, 14f);
				PointLight("Stall Light", new Vector3(side * -11f, 6f, side * 13f), Cyan, 22f, 14f);
			}

			var camera = Object.FindAnyObjectByType<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = RenderSettings.fogColor;
			camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

			var volume = new GameObject("Neon Bloom").AddComponent<Volume>();
			volume.isGlobal = true;
			volume.sharedProfile = BloomProfile();
		}

		private static VolumeProfile BloomProfile()
		{
			var path = $"{Folder}/Bloom.asset";

			AssetDatabase.DeleteAsset(path);

			var profile = ScriptableObject.CreateInstance<VolumeProfile>();
			AssetDatabase.CreateAsset(profile, path);

			var bloom = profile.Add<Bloom>();
			bloom.threshold.Override(1.1f);
			bloom.intensity.Override(0.75f);
			bloom.scatter.Override(0.6f);
			bloom.highQualityFiltering.Override(false);
			bloom.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;

			AssetDatabase.AddObjectToAsset(bloom, profile);

			return profile;
		}

		private static Light PointLight(string name, Vector3 position, Color colour, float range, float intensity)
		{
			var light = new GameObject(name).AddComponent<Light>();
			light.type = LightType.Point;
			light.shadows = LightShadows.None;
			light.color = colour;
			light.range = range;
			light.intensity = intensity;
			light.transform.position = position;

			return light;
		}

		private static void SetColours(Hazard hazard, Color off, Color warning, Color live)
		{
			var serialized = new SerializedObject(hazard);
			serialized.FindProperty("offColour").colorValue = off;
			serialized.FindProperty("warningColour").colorValue = warning;
			serialized.FindProperty("activeColour").colorValue = live;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		/// <summary>
		/// Puts a prefab down with the centre of its bounds at this XZ and its lowest point at this height, however
		/// the pack's pivot sits, with its colliders removed.
		/// </summary>
		private static GameObject Place(string name, Transform parent, Vector3 position, float yaw, float scale = 1f)
		{
			var instance = Instantiate(name, parent, position, yaw, scale);

			Align(instance, position);

			return instance;
		}

		private static GameObject Instantiate(string name, Transform parent, Vector3 position, float yaw, float scale = 1f)
		{
			var instance = Object.Instantiate(LoadPrefab(name), parent);

			instance.name = name;
			instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
			instance.transform.localScale *= scale;

			foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
				Object.DestroyImmediate(collider);

			foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
			{
				transform.gameObject.isStatic = transform.GetComponent<Animator>() == null
					&& transform.GetComponent<ParticleSystem>() == null
					&& transform.GetComponentInParent<Animator>() == null;
			}

			return instance;
		}

		private static void Align(GameObject instance, Vector3 position)
		{
			var bounds = BoundsOf(instance);

			instance.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
		}

		private static Bounds BoundsOf(GameObject instance)
		{
			var renderers = instance.GetComponentsInChildren<Renderer>().Where(renderer => renderer is not ParticleSystemRenderer).ToArray();

			if (renderers.Length == 0)
				return new Bounds(instance.transform.position, Vector3.zero);

			var bounds = renderers[0].bounds;

			foreach (var renderer in renderers)
				bounds.Encapsulate(renderer.bounds);

			return bounds;
		}

		private static GameObject LoadPrefab(string name)
		{
			if (Prefabs.TryGetValue(name, out var prefab))
				return prefab;

			var path = AssetDatabase.FindAssets($"{name} t:Prefab", new[] { Pack })
				.Select(AssetDatabase.GUIDToAssetPath)
				.FirstOrDefault(candidate => Path.GetFileNameWithoutExtension(candidate) == name);

			prefab = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);

			if (prefab == null)
				throw new FileNotFoundException($"{nameof(NightMarketBuilder)}: {name} not found in {Pack}");

			Prefabs[name] = prefab;

			return prefab;
		}

		private static Transform Group(string name)
		{
			var group = new GameObject(name).transform;
			group.SetParent(scenery, false);

			return group;
		}

		private static GameObject Find(string name)
		{
			var found = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
				.FirstOrDefault(transform => transform.name == name);

			if (found == null)
				throw new MissingReferenceException($"{nameof(NightMarketBuilder)}: {name} isn't in the Arena scene");

			return found.gameObject;
		}

		private static Material LitMaterial(string name, Color colour, float smoothness)
		{
			var material = NewMaterial(name, "Universal Render Pipeline/Lit");
			material.SetColor("_BaseColor", colour);
			material.SetFloat("_Smoothness", smoothness);

			return material;
		}

		// No highlights, because the stall lights would leave glowing spots on the pitch that look like hazards
		private static Material MatteMaterial(string name, Color colour)
		{
			var material = LitMaterial(name, colour, smoothness: 0.3f);
			material.SetFloat("_SpecularHighlights", 0f);
			material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

			return material;
		}

		private static Material Tiled(Material material, Texture texture, Vector2 tiling)
		{
			material.SetTexture("_BaseMap", texture);
			material.SetTextureScale("_BaseMap", tiling);

			return material;
		}

		private static Material EmissiveMaterial(string name, Color colour, float strength)
		{
			var material = NewMaterial(name, "Universal Render Pipeline/Lit");
			material.SetColor("_BaseColor", colour);
			material.SetColor("_EmissionColor", colour * strength);
			material.EnableKeyword("_EMISSION");
			material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

			return material;
		}

		// Hazards tint _BaseColor, and in the dark only unlit colours stay readable
		private static Material UnlitMaterial(string name, Texture texture, Vector2 tiling)
		{
			var material = NewMaterial(name, "Universal Render Pipeline/Unlit");

			material.SetTexture("_BaseMap", texture);
			material.SetTextureScale("_BaseMap", tiling);

			return material;
		}

		private static Material TransparentMaterial(string name, Color colour)
		{
			var material = NewMaterial(name, "Universal Render Pipeline/Unlit");
			material.SetColor("_BaseColor", colour);
			material.SetFloat("_Surface", 1f);
			material.SetFloat("_Blend", 0f);
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
			material.SetInt("_ZWrite", 0);
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.renderQueue = (int)RenderQueue.Transparent;

			return material;
		}

		private static Material NewMaterial(string name, string shader)
		{
			var path = $"{Folder}/Materials/{name}.mat";

			AssetDatabase.DeleteAsset(path);

			var material = new Material(Shader.Find(shader));
			AssetDatabase.CreateAsset(material, path);

			return material;
		}

		// Light and dark bands, so hazards read as bars and shutters whatever colour they're tinted
		private static Texture2D StripesTexture() => WriteTexture("Stripes", 64, (x, y) => (x + y) % 32 < 16 ? 1f : 0.42f);

		private static Texture2D TilesTexture() => WriteTexture("Paving", 128, (x, y) => x < 3 || y < 3 ? 0.55f : 1f);

		// Slats inside a solid frame, with the hazard's tint showing on the metal
		private static Texture2D GrateTexture() => WriteTexture("Grate", 128, (x, y) =>
		{
			const int frame = 10;

			if (x < frame || y < frame || x >= 128 - frame || y >= 128 - frame)
				return 1f;

			return (y - frame) % 12 < 5 ? 0.12f : 0.8f;
		});

		private static Texture2D WriteTexture(string name, int size, System.Func<int, int, float> shade)
		{
			var path = $"{Folder}/Textures/{name}.png";

			var image = new Texture2D(size, size, TextureFormat.RGBA32, false);

			for (var y = 0; y < size; y++)
			{
				for (var x = 0; x < size; x++)
				{
					var value = shade(x, y);
					image.SetPixel(x, y, new Color(value, value, value));
				}
			}

			File.WriteAllBytes(path, image.EncodeToPNG());
			Object.DestroyImmediate(image);

			AssetDatabase.ImportAsset(path);

			if (AssetImporter.GetAtPath(path) is TextureImporter importer)
			{
				importer.wrapMode = TextureWrapMode.Repeat;
				importer.SaveAndReimport();
			}

			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;

			var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');

			EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
		}
	}
}
