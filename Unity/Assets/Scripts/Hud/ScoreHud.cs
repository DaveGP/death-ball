using Data.Enums;
using DependencyInjection;
using Match;
using UnityEngine;
using UnityEngine.UI;


namespace Hud
{
	/// <summary>
	/// The score along the top of the screen, and a banner when a goal goes in.
	/// Finds the networked match state once it has spawned.
	/// </summary>
	public class ScoreHud : MonoBehaviour
	{
		[SerializeField]
		private Text scoreText;

		[SerializeField]
		private Text goalText;

		[SerializeField]
		private float goalBannerDuration = 2.5f;

		private MatchState matchState;

		private float goalBannerHideTime;

		private void Update()
		{
			if (matchState == null && !TryFindMatchState())
			{
				scoreText.text = string.Empty;
				goalText.enabled = false;
				return;
			}

			scoreText.text = $"HOME  {matchState.ScoreFor(TeamSide.Home)}  -  {matchState.ScoreFor(TeamSide.Away)}  AWAY";

			goalText.enabled = Time.time < goalBannerHideTime;
		}

		private void OnDestroy()
		{
			if (matchState != null)
				matchState.GoalScored -= OnGoalScored;
		}

		private bool TryFindMatchState()
		{
			if (!ServiceLocator.TryGet(out matchState))
				return false;

			matchState.GoalScored += OnGoalScored;

			return true;
		}

		private void OnGoalScored(TeamSide team, int points, string description)
		{
			goalText.text = $"{team.ToString().ToUpper()} SCORES\n{description}  +{points}";

			goalBannerHideTime = Time.time + goalBannerDuration;
		}
	}
}
