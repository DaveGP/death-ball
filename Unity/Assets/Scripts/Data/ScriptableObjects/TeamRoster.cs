using System;
using Data.Enums;
using UnityEngine;


namespace Data.ScriptableObjects
{
	/// <summary>
	/// The teams a match can pick from, and the model for each role. Made by Death Ball > Build Team Art from the
	/// Synty packs, which stay out of git, so a fresh clone doesn't have one. Without it there are no teams to pick,
	/// and matches are Home against Away with grey-box bodies.
	/// </summary>
	public class TeamRoster : ScriptableObject
	{
		private const string ResourceName = "TeamRoster";

		[Serializable]
		public class Team
		{
			public string Name;

			// Bruiser, Runner, Thrower, Enforcer, in PlayerType order
			public GameObject[] Players;
		}

		[SerializeField]
		private Team[] teams;

		public int Count => teams?.Length ?? 0;

		public static TeamRoster Load() => Resources.Load<TeamRoster>(ResourceName);

		public void Set(Team[] newTeams) => teams = newTeams;

		public string NameOf(int team) => IsValid(team) ? teams[team].Name : null;

		public GameObject PlayerFor(int team, PlayerType type)
			=> IsValid(team) && (int)type < teams[team].Players.Length ? teams[team].Players[(int)type] : null;

		private bool IsValid(int team) => team >= 0 && team < Count;
	}
}
