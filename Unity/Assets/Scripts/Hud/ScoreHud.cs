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

		private MatchState matchState;

		private float bannerHideTime;

		private void Update()
		{
			if (matchState == null && !TryFindMatchState())
			{
				scoreText.text = string.Empty;
				clockText.text = string.Empty;
				bannerText.enabled = false;
				return;
			}

			scoreText.text = $"{matchState.NameFor(TeamSide.Home).ToUpper()}  {matchState.ScoreFor(TeamSide.Home)}  -  {matchState.ScoreFor(TeamSide.Away)}  {matchState.NameFor(TeamSide.Away).ToUpper()}";

			clockText.text = ClockText();

			bannerText.enabled = Time.time < bannerHideTime;
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
			bannerText.text = $"{headline}\n{detail}";

			bannerHideTime = Time.time + duration;
		}
	}
}
