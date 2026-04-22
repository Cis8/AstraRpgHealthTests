using ElectricDrill.AstraRpgFramework.Contexts;
using ElectricDrill.AstraRpgFramework.GameActions;
using ElectricDrill.AstraRpgFramework.GameActions.Actions.Component;
using ElectricDrill.AstraRpgFramework.GameActions.Actions.WithIHasEntity;
using ElectricDrill.AstraRpgFramework.Scaling.ScalingComponents;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgHealth.Config;
using ElectricDrill.AstraRpgHealth.Damage.CalculationPipeline;
using ElectricDrill.AstraRpgHealth.Events;
using ElectricDrill.AstraRpgHealth.Experience;
using ElectricDrill.AstraRpgHealth.Heal;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests.TestUtils
{
    /// <summary>
    /// Mock implementation of IAstraRpgHealthConfig for testing purposes.
    /// Provides sensible defaults and allows overriding specific properties.
    /// </summary>
    internal class MockAstraRpgHealthConfig : IAstraRpgHealthConfig
    {
        public AttributesScalingComponentSO HealthAttributesScaling { get; set; }
        public StatSO GenericPercentageHealAmountModifierStat { get; set; }
        public StatSO GenericFlatHealAmountModifierStat { get; set; }
        public StatSO GenericPercentageDamageModificationStat { get; set; }
        public StatSO GenericFlatDamageModificationStat { get; set; }
        public DamageCalculationStrategySO DefaultDamageCalculationCalculationStrategy { get; set; }
        public HealSourceSO HealthRegenerationSource { get; set; }
        public StatSO PassiveHealthRegenerationStat { get; set; }
        public float PassiveHealthRegenerationInterval { get; set; }
        public StatSO ManualHealthRegenerationStat { get; set; }
        public bool SuppressPassiveRegenerationEvents { get; set; }
        public bool SuppressManualRegenerationEvents { get; set; }
        public LifestealConfigSO LifestealConfig { get; set; }
        public bool SuppressLifestealEvents { get; set; }
        public GameAction<IHasEntity> DefaultOnDeathGameAction { get; set; }
        public GameAction<IHasEntity> DefaultOnResurrectionGameAction { get; set; }
        public HealSourceSO DefaultResurrectionSource { get; set; }
        public ExpCollectionStrategySO DefaultExpCollectionStrategy { get; set; }
        public PreDamageGameEvent GlobalPreDamageInfoEvent { get; set; }
        public DamageResolutionGameEvent GlobalDamageResolutionEvent { get; set; }
        public EntityDiedGameEvent GlobalEntityDiedEvent { get; set; }
        public EntityMaxHealthChangedGameEvent GlobalMaxHealthChangedEvent { get; set; }
        public EntityHealthChangedGameEvent GlobalHealthChangedEvent { get; set; }
        public HealthRatioChangedGameEvent GlobalHealthRatioChangedEvent { get; set; }
        public PreHealGameEvent GlobalPreHealEvent { get; set; }
        public EntityHealedGameEvent GlobalEntityHealedEvent { get; set; }
        public EntityResurrectedGameEvent GlobalEntityResurrectedEvent { get; set; }

        public MockAstraRpgHealthConfig()
        {
            // Initialize with sensible defaults
            PassiveHealthRegenerationInterval = 1f;
            SuppressPassiveRegenerationEvents = false;
            SuppressManualRegenerationEvents = false;
            SuppressLifestealEvents = false;
            
            // HealthAttributesScaling is null by default - only set if test needs it
            // This avoids validation errors when test entities don't have EntityAttributes
            HealthAttributesScaling = null;
            
            // Create a default damage calculation strategy
            DefaultDamageCalculationCalculationStrategy = ScriptableObject.CreateInstance<DamageCalculationStrategySO>();
            
            // Create a default death strategy
            DefaultOnDeathGameAction = ScriptableObject.CreateInstance<DoNothingEntityContextGameAction>();
            
            // Create a default HealSource for resurrection
            DefaultResurrectionSource = ScriptableObject.CreateInstance<HealSourceSO>();
            
            // Initialize the three required global events
            GlobalPreDamageInfoEvent = ScriptableObject.CreateInstance<PreDamageGameEvent>();
            GlobalDamageResolutionEvent = ScriptableObject.CreateInstance<DamageResolutionGameEvent>();
            GlobalEntityDiedEvent = ScriptableObject.CreateInstance<EntityDiedGameEvent>();
        }

        /// <summary>
        /// Creates a minimal mock config with only the essentials needed for basic tests.
        /// </summary>
        public static MockAstraRpgHealthConfig CreateMinimal()
        {
            return new MockAstraRpgHealthConfig();
        }

        /// <summary>
        /// Creates a mock config with a custom damage calculation strategy.
        /// </summary>
        public static MockAstraRpgHealthConfig WithDamageStrategy(DamageCalculationStrategySO strategy)
        {
            var config = new MockAstraRpgHealthConfig
            {
                DefaultDamageCalculationCalculationStrategy = strategy
            };
            return config;
        }

        /// <summary>
        /// Creates a mock config with a custom death strategy.
        /// </summary>
        public static MockAstraRpgHealthConfig WithDeathGameAction(GameAction<IHasEntity> strategy)
        {
            var config = new MockAstraRpgHealthConfig
            {
                DefaultOnDeathGameAction = strategy
            };
            return config;
        }
        
        /// <summary>
        /// Creates a mock config with a custom resurrection heal source.
        /// </summary>
        public static MockAstraRpgHealthConfig WithResurrectionSource(HealSourceSO healSource)
        {
            var config = new MockAstraRpgHealthConfig
            {
                DefaultResurrectionSource = healSource
            };
            return config;
        }

        /// <summary>
        /// Creates a mock config with a custom health attributes scaling component.
        /// Use this only if your test entities have EntityAttributes configured.
        /// </summary>
        public static MockAstraRpgHealthConfig WithHealthAttributesScaling(AttributesScalingComponentSO scalingComponent)
        {
            var config = new MockAstraRpgHealthConfig
            {
                HealthAttributesScaling = scalingComponent
            };
            return config;
        }
    }
}
