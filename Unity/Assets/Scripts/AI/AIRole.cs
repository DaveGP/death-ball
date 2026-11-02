namespace AI
{
	public enum AIRole
	{
		// Get to the loose ball
		Chase = 0,

		// Take the ball to goal
		Carry = 1,

		// Run with our carrier and fight off whoever closes in on them
		Escort = 2,

		// Get open at a spot ahead of the play
		Support = 3,

		// Go after their carrier and tackle the ball loose
		Hunt = 4,

		// Stay on one opponent so they can't receive a pass
		Mark = 5,

		// Stand between the play and our ring
		Keep = 6,
	}
}
