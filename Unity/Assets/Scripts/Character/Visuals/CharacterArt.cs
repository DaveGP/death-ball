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
		private static readonly int BallState = Animator.StringToHash("BallState");
		private static readonly int BallCharge = Animator.StringToHash("BallCharge");

		[SerializeField] private float ballBlendTime = 0.08f;
		[SerializeField] private float catchDuration = 0.32f;
		[SerializeField] private float releaseDuration = 0.25f;

		private bool hasSpeed, hasBallState, hasBallCharge, wasCarrying, shotPending;
		private int ballLayer = -1;
		private float ballWeight, catchRemaining, releaseRemaining, firedCharge;
		private Vector3 firedDirection;
		private CharacterBallIK ballIK;

		public float BallLayerWeight => ballWeight;

		public bool TryGetBallPosition(float radius, out Vector3 position)
		{
			position = default;
			return ballIK != null && ballIK.TryGetBallPosition(radius, out position);
		}

		private void OnEnable()
		{
			if (body != null) body.ShotFired += OnShotFired;
		}

		private void OnDisable()
		{
			if (body != null) body.ShotFired -= OnShotFired;
			shotPending = false;
			catchRemaining = releaseRemaining = ballWeight = 0f;
			if (animator != null && ballLayer >= 0) animator.SetLayerWeight(ballLayer, 0f);
			if (ballIK != null) ballIK.SetPose(0, 0f, 0f, 0f, false, Vector3.zero);
		}

		private void OnShotFired(Vector3 direction, float charge)
		{
			shotPending = true;
			firedDirection = direction;
			firedCharge = Mathf.Clamp01(charge);
		}

		private static bool HasParameter(Animator source, int hash, AnimatorControllerParameterType type)
		{
			foreach (var parameter in source.parameters)
				if (parameter.nameHash == hash && parameter.type == type) return true;
			return false;
		}

		private void UpdateBallAnimation()
		{
			var carrying = ServiceLocator.TryGet(out Ball ball) && ball != null && ball.Carrier == body;
			var knockedOut = body.IsKnockedOut;
			catchRemaining = Mathf.Max(0f, catchRemaining - Time.deltaTime);
			releaseRemaining = Mathf.Max(0f, releaseRemaining - Time.deltaTime);
			if (knockedOut)
			{
				catchRemaining = releaseRemaining = 0f;
				shotPending = false;
			}
			else if (shotPending)
			{
				releaseRemaining = releaseDuration;
				catchRemaining = 0f;
				shotPending = false;
			}
			else if (carrying && !wasCarrying)
			{
				catchRemaining = catchDuration;
				releaseRemaining = 0f;
			}
			if (!carrying) catchRemaining = 0f;
			wasCarrying = carrying;
			var charging = carrying && !knockedOut && releaseRemaining <= 0f && body.IsChargingShot;
			var charge = charging ? Mathf.Clamp01(body.ShotCharge) : releaseRemaining > 0f ? firedCharge : 0f;
			var direction = charging ? body.ShotAim : releaseRemaining > 0f ? firedDirection : Vector3.zero;
			var state = knockedOut ? 0 : releaseRemaining > 0f ? 4 : !carrying ? 0 : charging ? 3 : catchRemaining > 0f ? 1 : 2;
			var ready = ballLayer >= 0 && hasBallState && hasBallCharge && ballIK != null;
			var targetWeight = ready && state != 0 ? 1f : 0f;
			ballWeight = Mathf.Lerp(ballWeight, targetWeight, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, ballBlendTime)));
			if (targetWeight == 0f && ballWeight < 0.001f) ballWeight = 0f;
			if (hasBallState) animator.SetInteger(BallState, state);
			if (hasBallCharge) animator.SetFloat(BallCharge, charge);
			if (ballLayer >= 0) animator.SetLayerWeight(ballLayer, ballWeight);
			if (ballIK != null) ballIK.SetPose(state, ballWeight, charge, 1f - catchRemaining / Mathf.Max(0.001f, catchDuration), charging || releaseRemaining > 0f, direction);
		}

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

				if (hasSpeed) animator.SetFloat(Speed, velocity.magnitude, speedDamping, Time.deltaTime);
				UpdateBallAnimation();
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
			ballIK = null;
			ballLayer = -1;
			hasSpeed = hasBallState = hasBallCharge = false;
			ballWeight = catchRemaining = releaseRemaining = 0f;

			if (art != null)
			{
				art.transform.SetLocalPositionAndRotation(Vector3.zero, Standing);

				animator = art.GetComponentInChildren<Animator>();
				if (animator != null)
				{
					animator.applyRootMotion = false;
					hasSpeed = HasParameter(animator, Speed, AnimatorControllerParameterType.Float);
					hasBallState = HasParameter(animator, BallState, AnimatorControllerParameterType.Int);
					hasBallCharge = HasParameter(animator, BallCharge, AnimatorControllerParameterType.Float);
					ballLayer = animator.GetLayerIndex("Ball");
					if (ballLayer >= 0 && animator.avatar != null && animator.isHuman)
					{
						ballIK = animator.GetComponent<CharacterBallIK>();
						if (ballIK == null) ballIK = animator.gameObject.AddComponent<CharacterBallIK>();
						ballIK.Initialise(animator, ballLayer);
						animator.SetLayerWeight(ballLayer, 0f);
					}
				}
			}

			foreach (var renderer in greyBox)
				renderer.enabled = art == null;

			marker.SetActive(art != null);
		}
	}
}
