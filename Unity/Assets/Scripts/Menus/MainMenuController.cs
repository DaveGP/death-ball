using System.Collections.Generic;
using System.Linq;
using System.Text;
using Data.Enums;
using DependencyInjection;
using Lobby;
using Match;
using Players;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace Menus
{
	/// <summary>
	/// Drives the start screen and lobby of the main menu. Play Local hosts with no network; Host
	/// Online makes an online game over Unity Relay and shows its join code; Join enters a friend's
	/// code. In the lobby, every device that presses a button becomes a local player, who picks a
	/// team with left and right and a player type with up and down. The host sets the match length
	/// and starts the match.
	/// </summary>
	public class MainMenuController : MonoBehaviour
	{
		// How far a stick or keys must be pushed to count as a choice
		private const float ChoiceThreshold = 0.5f;

		[Header("Start screen")]
		[SerializeField]
		private GameObject startPanel;

		[SerializeField]
		private InputField nameInput;

		[SerializeField]
		private InputField codeInput;

		[SerializeField]
		private Button localButton;

		[SerializeField]
		private Button hostButton;

		[SerializeField]
		private Button joinButton;

		[SerializeField]
		private Button quitButton;

		[SerializeField]
		private Text statusText;

		[Header("Lobby")]
		[SerializeField]
		private GameObject lobbyPanel;

		[SerializeField]
		private Text lobbyInfoText;

		[SerializeField]
		private Text homeListText;

		[SerializeField]
		private Text awayListText;

		[SerializeField]
		private Button matchLengthButton;

		[SerializeField]
		private Button startMatchButton;

		[SerializeField]
		private Button leaveButton;

		private NetworkManager networkManager;

		private GameSession gameSession;

		private LocalPlayerRegistry localPlayers;

		private MatchSettings matchSettings;

		private LobbyState lobbyState;

		// Last stick or key direction per local player, so a choice changes once per push
		private readonly Dictionary<int, Vector2> lastChoice = new();

		private void Start()
		{
			networkManager = NetworkManager.Singleton;

			gameSession = networkManager.GetComponent<GameSession>();

			localPlayers = ServiceLocator.Get<LocalPlayerRegistry>();

			matchSettings = ServiceLocator.Get<MatchSettings>();

			if (gameSession == null)
				Debug.LogError($"{nameof(gameSession)} is null");

			nameInput.text = LocalPlayerManager.PlayerName;

			nameInput.onEndEdit.AddListener(text => LocalPlayerManager.PlayerName = text);

			localButton.onClick.AddListener(PlayLocal);
			hostButton.onClick.AddListener(HostOnline);
			joinButton.onClick.AddListener(JoinOnline);
			quitButton.onClick.AddListener(Quit);

			// A browser tab can't close itself
			quitButton.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
			matchLengthButton.onClick.AddListener(CycleMatchLength);
			startMatchButton.onClick.AddListener(StartMatch);
			leaveButton.onClick.AddListener(() => gameSession.Leave());

			statusText.text = string.Empty;
		}

		private void OnDestroy()
		{
			if (lobbyState != null)
				lobbyState.Changed -= RefreshLobby;
		}

		private void Update()
		{
			var inLobby = networkManager.IsListening && networkManager.IsConnectedClient;

			startPanel.SetActive(!inLobby);
			lobbyPanel.SetActive(inLobby);

			if (!inLobby)
			{
				statusText.text = gameSession.Status;

				// A controller can start local play from here too. Not Enter, which also ends typing a name.
				if (WasStartPressed(false) && !gameSession.IsBusy)
					PlayLocal();

				return;
			}

			FindLobbyState();

			HandleLocalChoices();

			var isHost = networkManager.IsServer;

			matchLengthButton.gameObject.SetActive(isHost);
			startMatchButton.gameObject.SetActive(isHost);

			if (isHost && WasStartPressed(true))
				StartMatch();

			RefreshLobby();
		}

		private void PlayLocal()
		{
			LocalPlayerManager.PlayerName = nameInput.text;

			gameSession.PlayLocal();
		}

		private async void HostOnline()
		{
			LocalPlayerManager.PlayerName = nameInput.text;

			await gameSession.HostOnline();
		}

		private async void JoinOnline()
		{
			LocalPlayerManager.PlayerName = nameInput.text;

			if (string.IsNullOrWhiteSpace(codeInput.text))
			{
				statusText.text = "Type in the join code your friend was given";
				return;
			}

			await gameSession.JoinOnline(codeInput.text);
		}

		private static void Quit()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		private void StartMatch()
		{
			if (!localPlayers.Players.Any() && lobbyState != null && !lobbyState.Entries.Any())
			{
				lobbyInfoText.text = "Press a button on a keyboard or controller to add a player first";
				return;
			}

			gameSession.StartMatch();
		}

		private void CycleMatchLength()
		{
			var lengths = MatchSettings.MatchLengths;

			var next = lengths[(System.Array.IndexOf(lengths, matchSettings.MatchLengthSeconds) + 1) % lengths.Length];

			matchSettings.MatchLengthSeconds = next;

			lobbyState?.SetMatchLength(next);
		}

		private void FindLobbyState()
		{
			if (lobbyState != null || !ServiceLocator.TryGet(out LobbyState found))
				return;

			lobbyState = found;
			lobbyState.Changed += RefreshLobby;

			if (networkManager.IsServer)
				lobbyState.SetMatchLength(matchSettings.MatchLengthSeconds);

			// Everyone on this machine so far
			foreach (var player in localPlayers.Players)
				Submit(player);
		}

		// Left and right pick a team, up and down pick a player type
		private void HandleLocalChoices()
		{
			foreach (var player in localPlayers.Players)
			{
				var move = player.InputProvider.ConsumeCharacterInput().Move;

				var isNew = !lastChoice.TryGetValue(player.Index, out var last);

				lastChoice[player.Index] = move;

				if (isNew)
				{
					Submit(player);
					continue;
				}

				var changed = false;

				if (Pushed(move.x, last.x) != 0)
				{
					player.Team = move.x > 0f ? TeamSide.Away : TeamSide.Home;
					changed = true;
				}

				var typeStep = Pushed(move.y, last.y);

				if (typeStep != 0)
				{
					var types = System.Enum.GetValues(typeof(PlayerType)).Length;

					player.PreferredType = (PlayerType)(((int)player.PreferredType - typeStep + types) % types);
					changed = true;
				}

				if (changed)
					Submit(player);
			}
		}

		// 1 or -1 the moment a direction is pushed past the threshold, 0 otherwise
		private static int Pushed(float value, float previous)
		{
			if (value > ChoiceThreshold && previous <= ChoiceThreshold)
				return 1;

			if (value < -ChoiceThreshold && previous >= -ChoiceThreshold)
				return -1;

			return 0;
		}

		private void Submit(LocalPlayerController player)
		{
			lobbyState?.Submit(player.Index, player.Name, player.Team, player.PreferredType);
		}

		private void RefreshLobby()
		{
			if (lobbyState == null)
			{
				lobbyInfoText.text = "Joining lobby...";
				return;
			}

			var isHost = networkManager.IsServer;

			var seconds = lobbyState.MatchLength > 0 ? lobbyState.MatchLength : matchSettings.MatchLengthSeconds;

			var match = $"Match length {seconds / 60}:{seconds % 60:00}";

			var controls = localPlayers.Count == 0
				? "Press a button on a keyboard or controller to add a player"
				: "Left / right: team    Up / down: player type    Another controller: add a player";

			var hosting = gameSession.IsOnline ? $"Hosting online - join code {gameSession.JoinCode}" : "Playing locally";

			lobbyInfoText.text = isHost
				? $"{hosting}. {match}. Press Start or Enter to begin.\n{controls}"
				: $"Waiting for the host to start. {match}.\n{controls}";

			homeListText.text = TeamList(TeamSide.Home);
			awayListText.text = TeamList(TeamSide.Away);

			matchLengthButton.GetComponentInChildren<Text>().text = match;
		}

		private string TeamList(TeamSide team)
		{
			var text = new StringBuilder();

			text.AppendLine(team == TeamSide.Home ? "HOME" : "AWAY");
			text.AppendLine();

			var localClientId = networkManager.LocalClientId;

			foreach (var entry in lobbyState.Entries.Where(e => e.Team == team).OrderBy(e => e.ClientId).ThenBy(e => e.LocalIndex))
			{
				var isLocal = entry.ClientId == localClientId;

				text.AppendLine($"{(isLocal ? "> " : "")}{entry.Name}  {entry.Type}");
			}

			return text.ToString();
		}

		private static bool WasStartPressed(bool includeEnter)
			=> (includeEnter && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
			   || Gamepad.all.Any(pad => pad.startButton.wasPressedThisFrame);
	}
}
