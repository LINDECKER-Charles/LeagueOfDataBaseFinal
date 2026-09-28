namespace LoDb.Domain.Derived.Stats;

/// <summary>
/// The game attributes the encyclopedia surfaces (champion base stats, item stat blocks).
/// </summary>
/// <remarks>
/// Written in snake_case by the API ("attack_damage"), the vocabulary of the translation keys
/// and of the stat icons.
/// </remarks>
public enum GameStat
{
    AttackDamage,
    AbilityPower,
    AttackSpeed,
    CritChance,
    LifeSteal,
    Health,
    HealthRegen,
    Armor,
    MagicResist,
    Mana,
    ManaRegen,
    MoveSpeed,
    AttackRange,
}
