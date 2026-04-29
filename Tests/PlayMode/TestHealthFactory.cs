using System;
using System.Reflection;
using System.Threading;
using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgFramework.Attributes;
using ElectricDrill.AstraRpgFramework.Config;
using ElectricDrill.AstraRpgFramework.Contexts;
using ElectricDrill.AstraRpgFramework.Events;
using ElectricDrill.AstraRpgFramework.Experience;
using ElectricDrill.AstraRpgFramework.GameActions;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraHealth;
using ElectricDrill.AstraHealth.Config;
using ElectricDrill.AstraHealth.Core;
using ElectricDrill.AstraHealth.Damage;
using ElectricDrill.AstraHealth.Damage.CalculationPipeline;
using ElectricDrill.AstraHealth.Events;
using ElectricDrill.AstraHealth.Heal;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests.Tests.PlayMode
{
    public class mockAstraFrameworkConfig : IAstraFrameworkConfig
    {
        public mockAstraFrameworkConfig(
            EntityCoreGameEvent spawnedEvent = null,
            EntityLevelUpGameEvent levelUpEvent = null,
            EntityLevelDownGameEvent levelDownEvent = null,
            StatChangedGameEvent statChangedEvent = null,
            AttributeChangedGameEvent attributeChangedEvent = null)
        {
            GlobalEntitySpawnedEvent = spawnedEvent ? spawnedEvent : ScriptableObject.CreateInstance<EntityCoreGameEvent>();
            GlobalEntityLevelUpEvent = levelUpEvent ? levelUpEvent : ScriptableObject.CreateInstance<EntityLevelUpGameEvent>();
            GlobalEntityLevelDownEvent = levelDownEvent ? levelDownEvent : ScriptableObject.CreateInstance<EntityLevelDownGameEvent>();
            GlobalStatChangedEvent = statChangedEvent ? statChangedEvent : ScriptableObject.CreateInstance<StatChangedGameEvent>();
            GlobalAttributeChangedEvent = attributeChangedEvent ? attributeChangedEvent : ScriptableObject.CreateInstance<AttributeChangedGameEvent>();
        }

        public EntityCoreGameEvent GlobalEntitySpawnedEvent { get; set; }
        public EntityLevelUpGameEvent GlobalEntityLevelUpEvent { get; set; }
        public EntityLevelDownGameEvent GlobalEntityLevelDownEvent { get; set; }
        public StatChangedGameEvent GlobalStatChangedEvent { get; set; }
        public AttributeChangedGameEvent GlobalAttributeChangedEvent { get; set; }

        public static mockAstraFrameworkConfig CreateMinimal() => new();
    }

    /// <summary>
    /// Utility factory methods to spawn fully configured EntityHealth objects for play mode tests.
    /// Avoid duplicating reflection setup logic across test classes.
    /// </summary>
    public static class TestHealthFactory
    {
        public struct HealthEventsBundle
        {
            public PreDamageGameEvent PreDmg;
            public DamageResolutionGameEvent DamageResolution;
            public EntityMaxHealthChangedGameEvent MaxHpChanged;
            public EntityHealthChangedGameEvent Changed;
            public EntityDiedGameEvent Died;
            public PreHealGameEvent PreHeal;
            public EntityHealedGameEvent Healed;
            public EntityResurrectedGameEvent Resurrected;
        }

        public static HealthEventsBundle CreateSharedEvents()
        {
            return new HealthEventsBundle
            {
                PreDmg = ScriptableObject.CreateInstance<PreDamageGameEvent>(),
                DamageResolution = ScriptableObject.CreateInstance<DamageResolutionGameEvent>(),
                MaxHpChanged = ScriptableObject.CreateInstance<EntityMaxHealthChangedGameEvent>(),
                Changed = ScriptableObject.CreateInstance<EntityHealthChangedGameEvent>(),
                Died = ScriptableObject.CreateInstance<EntityDiedGameEvent>(),
                PreHeal = ScriptableObject.CreateInstance<PreHealGameEvent>(),
                Healed = ScriptableObject.CreateInstance<EntityHealedGameEvent>(),
                Resurrected = ScriptableObject.CreateInstance<EntityResurrectedGameEvent>()
            };
        }

        public struct HealthEntityBundle
        {
            public GameObject Go;
            public EntityCore Core;
            public EntityStats Stats;
            public EntityAttributes Attributes;
            public EntityHealth Health;
            public AstraRpgHealthConfigSO Config;
            public mockAstraFrameworkConfig FrameworkConfig;
            public DamageTypeSO DefaultDamageType;
            public DamageSourceSO DefaultDamageSource;
            public HealthEventsBundle Events; // events actually used (shared or per-entity)
        }

        public static HealthEntityBundle CreateEntity(string name = "Entity",
            AstraRpgHealthConfigSO sharedConfig = null,
            long maxHp = 100,
            bool allowNegative = false,
            long barrierAmount = 0,
            Action<AstraRpgHealthConfigSO> configMutator = null,
            Action<EntityHealth> healthMutator = null,
            bool initializeStats = false,
            bool initializeAttributes = false,
            HealthEventsBundle? sharedEvents = null)
        {
            // Always create a brand new config if one is not explicitly shared.
            // This avoids reusing a previously left-over provider instance with stale damage / lifesteal mappings.
            var config = sharedConfig;
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<AstraRpgHealthConfigSO>();
                // Ensure a default OnDeathGameAction on the config
                var cfgDeath = ScriptableObject.CreateInstance<TestOnDeathStrategy>();
                config.DefaultOnDeathGameAction = cfgDeath;
                configMutator?.Invoke(config);
                SetConfigProviderInstance(config);
            }
            else
            {
                if (config.DefaultOnDeathGameAction == null)
                {
                    var cfgDeath = ScriptableObject.CreateInstance<TestOnDeathStrategy>();
                    config.DefaultOnDeathGameAction = cfgDeath;
                }
                SetConfigProviderInstance(config);
            }

            // Ensure default damage calculation strategy exists (prevents "No Damage Calculation Strategy" errors)
            void EnsureDefaultStrategy()
            {
                if (config.DefaultDamageCalculationCalculationStrategy != null) return;

                var strat = ScriptableObject.CreateInstance<DamageCalculationStrategySO>();
                strat.name = "Auto_DefaultDamageCalculationStrategy";
                strat.steps.Add(new ApplyCriticalMultiplierStep());
                strat.steps.Add(new ApplyBarrierStep());
                strat.steps.Add(new ApplyDefenseStep());
                strat.steps.Add(new ApplyPercentageDmgModifiersStep());

                config.DefaultDamageCalculationCalculationStrategy = strat;
            }
            EnsureDefaultStrategy();

            var events = sharedEvents ?? CreateSharedEvents();
            var evtBundle = events;
            var frameworkConfig = sharedConfig != null && AstraFrameworkConfigProvider.Instance is mockAstraFrameworkConfig existingFrameworkConfig
                ? existingFrameworkConfig
                : mockAstraFrameworkConfig.CreateMinimal();
            AstraFrameworkConfigProvider.Instance = frameworkConfig;

            // Assign global events to config (must happen before go.SetActive(true) triggers Awake/ValidateConstraints)
            config.GlobalPreDamageInfoEvent = evtBundle.PreDmg;
            config.GlobalDamageResolutionEvent = evtBundle.DamageResolution;
            config.GlobalEntityDiedEvent = evtBundle.Died;
            config.GlobalMaxHealthChangedEvent = evtBundle.MaxHpChanged;
            config.GlobalHealthChangedEvent = evtBundle.Changed;
            config.GlobalPreHealEvent = evtBundle.PreHeal;
            config.GlobalEntityHealedEvent = evtBundle.Healed;
            config.GlobalEntityResurrectedEvent = evtBundle.Resurrected;

            // GameObject inactive so Awake sees injected fields
            var go = new GameObject(name);
            go.SetActive(false);

            // Core (no reflection needed for Level property: internal setter)
            var core = go.AddComponent<EntityCore>();
            core.Level = new EntityLevel();

            // Stats
            var stats = go.AddComponent<EntityStats>();
            stats.UseClassBaseStats = false;
            if (!initializeStats)
                stats.enabled = false;

            // Ensure a fixed StatSet exists (internal field accessible)
            if (stats._fixedBaseStatsStatSet == null)
            {
                stats._fixedBaseStatsStatSet = ScriptableObject.CreateInstance<StatSetSO>();
                // Initialize internal fixed base stats structures
                stats.InitializeFixedBaseStats();
            }
            
            // Attributes
            var attributes = go.AddComponent<EntityAttributes>();
            if (!initializeAttributes)
                attributes.enabled = false;
            
            // Ensure a fixed AttributeSet exists (internal field accessible)
            if (attributes._fixedBaseAttributeSet == null)
            {
                attributes._fixedBaseAttributeSet = ScriptableObject.CreateInstance<AttributeSetSO>();
                // Initialize internal fixed base attributes structures
                attributes.InitializeFixedBaseAttributes();
            }
            
            var health = go.AddComponent<EntityHealth>();

            // Refs
            var baseMax = new LongRef { Value = maxHp };
            var totalMax = new LongRef { Value = maxHp };
            var hp = new LongRef { Value = maxHp };
            var barrier = new LongRef { Value = barrierAmount };
            var deathThreshold = ScriptableObject.CreateInstance<LongVarSO>();
            deathThreshold.Value = allowNegative ? -9999 : 0;

            // OnDeathStrategy override (use public property, not reflection)
            var onDeathStrategy = ScriptableObject.CreateInstance<TestOnDeathStrategy>();
            health.OverrideOnDeathGameAction = onDeathStrategy;

            // Internal (accessible directly)
            health._baseMaxHp = baseMax;
            health._totalMaxHp = totalMax;
            health._hp = hp;
            health._barrier = barrier;
            health._deathThreshold = deathThreshold;
            health.HealthCanBeNegative = allowNegative;

            healthMutator?.Invoke(health);

            go.SetActive(true);

            // Dmg type & source
            var dmgType = ScriptableObject.CreateInstance<DamageTypeSO>();
            dmgType.name = $"{name}_DmgType";
            var dmgSource = ScriptableObject.CreateInstance<DamageSourceSO>();
            dmgSource.name = $"{name}_DmgSource";

            return new HealthEntityBundle
            {
                Go = go,
                Core = core,
                Stats = stats,
                Attributes = attributes, // added: expose created EntityAttributes
                Health = health,
                Config = config,
                FrameworkConfig = frameworkConfig,
                DefaultDamageType = dmgType,
                DefaultDamageSource = dmgSource,
                Events = evtBundle
            };
        }

        private static void SetConfigProviderInstance(AstraRpgHealthConfigSO config)
        {
            AstraRpgHealthConfigProvider.Instance = config;
        }

        public static PreDamageContext BuildPre(long amount, HealthEntityBundle dealer, HealthEntityBundle target,
            DamageTypeSO type = null, DamageSourceSO source = null, bool crit = false, double critMult = 1d, bool ignore = false)
        {
            var dmgType = type ?? dealer.DefaultDamageType;
            var dmgSource = source ?? dealer.DefaultDamageSource;

            var pre = PreDamageContext.Builder
                .WithAmount(amount)
                .WithType(dmgType)
                .WithSource(dmgSource)
                .WithTarget(target.Core)
                .WithPerformer(dealer.Core)
                .WithIsCritical(crit)
                .WithCriticalMultiplier(critMult)
                .Build();
            pre.Ignore = ignore;
            return pre;
        }

        // Simple OnDeathStrategy used in tests
        private class TestOnDeathStrategy : GameAction<IHasEntity>
        {
            public override Awaitable ExecuteAsync(IHasEntity health, CancellationToken cancellationToken = default) 
            { 
                /* no-op for tests */ 
                return Awaitable.NextFrameAsync(cancellationToken); 
            }
        }

        /// <summary>
        /// Inject a Percentage stat value into an EntityStats without using reflection.
        /// Adds the stat to the fixed StatSet if missing and sets its fixed base value.
        /// </summary>
        public static void InjectPercentageStat(EntityStats stats, StatSO stat, Percentage value)
        {
            if (stats == null || stat == null) return;

            // Ensure fixed stat set exists
            if (stats._fixedBaseStatsStatSet == null)
            {
                stats._fixedBaseStatsStatSet = ScriptableObject.CreateInstance<StatSetSO>();
                stats.InitializeFixedBaseStats();
            }

            // Add stat to StatSet if not already present
            if (!stats.StatSet.Contains(stat))
            {
                if (!stats._fixedBaseStatsStatSet._stats.Contains(stat))
                    stats._fixedBaseStatsStatSet._stats.Add(stat);

                // Re-initialize fixed base stats so internal dictionary reflects new stat
                stats.InitializeFixedBaseStats();
            }

            // Set base value (Percentage stored as long internally; (long)value extracts underlying)
            stats.SetFixed(stat, (long)value);

            // Invalidate cache so Get() recomputes with new stat
            stats.StatsCache.Invalidate(stat);
        }

        /// <summary>
        /// Inject a flat (raw long) stat value into an EntityStats (non-percentage defensive or similar).
        /// </summary>
        public static void InjectFlatStat(EntityStats stats, StatSO stat, long value)
        {
            if (stats == null || stat == null) return;

            if (stats._fixedBaseStatsStatSet == null)
            {
                stats._fixedBaseStatsStatSet = ScriptableObject.CreateInstance<StatSetSO>();
                stats.InitializeFixedBaseStats();
            }

            if (!stats.StatSet.Contains(stat))
            {
                if (!stats._fixedBaseStatsStatSet._stats.Contains(stat))
                    stats._fixedBaseStatsStatSet._stats.Add(stat);
                stats.InitializeFixedBaseStats();
            }

            stats.SetFixed(stat, value);
            stats.StatsCache.Invalidate(stat);
        }
        
        /// <summary>
        /// Inject a flat (raw long) attribute value into an EntityAttributes.
        /// Ensures the attribute exists in the fixed base attribute set and sets its fixed base value.
        /// </summary>
        public static void InjectFlatAttribute(EntityAttributes attributes, AttributeSO attribute, long value)
        {
            if (attributes == null || attribute == null) return;
            
            if (attributes._fixedBaseAttributeSet == null)
            {
                attributes._fixedBaseAttributeSet = ScriptableObject.CreateInstance<AttributeSetSO>();
                attributes.InitializeFixedBaseAttributes();
            }

            if (!attributes.AttributeSet.Contains(attribute))
            {
                if (!attributes._fixedBaseAttributeSet._attributes.Contains(attribute))
                    attributes._fixedBaseAttributeSet._attributes.Add(attribute);
                attributes.InitializeFixedBaseAttributes();
            }

            // Set base value (EntityAttributes API analogous to EntityStats)
            attributes.SetFixed(attribute, value);

            // Invalidate cache if present
            attributes.AttributesCache.Invalidate(attribute);
        }

        /// <summary>
        /// Configures type-specific lifesteal directly on the DamageTypeSO.
        /// </summary>
        internal static void AssignLifestealMapping(AstraRpgHealthConfigSO config, DamageTypeSO damageType, StatSO lifestealStat, HealSourceSO lifestealSource)
        {
            damageType.Lifesteal.Configure(lifestealStat, lifestealSource);
        }

        public static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            obj.GetType()
               .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
               ?.SetValue(obj, value);
        }

        /// <summary>
        /// Creates a DamageCalculationStrategy with steps ordered:
        /// Critical -> Barrier -> Defense -> Weakness/Resistances (ApplyDmgModifiers).
        /// No reflection: we rely on the concrete step classes directly.
        /// </summary>
        public static DamageCalculationStrategySO CreateCritBarrierDefenseWeaknessStrategy()
        {
            var strat = ScriptableObject.CreateInstance<DamageCalculationStrategySO>();
            strat.steps.Add(new ApplyCriticalMultiplierStep());
            strat.steps.Add(new ApplyBarrierStep());
            strat.steps.Add(new ApplyDefenseStep());
            strat.steps.Add(new ApplyPercentageDmgModifiersStep());
            return strat;
        }

        /// <summary>
        /// Configures lifesteal so that its basis is the damage amount recorded AFTER the Critical step (Post).
        /// Uses Step mode (no reflection).
        /// </summary>
        internal static void ConfigureLifestealBasisAfterCritical(
            DamageTypeSO damageType,
            StatSO lifestealStat,
            HealSourceSO lifestealSource)
        {
            if (!damageType) throw new ArgumentNullException(nameof(damageType));
            if (!lifestealStat) throw new ArgumentNullException(nameof(lifestealStat));
            if (!lifestealSource) throw new ArgumentNullException(nameof(lifestealSource));

            var selector = new LifestealAmountSelector(
                LifestealBasisMode.Step,
                typeof(ApplyCriticalMultiplierStep).AssemblyQualifiedName,
                StepValuePoint.Post);

            damageType.Lifesteal.Configure(lifestealStat, lifestealSource, selector);
        }
    }
}
