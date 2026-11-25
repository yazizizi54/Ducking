namespace DuckKing.Input
{
    /// <summary>
    /// Abstraction over duck input.
    /// Local keyboard is one implementation. If LAN multiplayer is ever added,
    /// only a new implementation is needed; the character controller stays untouched.
    /// </summary>
    public interface IDuckInput
    {
        /// <summary>Horizontal axis: -1 left, 0 idle, 1 right.</summary>
        float Horizontal { get; }

        /// <summary>True only on the frame the jump key went down.</summary>
        bool JumpPressedThisFrame { get; }

        /// <summary>True while the jump key is held, used for variable jump height.</summary>
        bool JumpHeld { get; }
    }
}