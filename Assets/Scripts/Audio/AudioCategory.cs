namespace Audio
{
    /// Broad mixing buckets used by AudioManager's category volume multipliers - there is no
    /// Unity AudioMixer asset in this project (deliberately, see AudioManager's class doc); each
    /// category is just a linear multiplier applied on top of a clip's own SfxDefinition.volume.
    public enum AudioCategory
    {
        Weapons,
        Impacts,
        Movement,
        Voice,
        UI,
        Music
    }
}
