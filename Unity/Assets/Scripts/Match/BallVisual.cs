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

		private void Awake()
		{
			ball = GetComponent<Ball>();
		}

		// Late, so bodies and the ball have taken this frame's network updates
		private void LateUpdate()
		{
			var carrier = ball.Carrier;

			var target = carrier != null ? ball.HoldPosition(carrier) : transform.position;

			if (carrier != lastCarrier)
			{
				blendOffset = visual.position - target;

				if (blendOffset.magnitude > maxBlendDistance)
					blendOffset = Vector3.zero;

				lastCarrier = carrier;
			}

			blendOffset = Vector3.MoveTowards(blendOffset, Vector3.zero, (carrier != null ? pickupBlendSpeed : releaseBlendSpeed) * Time.deltaTime);

			visual.position = target + blendOffset;
		}
	}
}
