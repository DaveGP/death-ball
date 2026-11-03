using System.Collections.Generic;
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
	/// Builds the grey-box main menu scene: a start screen to host or join, and the lobby.
	/// Run again after changing it; it overwrites the scene and lobby prefab it made before.
	/// </summary>
	public static class MainMenuSceneBuilder
	{
		public const string ScenePath = "Assets/Scenes/MainMenu.unity";


		private static readonly Color BackgroundColour = new(0.08f, 0.08f, 0.1f);

		private static readonly Color PanelColour = new(0f, 0f, 0f, 0.4f);

		private static readonly Color AccentColour = new(0.85f, 0.2f, 0.1f);

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

			CreateText("Title", root, "DEATH BALL", 120, new Vector2(0f, 380f), new Vector2(1400f, 160f), AccentColour);

			var controller = canvasObject.AddComponent<MainMenuController>();

			CreateStartPanel(root, controller);

			CreateLobbyPanel(root, controller);
		}

		private static void CreateStartPanel(Transform root, MainMenuController controller)
		{
			var panel = CreatePanel("Start Panel", root, new Vector2(0f, -90f), new Vector2(640f, 700f));

			var nameInput = CreateInputField("Name", panel, "Your name", new Vector2(0f, 270f));

			var local = CreateButton("Play Local", panel, "Play Local", new Vector2(0f, 170f));
			var host = CreateButton("Host Online", panel, "Host Online", new Vector2(0f, 85f));

			var codeInput = CreateInputField("Join Code", panel, "Join code", new Vector2(0f, -15f));
			var join = CreateButton("Join", panel, "Join", new Vector2(0f, -100f));

			var quit = CreateButton("Quit", panel, "Quit", new Vector2(0f, -195f));

			var status = CreateText("Status", panel, string.Empty, 26, new Vector2(0f, -285f), new Vector2(620f, 90f), Color.white);

			SetReferences(controller, new Dictionary<string, Object>
			{
				["startPanel"] = panel.gameObject,
				["nameInput"] = nameInput,
				["codeInput"] = codeInput,
				["localButton"] = local,
				["hostButton"] = host,
				["joinButton"] = join,
				["quitButton"] = quit,
				["statusText"] = status,
			});
		}

		private static void CreateLobbyPanel(Transform root, MainMenuController controller)
		{
			var panel = CreatePanel("Lobby Panel", root, new Vector2(0f, -80f), new Vector2(1500f, 760f));

			var info = CreateText("Info", panel, string.Empty, 28, new Vector2(0f, 300f), new Vector2(1400f, 100f), Color.white);

			var home = CreateText("Home List", panel, string.Empty, 36, new Vector2(-370f, 20f), new Vector2(620f, 420f), new Color(1f, 0.55f, 0.5f));
			home.alignment = TextAnchor.UpperCenter;

			var away = CreateText("Away List", panel, string.Empty, 36, new Vector2(370f, 20f), new Vector2(620f, 420f), new Color(0.55f, 0.7f, 1f));
			away.alignment = TextAnchor.UpperCenter;

			var matchLength = CreateButton("Match Length", panel, "Match length", new Vector2(-420f, -300f));
			var start = CreateButton("Start Match", panel, "Start Match", new Vector2(0f, -300f));
			var leave = CreateButton("Leave", panel, "Leave", new Vector2(420f, -300f));

			panel.gameObject.SetActive(false);

			SetReferences(controller, new Dictionary<string, Object>
			{
				["lobbyPanel"] = panel.gameObject,
				["lobbyInfoText"] = info,
				["homeListText"] = home,
				["awayListText"] = away,
				["matchLengthButton"] = matchLength,
				["startMatchButton"] = start,
				["leaveButton"] = leave,
			});
		}

		private static RectTransform CreatePanel(string name, Transform parent, Vector2 position, Vector2 size)
		{
			var panel = new GameObject(name).AddComponent<RectTransform>();
			panel.SetParent(parent, false);
			panel.anchoredPosition = position;
			panel.sizeDelta = size;

			panel.gameObject.AddComponent<Image>().color = PanelColour;

			return panel;
		}

		private static Text CreateText(string name, Transform parent, string content, int fontSize, Vector2 position, Vector2 size, Color colour)
		{
			var rectTransform = new GameObject(name).AddComponent<RectTransform>();
			rectTransform.SetParent(parent, false);
			rectTransform.anchoredPosition = position;
			rectTransform.sizeDelta = size;

			var text = rectTransform.gameObject.AddComponent<Text>();
			text.font = Font;
			text.text = content;
			text.fontSize = fontSize;
			text.fontStyle = FontStyle.Bold;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = colour;
			text.raycastTarget = false;

			return text;
		}

		private static Button CreateButton(string name, Transform parent, string label, Vector2 position)
		{
			var button = DefaultControls.CreateButton(Resources);

			button.name = name;

			var rectTransform = (RectTransform)button.transform;
			rectTransform.SetParent(parent, false);
			rectTransform.anchoredPosition = position;
			rectTransform.sizeDelta = new Vector2(380f, 70f);

			var text = button.GetComponentInChildren<Text>();
			text.font = Font;
			text.text = label;
			text.fontSize = 32;
			text.fontStyle = FontStyle.Bold;

			return button.GetComponent<Button>();
		}

		private static InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 position)
		{
			var field = DefaultControls.CreateInputField(Resources);

			field.name = name;

			var rectTransform = (RectTransform)field.transform;
			rectTransform.SetParent(parent, false);
			rectTransform.anchoredPosition = position;
			rectTransform.sizeDelta = new Vector2(500f, 64f);

			var input = field.GetComponent<InputField>();

			foreach (var text in field.GetComponentsInChildren<Text>(true))
			{
				text.font = Font;
				text.fontSize = 30;
			}

			((Text)input.placeholder).text = placeholder;

			return input;
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

		private static Font Font => UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

		private static DefaultControls.Resources Resources => new()
		{
			standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
			inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
		};
	}
}
