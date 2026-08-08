namespace Audio
{
    /// Every distinct one-shot/loop sound effect the game plays, looked up by SfxLibrary.Get and
    /// played via AudioManager.PlaySfx/PlayLoop. See Assets/Scripts/Audio/SfxLibrary.cs and the
    /// Tools/Audio/Build Sfx Library editor command for how these map onto imported AudioClips.
    public enum SfxId
    {
        PlayerShootPrimary,
        PlayerShootSecondary,
        PlayerMelee,
        PlayerHitReact,
        PlayerFootstep,
        PlayerDash,
        PlayerJump,
        PlayerLand,
        PlayerDeath,
        MechShootPrimaryLoop,
        MechShootSecondary,
        MechHitReact,
        MechLegStepA,
        MechLegStepB,
        MechShieldActivate,
        EnemyFlyingShoot,
        EnemyHitReact,
        EnemyDeath,
        Boss1Shoot,
        Boss1HitReact,
        Boss1Cutscene,
        Boss2Roar,
        Boss2ShootPrimaryLoop,
        Boss2Ricochet,
        Boss2TopDownV1,
        Boss2TopDownV2,
        Boss2JumpSlam,
        BossLegStepA,
        BossLegStepB,
        Boss2DeathImpact,
        Boss2DeathExplosion,
        ItemHealthPickup,
        ItemAmmoPickup,
        ItemMechUpgradePickup,
        ItemSpawn
    }
}
