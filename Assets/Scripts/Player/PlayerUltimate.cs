using System;
using System.Collections;
using Combat;
using UnityEngine;

namespace Player
{
    /// Timed "Mech mode" activated by picking up the Thunder item (see Items.ThunderPickup).
    /// Reuses the existing PlayerController/PlayerCombat pipeline entirely (locomotion, camera,
    /// health, stagger) - only the visual model (astronaut -> 1.5x Mech_FinnTheFrog, both
    /// pre-built under VisualRoot and toggled active/inactive, mirroring how BossMechAI's own
    /// model stays pre-built-but-inert until its cutscene) and PlayerCombat's active attack
    /// profile swap. duration/mechScale are placeholder balance (see PlayerAmmo's magazineSize
    /// for the established "serialized tunable, not hardcoded" convention in this project),
    /// meant to be scaled by a future upgrades system.
    public class PlayerUltimate : MonoBehaviour
    {
        [SerializeField] private GameObject astronautVisualRoot;
        [SerializeField] private GameObject mechVisualRoot;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private PlayerShield playerShield;
        [SerializeField] private PlayerAmmo playerAmmo;
        [SerializeField] private ThirdPersonCameraController cameraController;

        [Header("Animation swap")]
        [Tooltip("Both AnimatorControllers share the same param/state-name contract (see " +
                 "PlayerSceneSetup.BuildMechAnimatorController's doc comment) - swapping which " +
                 "one every animator-driving component targets is all that's needed for the " +
                 "mech to get its own Idle/Walk/Jump/Kick/Shoot_Small/Shoot_Big/Death/emotes.")]
        [SerializeField] private Animator astronautAnimator;
        [SerializeField] private Animator mechAnimator;
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerAnimatorRelay animatorRelay;
        [SerializeField] private PlayerEmoteController emoteController;

        [Tooltip("The astronaut's headAnchor (Stun VFX) sits at astronaut height/scale - the Mech " +
                 "gets its own, authored as a child of MechVisual so it scales/positions with it.")]
        [SerializeField] private Transform mechHeadAnchor;

        [SerializeField] private float duration = 40f;
        [SerializeField] private float mechScale = 1.4f;
        [Tooltip("Added to the camera's normal follow distance while the (larger) mech visual " +
                 "is active, so it doesn't fill the frame.")]
        [SerializeField] private float cameraExtraDistance = 3f;
        [Tooltip("Added to the camera's normal target-offset height while the Mech is active - " +
                 "the base rig sits at 3.2, the Mech's pivot needs to sit at 4.2, so this is 1.")]
        [SerializeField] private float cameraExtraHeight = 1f;

        private Coroutine _countdownRoutine;

        public bool IsActive { get; private set; }
        public float TimeRemaining { get; private set; }
        public float Duration => duration;
        // Multiplies any VFX authored/tuned for the astronaut's size (e.g. PlayerController's
        // Stun VFX, ItemPickup's player-side pickup burst) so they read at the right scale next
        // to the larger Mech instead of looking tiny - 1 while not in Ultimate.
        public float VfxScaleMultiplier => IsActive ? mechScale : 1f;

        public event Action UltimateActivated;
        public event Action UltimateEnded;

        private void Awake()
        {
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
            if (playerShield == null) playerShield = GetComponent<PlayerShield>();
            if (cameraController == null) cameraController = FindFirstObjectByType<ThirdPersonCameraController>();
        }

        public void ActivateUltimate()
        {
            ActivateUltimate(duration);
        }

        public void ActivateUltimate(float overrideDuration)
        {
            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);

            IsActive = true;
            TimeRemaining = overrideDuration > 0f ? overrideDuration : duration;

            // Never hide the astronaut unless the mech visual actually exists to replace it -
            // a missing/failed mech import should degrade to "ultimate works, still looks like
            // the astronaut" rather than leaving the player invisible.
            if (mechVisualRoot != null)
            {
                if (astronautVisualRoot != null) astronautVisualRoot.SetActive(false);
                mechVisualRoot.SetActive(true);
                mechVisualRoot.transform.localScale = Vector3.one * mechScale;
            }

            playerCombat?.SetUltimateActive(true);
            playerShield?.ResetEnergy();
            playerAmmo?.SetInfiniteAmmo(true); // "constant shooting, no reload" while the mech's electric guns are out
            if (mechVisualRoot != null)
            {
                cameraController?.SetExtraDistance(cameraExtraDistance);
                cameraController?.SetExtraHeight(cameraExtraHeight);
            }

            if (mechAnimator != null)
            {
                playerController?.SetAnimator(mechAnimator);
                playerCombat?.SetAnimator(mechAnimator);
                animatorRelay?.SetAnimator(mechAnimator);
                playerHealth?.SetAnimator(mechAnimator);
                emoteController?.SetAnimator(mechAnimator);
            }

            if (mechHeadAnchor != null) playerController?.SetHeadAnchor(mechHeadAnchor);

            UltimateActivated?.Invoke();
            _countdownRoutine = StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            while (TimeRemaining > 0f)
            {
                TimeRemaining -= Time.deltaTime;
                yield return null;
            }

            EndUltimate();
        }

        public void EndUltimate()
        {
            if (!IsActive) return;
            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;

            IsActive = false;
            TimeRemaining = 0f;

            if (mechVisualRoot != null) mechVisualRoot.SetActive(false);
            if (astronautVisualRoot != null) astronautVisualRoot.SetActive(true);

            playerCombat?.SetUltimateActive(false);
            playerShield?.SetHeld(false); // force-release an active shield hold
            playerAmmo?.SetInfiniteAmmo(false);
            cameraController?.SetExtraDistance(0f);
            cameraController?.SetExtraHeight(0f);

            if (astronautAnimator != null)
            {
                playerController?.SetAnimator(astronautAnimator);
                playerCombat?.SetAnimator(astronautAnimator);
                animatorRelay?.SetAnimator(astronautAnimator);
                playerHealth?.SetAnimator(astronautAnimator);
                emoteController?.SetAnimator(astronautAnimator);
            }

            playerController?.RestoreDefaultHeadAnchor();

            UltimateEnded?.Invoke();
        }
    }
}
