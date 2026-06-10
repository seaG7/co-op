namespace Gameplay.Player.Animation
{
    public readonly struct AirborneDecision
    {
        public readonly bool FireJump;
        public readonly bool FireLand;
        public AirborneDecision(bool fireJump, bool fireLand)
        {
            FireJump = fireJump;
            FireLand = fireLand;
        }
    }

    public static class AnimatorStateResolver
    {
        // Fires Jump exactly ONCE, on the frame the player leaves the ground with upward velocity.
        // (Previously two sources could fire it on consecutive frames -> double twitch.)
        public static AirborneDecision Evaluate(
            bool prevGrounded, bool curGrounded, float verticalVelocity, float jumpDetectVelocity)
        {
            bool leftGround = prevGrounded && !curGrounded;
            bool landed = !prevGrounded && curGrounded;
            bool fireJump = leftGround && verticalVelocity > jumpDetectVelocity;
            return new AirborneDecision(fireJump, landed);
        }
    }
}
