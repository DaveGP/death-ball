using Character.Networking;
using Data.Enums;
using UnityEngine;


namespace Character.Visuals
{
	/// <summary>
	/// Tints a grey-box body by team until there are real character models.
	/// </summary>
	public class TeamColour : MonoBehaviour
	{
		[SerializeField]
		private Renderer[] renderers;

		[SerializeField]
		private Color homeColour = new(0.8f, 0.15f, 0.1f);

		[SerializeField]
		private Color awayColour = new(0.1f, 0.3f, 0.85f);

		[SerializeField]
		private float humanBrightness = 1.6f;

		private CharacterNetworkController body;

		private MaterialPropertyBlock propertyBlock;

		private TeamSide? appliedTeam;

		private bool appliedHuman;

		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

		private void Awake()
		{
			body = GetComponent<CharacterNetworkController>();

			propertyBlock = new MaterialPropertyBlock();
		}

		private void Update()
		{
			if (!body.IsSpawned || (appliedTeam == body.Team && appliedHuman == body.IsHumanControlled))
				return;

			appliedTeam = body.Team;

			appliedHuman = body.IsHumanControlled;

			var colour = body.Team == TeamSide.Home ? homeColour : awayColour;

			// Bodies a human controls stand out from the AI
			if (appliedHuman)
				colour *= humanBrightness;

			propertyBlock.SetColor(BaseColor, colour);

			foreach (var renderer in renderers)
				renderer.SetPropertyBlock(propertyBlock);
		}
	}
}
