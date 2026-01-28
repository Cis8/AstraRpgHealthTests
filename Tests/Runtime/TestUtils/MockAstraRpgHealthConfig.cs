using ElectricDrill.AstraRpgFramework.GameActions;
using ElectricDrill.AstraRpgFramework.GameActions.Actions.SO.Component;
using ElectricDrill.AstraRpgFramework.Scaling.ScalingComponents;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraRpgHealth.Config;
using ElectricDrill.AstraRpgHealth.Damage;
using ElectricDrill.AstraRpgHealth.Damage.CalculationPipeline;
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
        public AttributesScalingComponent HealthAttributesScaling { get; set; }
        public Stat GenericHealAmountModifierStat { get; set; }
        public SerializableDictionary<HealSource, Stat> HealSourceModifications { get; set; }
        public Stat GenericPercentageDamageModificationStat { get; set; }
        public Stat GenericFlatDamageModificationStat { get; set; }
        public DamageCalculationStrategy DefaultDamageCalculationCalculationStrategy { get; set; }
        public HealSource PassiveHealthRegenerationSource { get; set; }
        public Stat PassiveHealthRegenerationStat { get; set; }
        public float PassiveHealthRegenerationInterval { get; set; }
        public Stat ManualHealthRegenerationStat { get; set; }
        public LifestealConfig LifestealConfig { get; set; }
        public GameAction<UnityEngine.Component> DefaultOnDeathGameAction { get; set; }
        public GameAction<UnityEngine.Component> DefaultOnResurrectionGameAction { get; set; }
        public HealSource DefaultResurrectionSource { get; set; }
        public ExpCollectionStrategy DefaultExpCollectionStrategy { get; set; }

        public MockAstraRpgHealthConfig()
        {
            // Initialize with sensible defaults
            PassiveHealthRegenerationInterval = 1f;
            
            // HealthAttributesScaling is null by default - only set if test needs it
            // This avoids validation errors when test entities don't have EntityAttributes
            HealthAttributesScaling = null;
            
            // Create a default damage calculation strategy
            DefaultDamageCalculationCalculationStrategy = ScriptableObject.CreateInstance<DamageCalculationStrategy>();
            
            // Create a default death strategy
            DefaultOnDeathGameAction = ScriptableObject.CreateInstance<DoNothingComponentGameAction>();
            
            // Create a default HealSource for resurrection
            DefaultResurrectionSource = ScriptableObject.CreateInstance<HealSource>();
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
        public static MockAstraRpgHealthConfig WithDamageStrategy(DamageCalculationStrategy strategy)
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
        public static MockAstraRpgHealthConfig WithDeathGameAction(GameAction<UnityEngine.Component> strategy)
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
        public static MockAstraRpgHealthConfig WithResurrectionSource(HealSource healSource)
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
        public static MockAstraRpgHealthConfig WithHealthAttributesScaling(AttributesScalingComponent scalingComponent)
        {
            var config = new MockAstraRpgHealthConfig
            {
                HealthAttributesScaling = scalingComponent
            };
            return config;
        }
    }
}
