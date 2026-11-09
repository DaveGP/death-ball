using System.Collections.Generic;
using System.Linq;
using Data.Enums;
using Menus;
using Networking;
using Players;
using SystemManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;


namespace Builders
{
	/// <summary>
	/// Builds the main menu scene: a start screen to host or join, and the lobby.
	/// Run again after changing it to regenerate the scene.
	/// </summary>
	public static class MainMenuSceneBuilder
	{
		public const string ScenePath = "Assets/Scenes/MainMenu.unity";


		private static readonly Color BackgroundColour = new Color32(14, 17, 22, 255);

		[MenuItem("Death Ball/Build Main Menu Scene")]
		public static void Build()
		{
			NetworkManagerBuilder.Build();

			CreateScene();

			AssetDatabase.SaveAssets();

			Debug.Log("MainMenuSceneBuilder: Main menu scene built.");
		}

		private static void CreateScene()
		{
			ArenaSceneBuilder.EnsureFolder("Assets/Scenes");

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

			var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = BackgroundColour;
			camera.gameObject.AddComponent<AudioListener>();

			// Makes the session's NetworkManager the first time, which then lasts into the arena and back
			var starter = new GameObject("Network Starter").AddComponent<NetworkStarter>();
			ArenaSceneBuilder.SetSerializedReference(starter, "networkManagerPrefab", NetworkManagerBuilder.LoadPrefab());

			new GameObject("Local Players").AddComponent<LocalPlayerManager>();

			new GameObject("Systems").AddComponent<SystemsRunner>();

			// Mouse only: sticks and keys are for picking teams and types
			var eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
			eventSystem.sendNavigationEvents = false;
			eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

			CreateCanvas();

			EditorSceneManager.SaveScene(scene, ScenePath);

			AddScenesToBuild();
		}

		private static void CreateCanvas()
		{
			var canvasObject = new GameObject("Menu");

			var canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var scaler = canvasObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;

			canvasObject.AddComponent<GraphicRaycaster>();

			var root = canvasObject.transform;

			CreateBroadcast(root);

			var controller = canvasObject.AddComponent<MainMenuController>();

			CreateStartPanel(root, controller);
			CreateLobbyPanel(root, controller);
		}

		private static void CreateBroadcast(Transform root)
		{
			var background = DeathBallUiArt.Image("Arena Feed", root, "Menu-Arena", Vector2.zero, Vector2.zero);
			var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
			fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			fitter.aspectRatio = 16f / 9f;

			CreateRail(root, "Top Broadcast Rail", new Vector2(0.5f, 1f), -53f,
				"ARENA FEED", "HALCYON SPORTS NETWORK / 01");
			CreateRail(root, "Bottom Broadcast Rail", new Vector2(0.5f, 0f), 53f,
				"SANCTIONED ARENA SPORT", "4 v 4 / SEALED ARENA");
		}

		private static void CreateRail(Transform root, string name, Vector2 anchor, float y, string left, string right)
		{
			var rail = DeathBallUiArt.Rect(name, root, new Vector2(0f, y), new Vector2(1760f, 42f));
			rail.anchorMin = rail.anchorMax = anchor;

			var line = DeathBallUiArt.Image("Rule", rail, null, new Vector2(0f, 20f), new Vector2(1760f, 1f));
			line.color = DeathBallUiArt.Steel;
			DeathBallUiArt.Text("Left", rail, left, 18, new Vector2(-550f, -4f), new Vector2(640f, 28f),
				DeathBallUiArt.Mono, TextAnchor.MiddleLeft);
			DeathBallUiArt.Text("Right", rail, right, 18, new Vector2(550f, -4f), new Vector2(640f, 28f),
				DeathBallUiArt.Mono, TextAnchor.MiddleRight);

			if (anchor.y == 1f)
			{
				var live = DeathBallUiArt.Image("Live Plate", rail, null, new Vector2(-848f, -4f), new Vector2(64f, 28f));
				live.color = DeathBallUiArt.Red;
				DeathBallUiArt.Text("Live", live.transform, "LIVE", 18, Vector2.zero, live.rectTransform.sizeDelta, DeathBallUiArt.Mono);
				rail.Find("Left").GetComponent<RectTransform>().anchoredPosition += new Vector2(82f, 0f);
			}
		}

		private static void CreateStartPanel(Transform root, MainMenuController controller)
		{
			var start = DeathBallUiArt.Rect("Start Screen", root, Vector2.zero, Vector2.zero);
			var logo = DeathBallUiArt.Image("Death Ball", start, "Death-Ball-Stacked", new Vector2(-510f, -7f), new Vector2(684f, 470f));
			logo.preserveAspect = true;
			DeathBallUiArt.Text("Sponsor", start, "A HALCYON HOLDINGS BROADCAST", 22,
				new Vector2(-408f, -223f), new Vector2(780f, 35f), DeathBallUiArt.Mono, TextAnchor.MiddleLeft);

			var panel = DeathBallUiArt.Image("Match Access", start, "Panel", new Vector2(425f, 130f), new Vector2(650f, 512f)).rectTransform;
			DeathBallUiArt.Text("Heading", panel, "MATCH ACCESS", 58, new Vector2(0f, 191f), new Vector2(560f, 84f),
				DeathBallUiArt.Display, TextAnchor.MiddleLeft);
			CreateLabel(panel, "PLAYER NAME", 132f);
			var nameInput = CreateInputField("Name", panel, "Your name", new Vector2(0f, 85f), 560f, DeathBallUiArt.Bold);

			var local = CreateButton("Play Local", panel, "PLAY LOCAL", new Vector2(-143f, 3f), 274f);
			var priority = DeathBallUiArt.Image("Priority", local.transform, null, new Vector2(-123f, 0f), new Vector2(5f, 32f));
			priority.color = DeathBallUiArt.Red;
			var host = CreateButton("Host Online", panel, "HOST ONLINE", new Vector2(143f, 3f), 274f);

			var rule = DeathBallUiArt.Image("Access Rule", panel, null, new Vector2(0f, -48f), new Vector2(560f, 1f));
			rule.color = DeathBallUiArt.Steel;
			CreateLabel(panel, "JOIN CODE", -77f);
			var codeInput = CreateInputField("Join Code", panel, "ENTER CODE", new Vector2(-82f, -124f), 396f, DeathBallUiArt.Mono);
			var join = CreateButton("Join", panel, "JOIN", new Vector2(204f, -124f), 152f, 64f);

			var quit = CreateButton("Quit", panel, "QUIT", new Vector2(227f, -201f), 106f, 44f);
			var statusLight = DeathBallUiArt.Image("Status Light", panel, null, new Vector2(-275f, -201f), new Vector2(9f, 9f));
			statusLight.color = DeathBallUiArt.Yellow;
			var status = DeathBallUiArt.Text("Status", panel, string.Empty, 20, new Vector2(-54f, -201f), new Vector2(416f, 60f),
				DeathBallUiArt.Mono, TextAnchor.MiddleLeft);

			SetReferences(controller, new Dictionary<string, Object>
			{
				["startPanel"] = start.gameObject,
				["nameInput"] = nameInput,
				["codeInput"] = codeInput,
				["localButton"] = local,
				["hostButton"] = host,
				["joinButton"] = join,
				["quitButton"] = quit,
				["statusText"] = status,
			});
		}

		private static void CreateLabel(Transform parent, string label, float y)
		{
			DeathBallUiArt.Text(label, parent, label, 21, new Vector2(0f, y), new Vector2(560f, 30f),
				DeathBallUiArt.Mono, TextAnchor.MiddleLeft);
		}

		private static void CreateLobbyPanel(Transform root, MainMenuController controller)
		{
			var panel = DeathBallUiArt.Image("Lobby Panel", root, "Panel", Vector2.zero, new Vector2(1500f, 864f)).rectTransform;

			DeathBallUiArt.Text("Heading", panel, "MATCH LOBBY", 60, new Vector2(0f, 360f), new Vector2(1320f, 88f),
				DeathBallUiArt.Display);
			var info = DeathBallUiArt.Text("Info", panel, string.Empty, 22, new Vector2(0f, 283f),
				new Vector2(1320f, 90f), DeathBallUiArt.Mono);

			var home = CreateLobbyColumn(panel, "Home", -350f);
			var away = CreateLobbyColumn(panel, "Away", 350f);

			var matchLength = CreateButton("Match Length", panel, "MATCH LENGTH", new Vector2(-415f, -352f));
			var start = CreateButton("Start Match", panel, "START MATCH", new Vector2(0f, -352f));
			var leave = CreateButton("Leave", panel, "LEAVE", new Vector2(415f, -352f));

			panel.gameObject.SetActive(false);

			SetReferences(controller, new Dictionary<string, Object>
			{
				["lobbyPanel"] = panel.gameObject,
				["lobbyInfoText"] = info,
				["homeListText"] = home.Header,
				["awayListText"] = away.Header,
				["homePlayerList"] = home.List,
				["awayPlayerList"] = away.List,
				["matchLengthButton"] = matchLength,
				["startMatchButton"] = start,
				["leaveButton"] = leave,
			});
		}

		private static (Text Header, LobbyPlayerList List) CreateLobbyColumn(Transform panel, string side, float x)
		{
			var plate = DeathBallUiArt.Image($"{side} Plate", panel, $"Lobby-{side}", new Vector2(x, 188f), new Vector2(620f, 58f));
			var header = DeathBallUiArt.Text($"{side} List", plate.transform, side.ToUpperInvariant(), 35, Vector2.zero, plate.rectTransform.sizeDelta);

			var viewport = DeathBallUiArt.Rect($"{side} Players", panel, new Vector2(x, -73f), new Vector2(620f, 450f));
			viewport.gameObject.AddComponent<RectMask2D>();
			viewport.gameObject.AddComponent<Image>().color = Color.clear;
			var scroll = viewport.gameObject.AddComponent<ScrollRect>();
			scroll.viewport = viewport;
			scroll.horizontal = false;
			scroll.movementType = ScrollRect.MovementType.Clamped;

			var content = DeathBallUiArt.Rect("Rows", viewport, Vector2.zero, Vector2.zero);
			content.anchorMin = new Vector2(0f, 1f);
			content.anchorMax = Vector2.one;
			content.pivot = new Vector2(0.5f, 1f);
			var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
			layout.spacing = 7f;
			layout.childControlHeight = false;
			layout.childForceExpandHeight = false;
			content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			scroll.content = content;

			var row = DeathBallUiArt.Image("Player Row", content, "Lobby-Row", Vector2.zero, new Vector2(620f, 62f));
			var name = DeathBallUiArt.Text("Player Name", row.transform, string.Empty, 29,
				new Vector2(-106f, 0f), new Vector2(368f, 48f), DeathBallUiArt.Bold, TextAnchor.MiddleLeft);
			name.resizeTextForBestFit = true;
			name.resizeTextMinSize = 20;
			name.resizeTextMaxSize = 29;
			var role = DeathBallUiArt.Text("Role", row.transform, string.Empty, 23,
				new Vector2(208f, 0f), new Vector2(150f, 48f), DeathBallUiArt.Bold, TextAnchor.MiddleRight);
			var roleIcon = DeathBallUiArt.Image("Role Icon", row.transform, null, new Vector2(117f, 0f), new Vector2(36f, 36f));
			var view = row.gameObject.AddComponent<LobbyPlayerRow>();
			SetReferences(view, new Dictionary<string, Object>
			{
				["background"] = row, ["playerName"] = name, ["role"] = role, ["roleIcon"] = roleIcon,
				["normal"] = row.sprite, ["selected"] = DeathBallUiArt.Sprite("Lobby-Row-Selected")
			});
			ArenaSceneBuilder.SetSerializedArray(view, "roleIcons",
				System.Enum.GetValues(typeof(PlayerType)).Cast<PlayerType>().Select(type => (Object)DeathBallUiArt.Sprite($"Role-{type}")).ToArray());
			row.gameObject.SetActive(false);

			var list = viewport.gameObject.AddComponent<LobbyPlayerList>();
			ArenaSceneBuilder.SetSerializedReference(list, "rowTemplate", view);
			return (header, list);
		}

		private static Button CreateButton(string name, Transform parent, string label, Vector2 position, float width = 380f, float height = 70f)
		{
			var button = DefaultControls.CreateButton(default);
			button.name = name;
			var rect = (RectTransform)button.transform;
			rect.SetParent(parent, false);
			rect.anchoredPosition = position;
			rect.sizeDelta = new Vector2(width, height);

			var text = button.GetComponentInChildren<Text>();
			text.font = DeathBallUiArt.Bold;
			text.text = label;
			text.fontSize = height < 50f ? 26 : 34;
			text.color = DeathBallUiArt.Paper;
			text.raycastTarget = false;

			var control = button.GetComponent<Button>();
			Skin(control, "Button-Normal", "Button-Highlighted", "Button-Pressed", "Button-Disabled");
			return control;
		}

		private static InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 position, float width, Font font)
		{
			var field = DefaultControls.CreateInputField(default);
			field.name = name;
			var rect = (RectTransform)field.transform;
			rect.SetParent(parent, false);
			rect.anchoredPosition = position;
			rect.sizeDelta = new Vector2(width, 64f);

			var input = field.GetComponent<InputField>();
			foreach (var text in field.GetComponentsInChildren<Text>(true))
			{
				text.font = font;
				text.fontSize = 29;
				text.fontStyle = FontStyle.Normal;
				text.alignment = TextAnchor.MiddleLeft;
				text.color = DeathBallUiArt.Paper;
				text.rectTransform.offsetMin = new Vector2(16f, 4f);
				text.rectTransform.offsetMax = new Vector2(-16f, -4f);
			}

			var hint = (Text)input.placeholder;
			hint.text = placeholder;
			hint.color = new Color32(177, 184, 193, 255);
			Skin(input, "Input-Normal", "Input-Selected", "Input-Selected", "Input-Normal");
			return input;
		}

		private static void Skin(Selectable control, string normal, string highlighted, string pressed, string disabled)
		{
			DeathBallUiArt.SetSprite((Image)control.targetGraphic, normal);
			control.transition = Selectable.Transition.SpriteSwap;
			control.spriteState = new SpriteState
			{
				highlightedSprite = DeathBallUiArt.Sprite(highlighted),
				selectedSprite = DeathBallUiArt.Sprite(highlighted),
				pressedSprite = DeathBallUiArt.Sprite(pressed),
				disabledSprite = DeathBallUiArt.Sprite(disabled)
			};
		}

		private static void SetReferences(Object target, Dictionary<string, Object> references)
		{
			foreach (var reference in references)
				ArenaSceneBuilder.SetSerializedReference(target, reference.Key, reference.Value);
		}

		private static void AddScenesToBuild()
		{
			var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

			scenes.RemoveAll(s => s.path == ScenePath || !System.IO.File.Exists(s.path));

			scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

			if (scenes.TrueForAll(s => s.path != ArenaSceneBuilder.ScenePath) && System.IO.File.Exists(ArenaSceneBuilder.ScenePath))
				scenes.Add(new EditorBuildSettingsScene(ArenaSceneBuilder.ScenePath, true));

			EditorBuildSettings.scenes = scenes.ToArray();
		}

	}
}
