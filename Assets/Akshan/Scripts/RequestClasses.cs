using UnityEngine;
using System;
using System.Collections.Generic;

public class ActionRequestClass
{
    public String ActionName;
    public String IPAdd;
}

public enum Ability
{
    Healer,
    Invicible,
    Randy,
    Striker,
    Sniper,
    Tank,
    Saboteur,
    Phantom,
    Berserker
}

public class ability
{
    public Ability AbilityClass;
    public float CoolDown;

    // NEW: each ability gets a signature hull color, applied via
    // BoatHealth.SetHullColor() when a ship spawns with this ability.
    public Color HullColor;

    // NEW: human-readable descriptions shown to the player (Phone2Script /
    // CombinedSceneScript) so they know what their passive stat changes
    // and active button actually do. Written directly from the mechanics
    // in GameHandler.ApplyAbilityPassives() and RequestHandler()'s "AB"
    // branch, not invented.
    public string PassiveDescription;
    public string ActiveDescription;
}

public static class Abilities
{
    public static readonly ability Invincible = new ability
    {
        AbilityClass = Ability.Invicible,
        CoolDown = 30f,
        HullColor = new Color(1f, 0.85f, 0.1f), // gold - "untouchable"
        PassiveDescription = "Reduced max health (50) in exchange for a powerful active.",
        ActiveDescription = "Become briefly invincible - incoming damage is nullified for 5 seconds."
    };

    public static readonly ability Healer = new ability
    {
        AbilityClass = Ability.Healer,
        CoolDown = 10f,
        HullColor = new Color(0.2f, 0.9f, 0.35f), // green - restorative
        PassiveDescription = "Weaker cannon (5 damage per hit) in exchange for a strong, spammable heal.",
        ActiveDescription = "Instantly heal for 25% of your CURRENT health (capped at max health)."
    };

    public static readonly ability Randy = new ability
    {
        AbilityClass = Ability.Randy,
        CoolDown = 100f,
        HullColor = new Color(0.7f, 0.1f, 0.9f), // magenta/purple - chaos
        PassiveDescription = "Unpredictable cannon damage each match (rolls 1-10).",
        ActiveDescription = "Instantly destroys a random ship in the match - could be an enemy, or your own."
    };

    public static readonly ability Striker = new ability
    {
        AbilityClass = Ability.Striker,
        CoolDown = 20f,
        HullColor = new Color(0.1f, 0.7f, 1f), // cyan - speed
        PassiveDescription = "Increased acceleration (+40%) and top speed (+30%).",
        ActiveDescription = "Performs a quick dash forward."
    };

    // ---------- New abilities ----------

    public static readonly ability Sniper = new ability
    {
        AbilityClass = Ability.Sniper,
        CoolDown = 25f,
        HullColor = new Color(0.6f, 0.05f, 0.05f), // dark red - precision/danger
        PassiveDescription = "Higher cannon damage (12) but reduced max health (70).",
        ActiveDescription = "Focus Shot - doubles cannon damage for 6 seconds."
    };

    public static readonly ability Tank = new ability
    {
        AbilityClass = Ability.Tank,
        CoolDown = 35f,
        HullColor = new Color(0.45f, 0.4f, 0.35f), // armor grey/brown
        PassiveDescription = "Much higher max health (150) but slower acceleration (-25%).",
        ActiveDescription = "Shield - reduces incoming damage by 50% for 5 seconds."
    };

    public static readonly ability Saboteur = new ability
    {
        AbilityClass = Ability.Saboteur,
        CoolDown = 20f,
        HullColor = new Color(0.1f, 0.25f, 0.15f), // dark stealthy green
        PassiveDescription = "No stat changes - relies entirely on its active.",
        ActiveDescription = "Slows a random ENEMY ship's speed by 60% for 4 seconds."
    };

    public static readonly ability Phantom = new ability
    {
        AbilityClass = Ability.Phantom,
        CoolDown = 15f,
        HullColor = new Color(0.85f, 0.9f, 1f), // pale ghostly blue-white
        PassiveDescription = "Improved turning speed (+30%).",
        ActiveDescription = "Blink - instantly teleports forward 15 units."
    };

    public static readonly ability Berserker = new ability
    {
        AbilityClass = Ability.Berserker,
        CoolDown = 40f,
        HullColor = new Color(0.8f, 0.2f, 0.05f), // fiery orange-red - rage
        PassiveDescription = "Much higher max health (130) but weaker cannon (3 damage).",
        ActiveDescription = "Cleave - deals 15 damage to all enemy ships within 15 units."
    };

    public static readonly IReadOnlyDictionary<Ability, ability> All = new Dictionary<Ability, ability>
    {
        { Ability.Invicible, Invincible },
        { Ability.Healer, Healer },
        { Ability.Randy, Randy },
        { Ability.Striker, Striker },
        { Ability.Sniper, Sniper },
        { Ability.Tank, Tank },
        { Ability.Saboteur, Saboteur },
        { Ability.Phantom, Phantom },
        { Ability.Berserker, Berserker }
    };

    public static ability Get(Ability type) => All[type];
}

public static class AbilityUtils
{
    private static readonly Ability[] AllValues = (Ability[])Enum.GetValues(typeof(Ability));

    public static Ability GetRandom() => AllValues[UnityEngine.Random.Range(0, AllValues.Length)];
}