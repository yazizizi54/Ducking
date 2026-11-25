using UnityEngine;

namespace DuckKing.Input
{
    /// <summary>
    /// Two-player shared keyboard input using the legacy Input Manager.
    ///
    /// Movement keys stay with each duck, but the JUMP keys are deliberately
    /// swapped: duck 1 jumps with the arrow-up key and duck 2 jumps with W.
    /// That is the story hook, not a mistake. Two ducks alone cannot climb
    /// because each one is pressing the other duck's jump key, so only by
    /// cooperating can they move.
    ///
    /// Duck 1: A / D to move, Arrow Up to jump.
    /// Duck 2: Left / Right arrows to move, W to jump.
    /// </summary>
    public class KeyboardDuckInput : IDuckInput
    {
        public enum Scheme
        {
            PlayerOne,
            PlayerTwo
        }

        private readonly KeyCode _left;
        private readonly KeyCode _right;
        private readonly KeyCode _jump;

        public KeyboardDuckInput(Scheme scheme)
        {
            if (scheme == Scheme.PlayerOne)
            {
                _left = KeyCode.A;
                _right = KeyCode.D;
                // Swapped on purpose: duck 1 jumps with the arrow-up key.
                _jump = KeyCode.UpArrow;
            }
            else
            {
                _left = KeyCode.LeftArrow;
                _right = KeyCode.RightArrow;
                // Swapped on purpose: duck 2 jumps with the W key.
                _jump = KeyCode.W;
            }
        }

        public float Horizontal
        {
            get
            {
                float axis = 0f;
                if (UnityEngine.Input.GetKey(_left)) axis -= 1f;
                if (UnityEngine.Input.GetKey(_right)) axis += 1f;
                return axis;
            }
        }

        public bool JumpPressedThisFrame => UnityEngine.Input.GetKeyDown(_jump);

        public bool JumpHeld => UnityEngine.Input.GetKey(_jump);
    }
}