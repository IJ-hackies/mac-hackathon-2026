using Player;
using UnityEngine;

namespace Tutorial
{
    /// Reusable trigger volume for the Overview stage: opens a modal info popup (a labeled
    /// station pillar, a wave-system panel) the moment the player steps in - TutorialManager
    /// suspends movement/look for as long as it's open, and only its own X button closes it, so
    /// there's no OnTriggerExit behavior to speak of. Also used for the final continue zone,
    /// which tells TutorialManager to finish the tutorial instead of/alongside showing a message.
    /// Not used for stage-gating - TutorialGate/TutorialManager's own requirement tracking owns
    /// that.
    [RequireComponent(typeof(Collider))]
    public class TutorialZone : MonoBehaviour
    {
        [SerializeField, TextArea] private string message;
        [SerializeField] private bool advancesToComplete;
        [SerializeField] private TutorialManager manager;

        public void Configure(TutorialManager owner, string infoMessage, bool advances)
        {
            manager = owner;
            message = infoMessage;
            advancesToComplete = advances;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;

            if (manager == null)
            {
                Debug.LogWarning($"TutorialZone \"{name}\": entered by the player but its Manager " +
                                  "field is empty, so it can't show a message or advance anything.", this);
                return;
            }

            bool didSomething = false;
            if (!string.IsNullOrEmpty(message))
            {
                manager.ShowInfo(message);
                didSomething = true;
            }
            if (advancesToComplete)
            {
                manager.NotifyOverviewFinished();
                didSomething = true;
            }

            if (!didSomething)
            {
                Debug.LogWarning($"TutorialZone \"{name}\": entered by the player, but Message is " +
                                  "empty and Advances To Complete is off - it has nothing to do. " +
                                  "Set one of those in the Inspector.", this);
            }
        }

    }
}
