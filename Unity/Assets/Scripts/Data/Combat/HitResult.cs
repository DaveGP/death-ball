namespace Data.Combat
{
	public enum HitResult
	{
		// Dodged, or the target was already down
		Missed = 0,

		// Took reduced damage and kept their footing, and the ball
		Blocked = 1,

		Landed = 2,
	}
}
