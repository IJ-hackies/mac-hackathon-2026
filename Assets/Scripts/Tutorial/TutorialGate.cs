using UnityEngine;

namespace Tutorial
{
    /// A solid energy-barrier that physically blocks the corridor until its stage's requirement
    /// is met (Overwatch-style progression gating). TutorialManager opens the one it's holding a
    /// reference to when that stage's requirement completes.
    ///
    /// A single stage boundary may be built from several separate barrier pieces instead of one
    /// wide object (e.g. multiple panels spanning a doorway) - TutorialManager only ever holds
    /// one TutorialGate reference per boundary, so wire the other pieces into that one gate's
    /// Linked Gates list in the Inspector and Open() will cascade to all of them together.
    public class TutorialGate : MonoBehaviour
    {
        [Tooltip("Other TutorialGate pieces that make up the same physical barrier - they open " +
                 "together when this one does. Leave empty for a single-piece gate.")]
        [SerializeField] private TutorialGate[] linkedGates;

        private bool _open;

        public bool IsOpen => _open;

        public void Open()
        {
            if (_open) return;
            _open = true;
            gameObject.SetActive(false);

            if (linkedGates == null) return;
            foreach (var linked in linkedGates)
            {
                if (linked != null) linked.Open();
            }
        }
    }
}
