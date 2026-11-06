using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


namespace Builders
{
	/// <summary>
	/// Makes each team's own copy of its Synty character models (see Documentation/Teams.md): one prefab per
	/// role in a folder per team, scaled to read as that role and recoloured into the team's colours. The
	/// packs colour everything from one atlas texture, so each team gets its own recoloured copy of it, with
	/// a table of which atlas colours to replace. Skin isn't in the tables, so it stays as it is.
	/// The output sits in ThirdParty, which git ignores
	/// because the packs' licences don't allow redistributing them, so this tool is how another machine with
	/// the packs gets the same result.
	/// </summary>
	public static class TeamArtBuilder
	{
		public const string OutputRoot = "Assets/ThirdParty/Synty/DeathBallTeams";

		private const string CyberCity = "Assets/ThirdParty/Synty/PolygonCyberCity";
		private const string SciFiCity = "Assets/ThirdParty/Synty/PolygonSciFiCity";

		// Relative to the Thrower, as the grey-box capsules were: Bruisers wide, Runners slim and tall
		private static readonly Vector3 BruiserScale = new(1.33f, 1.05f, 1.33f);
		private static readonly Vector3 RunnerScale = new(0.8f, 1.05f, 0.8f);

		private const string BaseMap = "_BaseMap";

		// How far an atlas pixel can be from a table colour and still count as it, out of 255 per channel
		private const int MatchDistance = 16;

		// The packs' atlases go up to 4096; players never fill the screen, and the web build has to carry these
		private const int MaxTextureSize = 2048;

		private enum Role
		{
			Bruiser,
			Runner,
			Thrower,
			Enforcer,
		}

		private sealed class Team
		{
			public string Folder;
			public string Pack;
			public string[] Members; // Bruiser, Runner, Thrower, Enforcer

			// Atlas colour (each channel in sixteenths, as the top hex digit) to the team's colour, by source
			// texture name; "*" is for any texture without its own table
			public Dictionary<string, (string From, string To)[]> Colours;

			// Glowing trim is recoloured to this, or left as the pack has it when empty
			public string Glow;
		}

		private static readonly Team[] Teams =
		{
			new()
			{
				Folder = "NinthLife",
				Pack = CyberCity,
				Members = new[] { "SM_Chr_MurderKitten_01", "SM_Chr_Cat_01", "SM_Chr_Mercenary_Female_01", "SM_Chr_Mercenary_Male_01" },
				// Black and yellow with cyan accents: the Cat's hoodie is already the look
				Colours = new()
				{
					["*"] = new[]
					{
						("597", "F2A93B"), ("486", "F2A93B"), ("365", "16323A"), ("366", "16323A"), ("255", "16323A"),
						("B8B", "2FD6E0"), ("969", "1FA7B5"), ("FFF", "6FE3EA"),
					},
				},
			},
			new()
			{
				Folder = "Recalled",
				Pack = CyberCity,
				Members = new[] { "SM_Chr_PleasureBot_Male_01", "SM_Chr_Robot_Female_01", "SM_Chr_Robot_Mercenary_01", "SM_Chr_Robot_Damaged_01" },
				// Silver with orange warning paint. The Marshal comes from a different atlas.
				Colours = new()
				{
					["*"] = new[]
					{
						("B8B", "E0701E"), ("EA4", "E0701E"), ("6B8", "F08A2C"), ("0FD", "F08A2C"), ("333", "C85A16"),
					},
					["PolygonCyberCity_Texture_03_B"] = new[]
					{
						("345", "C85A16"), ("556", "E0701E"), ("234", "8A3D10"), ("666", "AEB3B8"), ("444", "7E8388"), ("445", "8A8F96"),
					},
				},
				Glow = "FF7A1A",
			},
			new()
			{
				Folder = "HalcyonBlueChips",
				Pack = SciFiCity,
				Members = new[] { "Character_Cop_01", "Character_Rich_Female_01", "Character_Rich_Male_01", "Character_Android_Female_01" },
				// White and ice blue, so the hired muscle wears the same corporate colours as the board
				Colours = new()
				{
					["*"] = new[]
					{
						("122", "6F8FB0"), ("444", "E6EEF4"), ("555", "BDD8EA"), ("222", "47627F"), ("566", "A9C6DC"), ("333", "54708C"), ("111", "2F4560"),
						("D9B", "9CD3F0"),
					},
				},
				Glow = "27E0F5",
			},
			new()
			{
				Folder = "Local66",
				Pack = SciFiCity,
				Members = new[] { "Character_Garbage_Male_01", "Character_Junky_Female_01", "Character_Junky_Male_01", "Character_Medical_Male_01" },
				// Olive work clothes and hazard yellow, the colours the Bruiser and Enforcer already wear
				Colours = new()
				{
					["*"] = new[]
					{
						("A32", "666626"), ("548", "D9A63F"), ("66B", "E6B84C"), ("324", "6E5A1E"), ("522", "D9A63F"),
					},
				},
				Glow = "FFC21A",
			},
			new()
			{
				Folder = "OrderOfTheLastRing",
				Pack = SciFiCity,
				Members = new[] { "Character_Muscle_Male_01", "Character_CyborgNinja_01", "Character_Alien_Male_01", "Character_Monk_Male_01" },
				// Rust robes, cream cloth and cyan glow, taken from the Monk. The Alien's blue skin stays.
				Colours = new()
				{
					["*"] = new[]
					{
						("732", "8F3F20"), ("988", "9A4A24"), ("345", "8F3F20"), ("AAA", "D9CDB8"), ("A74", "B8622A"),
						("467", "23B8B0"), ("122", "4A2414"), ("787", "C8A070"),
					},
				},
				Glow = "27F0DC",
			},
		};

		[MenuItem("Death Ball/Build Team Art")]
		public static void Build()
		{
			if (!AssetDatabase.IsValidFolder(CyberCity) || !AssetDatabase.IsValidFolder(SciFiCity))
			{
				Debug.LogError($"{nameof(TeamArtBuilder)}: needs the Cyber City and SciFi City packs in Assets/ThirdParty/Synty");
				return;
			}

			foreach (var team in Teams)
				BuildTeam(team);

			AssetDatabase.SaveAssets();

			Debug.Log($"{nameof(TeamArtBuilder)}: built {Teams.Length} teams in {OutputRoot}");
		}

		private static void BuildTeam(Team team)
		{
			var folder = EnsureFolder(OutputRoot, team.Folder);
			var models = EnsureFolder(folder, "Models");
			var materials = EnsureFolder(folder, "Materials");
			var textures = EnsureFolder(folder, "Textures");

			var previewScene = EditorSceneManager.NewPreviewScene();

			try
			{
				for (var i = 0; i < team.Members.Length; i++)
					BuildMember(team, (Role)i, team.Members[i], folder, models, materials, previewScene);
			}
			finally
			{
				EditorSceneManager.ClosePreviewScene(previewScene);
			}

			foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { materials }))
				Recolour(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)), team, textures);
		}

		private static void Recolour(Material material, Team team, string folder)
		{
			var atlas = material.GetTexture(BaseMap);
			var sourceAtlas = SourceOf(atlas);
			var table = team.Colours.TryGetValue(sourceAtlas.name, out var own) ? own : team.Colours["*"];

			var recoloured = WriteTexture(sourceAtlas, folder, ColourMap(table));

			foreach (var property in new[] { BaseMap, "_MainTex", "_Texture" })
			{
				if (material.HasTexture(property))
					material.SetTexture(property, recoloured);
			}

			if (!string.IsNullOrEmpty(team.Glow) && material.GetTexture("_EmissionMap") is { } glowMap)
			{
				var tint = ToColour(team.Glow);
				var glow = WriteTexture(SourceOf(glowMap), folder, pixel =>
				{
					var strength = Mathf.Max(pixel.r, pixel.g, pixel.b) / 255f;
					return new Color32((byte)(tint.r * strength), (byte)(tint.g * strength), (byte)(tint.b * strength), pixel.a);
				});

				foreach (var property in new[] { "_EmissionMap", "_Emission" })
				{
					if (material.HasTexture(property))
						material.SetTexture(property, glow);
				}
			}

			EditorUtility.SetDirty(material);
		}

		/// <summary>
		/// Replaces each pixel near a table colour with the team's colour, keeping how much lighter or darker it
		/// was so shading and detail painted into the atlas survive.
		/// </summary>
		private static System.Func<Color32, Color32> ColourMap((string From, string To)[] table)
		{
			var entries = table.Select(row => (From: FromBuckets(row.From), To: ToColour(row.To))).ToArray();

			return pixel =>
			{
				foreach (var (from, to) in entries)
				{
					int dr = pixel.r - from.r, dg = pixel.g - from.g, db = pixel.b - from.b;

					if (dr * dr + dg * dg + db * db > MatchDistance * MatchDistance)
						continue;

					var shade = Mathf.Clamp(Luma(pixel) / Mathf.Max(Luma(from), 1f), 0.6f, 1.4f);

					return new Color32(
						(byte)Mathf.Min(to.r * shade, 255f),
						(byte)Mathf.Min(to.g * shade, 255f),
						(byte)Mathf.Min(to.b * shade, 255f),
						pixel.a);
				}

				return pixel;
			};
		}

		private static Texture2D WriteTexture(Texture source, string folder, System.Func<Color32, Color32> map)
		{
			var sourcePath = AssetDatabase.GetAssetPath(source);
			var path = $"{folder}/{source.name}.png";

			var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			image.LoadImage(File.ReadAllBytes(sourcePath));

			// Atlases are flat colour, so there are few distinct pixels to work out
			var known = new Dictionary<int, Color32>();
			var pixels = image.GetPixels32();

			for (var i = 0; i < pixels.Length; i++)
			{
				var pixel = pixels[i];
				var key = (pixel.r << 24) | (pixel.g << 16) | (pixel.b << 8) | pixel.a;

				if (!known.TryGetValue(key, out var mapped))
					known[key] = mapped = map(pixel);

				pixels[i] = mapped;
			}

			image.SetPixels32(pixels);
			File.WriteAllBytes(path, image.EncodeToPNG());
			Object.DestroyImmediate(image);

			AssetDatabase.ImportAsset(path);

			if (AssetImporter.GetAtPath(path) is TextureImporter importer && AssetImporter.GetAtPath(sourcePath) is TextureImporter sourceImporter)
			{
				var settings = new TextureImporterSettings();
				sourceImporter.ReadTextureSettings(settings);
				importer.SetTextureSettings(settings);
				importer.maxTextureSize = MaxTextureSize;
				importer.SaveAndReimport();
			}

			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		// A rebuild finds the team's own texture on the material, so go back to the pack's
		private static Texture SourceOf(Texture texture)
		{
			var path = AssetDatabase.GetAssetPath(texture);

			if (!path.StartsWith(OutputRoot))
				return texture;

			var match = AssetDatabase.FindAssets($"{texture.name} t:Texture2D", new[] { CyberCity, SciFiCity })
				.Select(AssetDatabase.GUIDToAssetPath)
				.FirstOrDefault(candidate => Path.GetFileNameWithoutExtension(candidate) == texture.name);

			return match == null ? texture : AssetDatabase.LoadAssetAtPath<Texture>(match);
		}

		private static Color32 FromBuckets(string hex)
		{
			byte Channel(char digit) => (byte)(System.Convert.ToInt32(digit.ToString(), 16) * 16 + 8);

			return new Color32(Channel(hex[0]), Channel(hex[1]), Channel(hex[2]), 255);
		}

		private static Color32 ToColour(string hex)
		{
			ColorUtility.TryParseHtmlString($"#{hex}", out var colour);

			return colour;
		}

		private static float Luma(Color32 colour) => 0.3f * colour.r + 0.59f * colour.g + 0.11f * colour.b;

		private static void BuildMember(Team team, Role role, string member, string folder, string models, string materials, UnityEngine.SceneManagement.Scene scene)
		{
			var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{team.Pack}/Prefabs/Characters/{member}.prefab");

			if (source == null)
			{
				Debug.LogError($"{nameof(TeamArtBuilder)}: {member} not found in {team.Pack}");
				return;
			}

			var copy = Object.Instantiate(source);
			UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, scene);
			copy.name = $"{role}_{member}";

			// SciFi City's prefabs carry every character in the pack, switched off. Keep only this one.
			foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
			{
				if (!renderer.gameObject.activeSelf)
					Object.DestroyImmediate(renderer.gameObject);
			}

			foreach (var animator in copy.GetComponentsInChildren<Animator>(true))
			{
				if (animator.avatar != null)
					animator.avatar = CopiedAsset<Avatar>(animator.avatar, models);
			}

			foreach (var skinned in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				skinned.sharedMesh = CopiedAsset<Mesh>(skinned.sharedMesh, models);

			foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
				filter.sharedMesh = CopiedAsset<Mesh>(filter.sharedMesh, models);

			foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
				renderer.sharedMaterials = renderer.sharedMaterials.Select(material => CopiedMaterial(material, materials)).ToArray();

			copy.transform.localScale = role switch
			{
				Role.Bruiser => BruiserScale,
				Role.Runner => RunnerScale,
				_ => Vector3.one,
			};

			PrefabUtility.SaveAsPrefabAsset(copy, $"{folder}/{copy.name}.prefab");
			Object.DestroyImmediate(copy);
		}

		/// <summary>
		/// Finds an asset's twin in the team's own copy of the file it came from, copying the file the first
		/// time. Copies are readable so the team's colours can be worked out from their meshes.
		/// </summary>
		private static T CopiedAsset<T>(T asset, string folder) where T : Object
		{
			if (asset == null)
				return null;

			var sourcePath = AssetDatabase.GetAssetPath(asset);

			if (sourcePath.StartsWith(OutputRoot))
				return asset;

			var copyPath = $"{folder}/{Path.GetFileName(sourcePath)}";

			if (AssetDatabase.AssetPathToGUID(copyPath, AssetPathToGUIDOptions.OnlyExistingAssets) == "")
			{
				AssetDatabase.CopyAsset(sourcePath, copyPath);

				if (AssetImporter.GetAtPath(copyPath) is ModelImporter importer && !importer.isReadable)
				{
					importer.isReadable = true;
					importer.SaveAndReimport();
				}
			}

			var copied = AssetDatabase.LoadAllAssetsAtPath(copyPath).OfType<T>().FirstOrDefault(candidate => candidate.name == asset.name);

			if (copied == null)
				Debug.LogError($"{nameof(TeamArtBuilder)}: {asset.name} not found in {copyPath}");

			return copied;
		}

		private static Material CopiedMaterial(Material material, string folder)
		{
			if (material == null)
				return null;

			var path = $"{folder}/{material.name}.mat";
			var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

			if (existing != null)
				return existing;

			if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(material), path))
				Debug.LogError($"{nameof(TeamArtBuilder)}: couldn't copy {material.name}");

			return AssetDatabase.LoadAssetAtPath<Material>(path);
		}

		private static string EnsureFolder(string parent, string name)
		{
			if (!AssetDatabase.IsValidFolder(parent))
				EnsureFolder(Path.GetDirectoryName(parent)?.Replace('\\', '/'), Path.GetFileName(parent));

			var path = $"{parent}/{name}";

			if (!AssetDatabase.IsValidFolder(path))
				AssetDatabase.CreateFolder(parent, name);

			return path;
		}
	}
}
