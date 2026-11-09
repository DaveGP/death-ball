using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Builders
{
	internal static class DeathBallUiArt
	{
		public static readonly Color32 Paper = new(232, 230, 225, 255);
		public static readonly Color32 Steel = new(107, 116, 128, 255);
		public static readonly Color32 Red = new(200, 57, 43, 255);
		public static readonly Color32 Yellow = new(242, 194, 48, 255);

		public static Font Bold => LoadFont("BarlowCondensed-Bold");
		public static Font Display => LoadFont("BarlowCondensed-ExtraBold");
		public static Font Mono => LoadFont("IBMPlexMono-Medium");

		private static Font LoadFont(string name) =>
			AssetDatabase.LoadAssetAtPath<Font>($"Assets/UI/Fonts/{name}.ttf");

		public static Sprite Sprite(string name) =>
			AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/UI/{name}.png");

		public static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
		{
			var rect = new GameObject(name).AddComponent<RectTransform>();
			rect.SetParent(parent, false);
			rect.anchoredPosition = position;
			rect.sizeDelta = size;
			return rect;
		}

		public static Image Image(string name, Transform parent, string sprite, Vector2 position, Vector2 size)
		{
			var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
			SetSprite(image, sprite);
			image.raycastTarget = false;
			return image;
		}

		public static void SetSprite(Image image, string name)
		{
			image.sprite = Sprite(name);
			image.type = image.sprite != null && image.sprite.border != Vector4.zero
				? UnityEngine.UI.Image.Type.Sliced
				: UnityEngine.UI.Image.Type.Simple;
			image.color = Color.white;
		}

		public static Text Text(string name, Transform parent, string content, int fontSize, Vector2 position, Vector2 size,
			Font font = null, TextAnchor alignment = TextAnchor.MiddleCenter)
		{
			var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
			text.font = font != null ? font : Bold;
			text.text = content;
			text.fontSize = fontSize;
			text.alignment = alignment;
			text.color = Paper;
			text.raycastTarget = false;
			return text;
		}
	}

	internal sealed class DeathBallUiTextureImporter : AssetPostprocessor
	{
		private void OnPreprocessTexture()
		{
			if (!assetPath.StartsWith("Assets/UI/") || Path.GetExtension(assetPath) != ".png")
				return;

			var name = Path.GetFileNameWithoutExtension(assetPath);
			var crest = assetPath.StartsWith("Assets/UI/Resources/TeamCrests/");
			var border = name switch
			{
				"Panel" => 28,
				"HUD-Event" => 38,
				"HUD-Score" => 24,
				"HUD-Clock" or "Lobby-Row" or "Lobby-Row-Selected" => 12,
				"Lobby-Home" or "Lobby-Away" => 16,
				_ when name.StartsWith("Button-") || name.StartsWith("Input-") => 24,
				_ => 0
			};

			var texture = (TextureImporter)assetImporter;
			texture.textureType = TextureImporterType.Sprite;
			texture.spriteImportMode = SpriteImportMode.Single;
			var settings = new TextureImporterSettings();
			texture.ReadTextureSettings(settings);
			settings.spriteMeshType = SpriteMeshType.FullRect;
			texture.SetTextureSettings(settings);
			texture.spriteBorder = Vector4.one * border;
			texture.npotScale = TextureImporterNPOTScale.None;
			texture.mipmapEnabled = false;
			texture.alphaIsTransparency = name != "Menu-Arena";
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.maxTextureSize = crest ? 256 : 2048;

			// Keep the small UI edges and exact side colours lossless. Compress the large plates,
			// artwork and crests, which all have source dimensions divisible by four.
			var compressed = crest || name is "Menu-Arena" or "Death-Ball" or "Death-Ball-Stacked" or "HUD-Event" or "Panel";
			texture.textureCompression = compressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
			texture.SetPlatformTextureSettings(new TextureImporterPlatformSettings
			{
				name = "WebGL",
				overridden = compressed,
				maxTextureSize = texture.maxTextureSize,
				format = name == "Menu-Arena" ? TextureImporterFormat.DXT1Crunched : TextureImporterFormat.DXT5Crunched,
				textureCompression = TextureImporterCompression.Compressed,
				compressionQuality = 85
			});
		}
	}
}
