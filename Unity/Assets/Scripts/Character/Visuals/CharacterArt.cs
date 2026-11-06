using Character.Networking;
using Data.Enums;
using Data.ScriptableObjects;
using DependencyInjection;
using Match;
using UnityEngine;


namespace Character.Visuals
{
	/// <summary>
	/// Shows the model for this body's role on whichever team its side is playing as, in place of the grey-box
	/// shapes. The match can change teams between games, so it keeps checking. Only for looks, and every machine works
	/// it out for itself from the match state.
	/// </summary>
	[RequireComponent(typeof(CharacterNetworkController))]
	public class CharacterArt : MonoBehaviour
	{
		[SerializeField]
		private Renderer[] greyBox;

		// The disc under a model, in its side's colour, because the models are in their team's colours instead
		[SerializeField]
		private GameObject marker;

		[SerializeField]
		private float humanMarkerScale = 1.4f;

		// How quickly the animation catches up with the body's speed
		[SerializeField]
		private float speedDamping = 0.1f;

		// How quickly a knocked out model falls
		[SerializeField]
		private float fallSpeed = 240f;

		private static readonly int Speed = Animator.StringToHash("Speed");

		private static readonly Quaternion Standing = Quaternion.identity;

		private static readonly Quaternion Fallen = Quaternion.Euler(-90f, 0f, 0f);

		private CharacterNetworkController body;

		private TeamRoster roster;

		private Vector3 markerScale;

		private GameObject shownPrefab;

		private GameObject art;

		private Animator animator;

		private void Awake()
		{
			body = GetComponent<CharacterNetworkController>();

			roster = TeamRoster.Load();

			markerScale = marker.transform.localScale;
		}

		private void Update()
		{
			if (roster == null || !body.IsSpawned || !ServiceLocator.TryGet(out MatchState matchState))
				return;

			var prefab = roster.PlayerFor(matchState.TeamIndexFor(body.Team), body.Type);

			if (prefab != shownPrefab)
				Show(prefab);

			if (art == null)
				return;

			if (animator != null)
			{
				var velocity = body.IsKnockedOut ? Vector3.zero : body.Velocity;
				velocity.y = 0f;

				animator.SetFloat(Speed, velocity.magnitude, speedDamping, Time.deltaTime);
			}

			art.transform.localRotation = Quaternion.RotateTowards(
				art.transform.localRotation,
				body.IsKnockedOut ? Fallen : Standing,
				fallSpeed * Time.deltaTime);

			var scale = body.IsHumanControlled ? humanMarkerScale : 1f;

			marker.transform.localScale = new Vector3(markerScale.x * scale, markerScale.y, markerScale.z * scale);
		}

		private void Show(GameObject prefab)
		{
			if (art != null)
				Destroy(art);

			shownPrefab = prefab;

			art = prefab != null ? Instantiate(prefab, transform) : null;

			animator = null;

			if (art != null)
			{
				art.transform.SetLocalPositionAndRotation(Vector3.zero, Standing);

				animator = art.GetComponentInChildren<Animator>();
			}

			foreach (var renderer in greyBox)
				renderer.enabled = art == null;

			marker.SetActive(art != null);
		}
	}
}
