using Audio;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Animation Event receiver for footstep sound. Unity delivers Animation Events via
    /// SendMessage on the exact GameObject the Animator component lives on - not upwards to a
    /// parent - so this has to sit on that GameObject itself rather than on PlayerAnimatorRelay
    /// (which lives on the player root, not the model). PlayerAnimatorRelay adds this
    /// automatically to whichever GameObject its active Animator points at (including on
    /// SetAnimator swaps between the astronaut and mech models), so nothing needs to be wired by
    /// hand in the prefab. See Assets/Editor/Player/PlayerFootstepEventsSetup.cs for where the
    /// "PlayFootstep" events actually get placed on the walk/run clips.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFootstepAnimationEvents : MonoBehaviour
    {
        public void PlayFootstep()
        {
            AudioManager.Instance.PlaySfx(SfxId.PlayerFootstep, transform.position);
        }
    }
}
