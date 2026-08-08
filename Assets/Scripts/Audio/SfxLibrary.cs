using System.Collections.Generic;
using UnityEngine;

namespace Audio
{
    /// Flat table of every SfxDefinition in the game, authored as a ScriptableObject asset so it
    /// can be assigned in the Inspector (AudioManager.library) or loaded at runtime via
    /// Resources.Load (see AudioManager's lazy-create fallback). Populate via the
    /// Tools/Audio/Build Sfx Library editor menu command (Assets/Editor/Audio/BuildSfxLibrary.cs)
    /// rather than hand-dragging every clip.
    [CreateAssetMenu(fileName = "SfxLibrary", menuName = "Audio/Sfx Library")]
    public class SfxLibrary : ScriptableObject
    {
        public List<SfxDefinition> entries = new List<SfxDefinition>();

        private Dictionary<SfxId, SfxDefinition> _lookup;

        public SfxDefinition Get(SfxId id)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(id, out var definition) ? definition : null;
        }

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<SfxId, SfxDefinition>();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                _lookup[entry.id] = entry;
            }
        }
    }
}
