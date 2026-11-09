using Character.Networking;
using UnityEngine;


namespace Match
{
	/// <summary>
	/// Draws a carried ball in the carrier's hands on every machine. The ball's networked position
	/// goes from the carrier's machine to the server and back, so it trails a running carrier by
	/// metres; the visual follows the carrier as this machine sees them instead. When the ball
	/// changes hands or is let go, it glides across rather than popping. Letting go glides slowly,
	/// or a ball thrown on the run would slide back to the trailing position before flying forward.
	/// </summary>
	[RequireComponent(typeof(Ball))]
	public class BallVisual : MonoBehaviour
	{
		[SerializeField]
		private Transform visual;

		// How fast the visual closes the gap once someone has the ball, in metres per second
		[SerializeField]
		private float pickupBlendSpeed = 20f;

		// As above once it's loose, about the carrier's running speed so it doesn't slide backwards
		[SerializeField]
		private float releaseBlendSpeed = 5f;

		// A bigger gap than this is a reset rather than a throw, so it snaps
		[SerializeField]
		private float maxBlendDistance = 8f;

		private Ball ball;

		private CharacterNetworkController lastCarrier;

		private Vector3 blendOffset;
		private Character.Visuals.CharacterArt carrierArt;
		private bool lastHadHandTarget;
		private float visualRadius;

		private void Awake()
		{
			ball = GetComponent<Ball>();
			var renderer = visual.GetComponent<Renderer>();
			visualRadius = renderer != null ? Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y, renderer.bounds.extents.z) : 0f;
		}

		// Late, so bodies and the ball have taken this frame's network updates
		private void LateUpdate()
		{
			var carrier = ball.Carrier;

			if (carrier != lastCarrier)
				carrierArt = carrier != null ? carrier.GetComponent<Character.Visuals.CharacterArt>() : null;

			var target = carrier != null ? ball.HoldPosition(carrier) : transform.position;
			var handTarget = Vector3.zero;
			var hasHandTarget = carrierArt != null && carrierArt.TryGetBallPosition(visualRadius, out handTarget);
			if (hasHandTarget) target = handTarget;

			if (carrier != lastCarrier || hasHandTarget != lastHadHandTarget)
			{
				blendOffset = visual.position - target;

				if (blendOffset.magnitude > maxBlendDistance)
					blendOffset = Vector3.zero;

				lastCarrier = carrier;
				lastHadHandTarget = hasHandTarget;
			}

			blendOffset = Vector3.MoveTowards(blendOffset, Vector3.zero, (carrier != null ? pickupBlendSpeed : releaseBlendSpeed) * Time.deltaTime);

			visual.position = target + blendOffset;
		}
	}
}
