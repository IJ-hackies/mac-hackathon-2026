namespace Tutorial
{
    /// Linear order the Overwatch-style tutorial walks the player through. Each stage has a
    /// physical TutorialGate blocking entry until the previous stage's requirement is met - see
    /// TutorialManager.
    public enum TutorialStage
    {
        Movement,
        Jump,
        Dash,
        LightAttack,
        HeavyAttack,
        Items,
        Overview,
        Complete,
    }
}
