using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgFramework.GameActions;
using ElectricDrill.AstraRpgFramework.Scaling.ScalingComponents;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraRpgHealth;
using ElectricDrill.AstraRpgHealth.Config;
using ElectricDrill.AstraRpgHealth.Damage;
using ElectricDrill.AstraRpgHealth.Damage.CalculationPipeline;
using ElectricDrill.AstraRpgHealth.Experience;
using ElectricDrill.AstraRpgHealth.Heal;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ElectricDrill.AstraRpgHealthTests.DamagePipeline
{
    /// <summary>
    /// Tests for True Damage mechanics: barriers and modifiers can be ignored based on DamageType configuration.
    /// </summary>
    public class TrueDamageTests
    {
        private class MockDamageType : DamageTypeSO
        {
            public static MockDamageType Create(
                string name = "MockType",
                bool ignoreBarrier = false,
                bool ignorePercentage = false,
                bool ignoreFlat = false)
            {
                var t = CreateInstance<MockDamageType>();
                t.name = name;
                t.IgnoresBarrier = ignoreBarrier;
                t.IgnoreGenericPercentageDamageModifiers = ignorePercentage;
                t.IgnoreGenericFlatDamageModifiers = ignoreFlat;
                return t;
            }
        }

        private class MockDamageSource : DamageSourceSO
        {
            public static MockDamageSource Create(string name = "MockSource")
            {
                var s = CreateInstance<MockDamageSource>();
                s.name = name;
                return s;
            }
        }

        private class MockConfig : IAstraRpgHealthConfig
        {
            public SerializableDictionary<HealSourceSO, Stat> HealSourceModifications { get; set; }
            public Stat GenericPercentageDamageModificationStat { get; set; }
            public Stat GenericFlatDamageModificationStat { get; set; }
            
            // Other required properties
            public AttributesScalingComponent HealthAttributesScaling { get; set; }
            public Stat GenericFlatHealAmountModifierStat { get; set; }
            public Stat GenericPercentageHealAmountModifierStat { get; set; }
            public DamageCalculationStrategySO DefaultDamageCalculationCalculationStrategy { get; set; }
            public HealSourceSO HealthRegenerationSource { get; set; }
            public Stat PassiveHealthRegenerationStat { get; set; }
            public float PassiveHealthRegenerationInterval { get; set; }
            public Stat ManualHealthRegenerationStat { get; set; }
            public bool SuppressPassiveRegenerationEvents { get; set; }
            public bool SuppressManualRegenerationEvents { get; set; }
            public LifestealConfigSO LifestealConfig { get; set; }
            public bool SuppressLifestealEvents { get; set; }
            public GameAction<Component> DefaultOnDeathGameAction { get; set; }
            public GameAction<Component> DefaultOnResurrectionGameAction { get; set; }
            public HealSourceSO DefaultResurrectionSource { get; set; }
            public ExpCollectionStrategySO DefaultExpCollectionStrategy { get; set; }
        }

        private class TestStats : EntityStats
        {
            public long genericPercentageModValue;
            public long genericFlatModValue;
            public Stat genericPercentageStat;
            public Stat genericFlatStat;

            public override long Get(Stat stat)
            {
                if (stat == genericPercentageStat) return genericPercentageModValue;
                if (stat == genericFlatStat) return genericFlatModValue;
                return 0;
            }
        }

        private GameObject _targetGo;
        private GameObject _dealerGo;
        private EntityCore _targetCore;
        private EntityCore _dealerCore;
        private EntityHealth _targetHealth;
        private TestStats _targetStats;

        [SetUp]
        public void Setup()
        {
            _targetGo = new GameObject("Target");
            _dealerGo = new GameObject("Dealer");

            _targetCore = _targetGo.AddComponent<EntityCore>();
            _dealerCore = _dealerGo.AddComponent<EntityCore>();

            _targetStats = _targetGo.AddComponent<TestStats>();
            _targetHealth = _targetGo.AddComponent<EntityHealth>();
            
            // Setup health component
            _targetHealth._entityCore = _targetCore;
            _targetHealth._entityStats = _targetStats;
            _targetHealth._baseMaxHp = new LongRef { UseConstant = true, ConstantValue = 100 };
            _targetHealth._totalMaxHp = new LongRef { UseConstant = true, ConstantValue = 100 };
            _targetHealth._hp = new LongRef { UseConstant = true, ConstantValue = 100 };
            _targetHealth._barrier = new LongRef { UseConstant = true, ConstantValue = 50 };
        }

        [TearDown]
        public void Cleanup()
        {
            AstraRpgHealthConfigProvider.Reset();
            Object.DestroyImmediate(_targetGo);
            Object.DestroyImmediate(_dealerGo);
        }

        private static Stat CreateStat(string name)
        {
            var stat = ScriptableObject.CreateInstance<Stat>();
            stat.name = name;
            return stat;
        }

        private DamageInfo MakeDamageInfo(long raw, DamageTypeSO type, DamageSourceSO source)
        {
            var pre = PreDamageContext.Builder
                .WithAmount(raw)
                .WithType(type)
                .WithSource(source)
                .WithTarget(_targetCore)
                .WithDealer(_dealerCore)
                .Build();
            return new DamageInfo(pre);
        }

        #region Barrier Tests

        [Test]
        public void ApplyBarrierStep_IgnoresBarrier_WhenDamageTypeIgnoresBarrier()
        {
            const long raw = 60;
            var type = MockDamageType.Create(ignoreBarrier: true);
            var source = MockDamageSource.Create();
            
            var info = MakeDamageInfo(raw, type, source);
            
            var step = new ApplyBarrierStep();
            step.Process(info);

            // Damage should pass through barrier unchanged
            Assert.AreEqual(60, info.Amounts.Current, "Damage should ignore barrier.");
            Assert.AreEqual(50, _targetHealth.Barrier, "Barrier should remain intact.");
        }

        [Test]
        public void ApplyBarrierStep_AppliesBarrier_WhenDamageTypeDoesNotIgnoreBarrier()
        {
            const long raw = 60;
            var type = MockDamageType.Create(ignoreBarrier: false);
            var source = MockDamageSource.Create();
            
            var info = MakeDamageInfo(raw, type, source);
            
            var step = new ApplyBarrierStep();
            step.Process(info);

            // Damage should be reduced by barrier
            Assert.AreEqual(10, info.Amounts.Current, "Damage should be reduced by barrier (60 - 50 = 10).");
            Assert.AreEqual(0, _targetHealth.Barrier, "Barrier should be consumed.");
        }

        #endregion

        #region Percentage Modifier Tests

        [Test]
        public void ApplyPercentageDmgModifiersStep_IgnoresGenericModifier_WhenDamageTypeIgnoresFlagIsSet()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericPercentageMod");
            _targetStats.genericPercentageStat = genericStat;
            _targetStats.genericPercentageModValue = -50; // -50% resistance

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create(ignorePercentage: true);
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            // Damage should be unchanged despite -50% modifier
            Assert.AreEqual(100, info.Amounts.Current, "Damage should ignore generic percentage modifier.");
        }

        [Test]
        public void ApplyPercentageDmgModifiersStep_AppliesGenericModifier_WhenDamageTypeDoesNotIgnore()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericPercentageMod");
            _targetStats.genericPercentageStat = genericStat;
            _targetStats.genericPercentageModValue = -50; // -50% resistance

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create(ignorePercentage: false);
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            // Damage should be reduced by 50%
            Assert.AreEqual(50, info.Amounts.Current, "Damage should be modified by -50%.");
        }

        [Test]
        public void ApplyPercentageDmgModifiersStep_DoesNotSetImmunity_WhenIgnoringGenericWith100Resistance()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericPercentageMod");
            _targetStats.genericPercentageStat = genericStat;
            _targetStats.genericPercentageModValue = -100; // -100% immunity

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create(ignorePercentage: true);
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            // Damage should pass through and NOT trigger immunity
            Assert.AreEqual(100, info.Amounts.Current, "Damage should ignore immunity.");
            Assert.IsFalse((info.Reasons & DamagePreventionReason.AllDamageImmune) != 0, "Should not set AllDamageImmune.");
        }

        #endregion

        #region Flat Modifier Tests

        [Test]
        public void ApplyFlatDmgModifiersStep_IgnoresGenericModifier_WhenDamageTypeIgnoresFlagIsSet()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericFlatMod");
            _targetStats.genericFlatStat = genericStat;
            _targetStats.genericFlatModValue = -30; // -30 flat reduction

            var config = new MockConfig
            {
                GenericFlatDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create(ignoreFlat: true);
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            // Damage should be unchanged despite -30 flat modifier
            Assert.AreEqual(100, info.Amounts.Current, "Damage should ignore generic flat modifier.");
        }

        [Test]
        public void ApplyFlatDmgModifiersStep_AppliesGenericModifier_WhenDamageTypeDoesNotIgnore()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericFlatMod");
            _targetStats.genericFlatStat = genericStat;
            _targetStats.genericFlatModValue = -30; // -30 flat reduction

            var config = new MockConfig
            {
                GenericFlatDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create(ignoreFlat: false);
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            // Damage should be reduced by 30
            Assert.AreEqual(70, info.Amounts.Current, "Damage should be reduced by 30 flat.");
        }

        #endregion

        #region Combined True Damage Tests

        [Test]
        public void TrueDamage_IgnoresAllModifications_WhenAllFlagsAreSet()
        {
            const long raw = 100;
            
            // Setup percentage modifier
            var percentageStat = CreateStat("PercentageMod");
            _targetStats.genericPercentageStat = percentageStat;
            _targetStats.genericPercentageModValue = -50; // -50%
            
            // Setup flat modifier
            var flatStat = CreateStat("FlatMod");
            _targetStats.genericFlatStat = flatStat;
            _targetStats.genericFlatModValue = -20; // -20 flat

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = percentageStat,
                GenericFlatDamageModificationStat = flatStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create(
                ignoreBarrier: true,
                ignorePercentage: true,
                ignoreFlat: true);
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source);

            // Apply all steps
            var barrierStep = new ApplyBarrierStep();
            var percentageStep = new ApplyPercentageDmgModifiersStep();
            var flatStep = new ApplyFlatDmgModifiersStep();

            barrierStep.Process(info);
            percentageStep.Process(info);
            flatStep.Process(info);

            // Damage should be 100 (unchanged)
            Assert.AreEqual(100, info.Amounts.Current, "True damage should ignore all modifications.");
            Assert.AreEqual(50, _targetHealth.Barrier, "Barrier should remain intact.");
        }

        [Test]
        public void TrueDamage_AppliesOnlyNonIgnoredModifications()
        {
            const long raw = 100;
            
            // Setup percentage modifier
            var percentageStat = CreateStat("PercentageMod");
            _targetStats.genericPercentageStat = percentageStat;
            _targetStats.genericPercentageModValue = -50; // -50%
            
            // Setup flat modifier
            var flatStat = CreateStat("FlatMod");
            _targetStats.genericFlatStat = flatStat;
            _targetStats.genericFlatModValue = -20; // -20 flat

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = percentageStat,
                GenericFlatDamageModificationStat = flatStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            // Ignore only barrier and percentage, but not flat
            var type = MockDamageType.Create(
                ignoreBarrier: true,
                ignorePercentage: true,
                ignoreFlat: false);
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source);

            // Apply all steps
            var barrierStep = new ApplyBarrierStep();
            var percentageStep = new ApplyPercentageDmgModifiersStep();
            var flatStep = new ApplyFlatDmgModifiersStep();

            barrierStep.Process(info);
            percentageStep.Process(info);
            flatStep.Process(info);

            // Damage should be 80 (100 - 20 flat, percentage ignored)
            Assert.AreEqual(80, info.Amounts.Current, "Damage should only apply flat modification.");
            Assert.AreEqual(50, _targetHealth.Barrier, "Barrier should remain intact.");
        }

        #endregion
    }
}
