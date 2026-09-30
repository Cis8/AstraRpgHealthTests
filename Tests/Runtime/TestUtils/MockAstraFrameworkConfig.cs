using ElectricDrill.AstraRpgFramework.Config;
using ElectricDrill.AstraRpgFramework.Events;
using ElectricDrill.AstraRpgFramework.Stats;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests.TestUtils
{
    /// <summary>
    /// Mock implementation of IAstraFrameworkConfig for EditMode tests that touch EntityCore/EntityStats
    /// and would otherwise hit AstraFrameworkConfigProvider's real, config-less Load() path.
    /// </summary>
    internal class MockAstraFrameworkConfig : IAstraFrameworkConfig
    {
        public EntityCoreGameEvent GlobalEntitySpawnedEvent { get; } = ScriptableObject.CreateInstance<EntityCoreGameEvent>();
        public EntityLevelUpGameEvent GlobalEntityLevelUpEvent { get; } = ScriptableObject.CreateInstance<EntityLevelUpGameEvent>();
        public EntityLevelDownGameEvent GlobalEntityLevelDownEvent { get; } = ScriptableObject.CreateInstance<EntityLevelDownGameEvent>();
        public StatChangedGameEvent GlobalStatChangedEvent { get; } = ScriptableObject.CreateInstance<StatChangedGameEvent>();
        public AttributeChangedGameEvent GlobalAttributeChangedEvent { get; } = ScriptableObject.CreateInstance<AttributeChangedGameEvent>();
        public StatSO ExperienceGainedModifierStat { get; } = ScriptableObject.CreateInstance<StatSO>();

        public static MockAstraFrameworkConfig CreateMinimal() => new();
    }
}
