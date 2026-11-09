using Data.Enums;
using DependencyInjection;
using Match;
using UnityEngine;
using UnityEngine.UI;


namespace Hud
{
	/// <summary>
	/// The score and clock along the top of the screen, and a banner for goals, sudden death
	/// and the result. Finds the networked match state once it has spawned.
	/// </summary>
	public class ScoreHud : MonoBehaviour
	{
		[SerializeField]
		private Text scoreText;

		[SerializeField]
		private Text clockText;

		[SerializeField]
		private Text bannerText;

		[SerializeField] private GameObject scoreboard;
		[SerializeField] private Text homeNameText;
		[SerializeField] private Text awayNameText;
		[SerializeField] private Image homeCrest;
		[SerializeField] private Image awayCrest;
		[SerializeField] private GameObject eventPlate;
		[SerializeField] private Text bannerDetailText;

		private MatchState matchState;
		private string homeName;
		private string awayName;

		private float bannerHideTime;

		private void Update()
		{
			if (matchState == null && !TryFindMatchState())
			{
				scoreText.text = string.Empty;
				clockText.text = string.Empty;
				if (scoreboard != null)
					scoreboard.SetActive(false);
				ShowBanner(false);
				return;
			}

			var home = matchState.NameFor(TeamSide.Home);
			var away = matchState.NameFor(TeamSide.Away);
			var homeScore = matchState.ScoreFor(TeamSide.Home);
			var awayScore = matchState.ScoreFor(TeamSide.Away);
			if (homeNameText != null && awayNameText != null)
			{
				UpdateTeam(homeNameText, homeCrest, home, ref homeName);
				UpdateTeam(awayNameText, awayCrest, away, ref awayName);
				scoreText.text = $"{homeScore} : {awayScore}";
			}
			else
			{
				scoreText.text = $"{home.ToUpperInvariant()}  {homeScore}  -  {awayScore}  {away.ToUpperInvariant()}";
			}

			clockText.text = ClockText();
			if (scoreboard != null)
				scoreboard.SetActive(true);
			ShowBanner(Time.time < bannerHideTime);
		}

		private static void UpdateTeam(Text label, Image crest, string name, ref string previous)
		{
			if (name == previous)
				return;

			previous = name;
			label.text = name.ToUpperInvariant();
			if (crest == null)
				return;

			crest.sprite = Resources.Load<Sprite>($"TeamCrests/{name}");
			crest.enabled = crest.sprite != null;
		}

		private void ShowBanner(bool visible)
		{
			if (eventPlate != null)
				eventPlate.SetActive(visible);
			else
				bannerText.enabled = visible;
		}

		private void OnDestroy()
		{
			if (matchState != null)
				matchState.Announced -= OnAnnounced;
		}

		private string ClockText()
		{
			switch (matchState.Phase)
			{
				case MatchPhase.SuddenDeath:
					return "SUDDEN DEATH";

				case MatchPhase.Ended:
					return "FINAL";

				default:
					var seconds = matchState.SecondsRemaining;
					return $"{seconds / 60}:{seconds % 60:00}";
			}
		}

		private bool TryFindMatchState()
		{
			if (!ServiceLocator.TryGet(out matchState))
				return false;

			matchState.Announced += OnAnnounced;

			return true;
		}

		private void OnAnnounced(string headline, string detail, float duration)
		{
			if (bannerDetailText != null)
			{
				bannerText.text = headline;
				bannerDetailText.text = detail;
			}
			else
			{
				bannerText.text = $"{headline}\n{detail}";
			}

			bannerHideTime = Time.time + duration;
		}
	}
}
