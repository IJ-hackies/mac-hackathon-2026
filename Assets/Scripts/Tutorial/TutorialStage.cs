namespace Tutorial
{
    /// Linear order the Overwatch-style tutorial walks the player through. Each stage has a
    /// physical TutorialGate blocking entry until the previous stage's requirement is met - see
    /// TutorialManager.
    public enum TutorialStage
    {
        Movement,
        Emote,
        Jump,
        Dash,
        LightAttack,
        Reload,
        HeavyAttack,
        Melee,
        Items,
        Overview,
        Complete,
    }
}
