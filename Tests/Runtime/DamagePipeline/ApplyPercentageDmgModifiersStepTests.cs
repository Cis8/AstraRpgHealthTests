using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgFramework.GameActions;
using ElectricDrill.AstraRpgFramework.Scaling.ScalingComponents;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraRpgHealth.Config;
using ElectricDrill.AstraRpgHealth.Damage;
using ElectricDrill.AstraRpgHealth.Damage.CalculationPipeline;
using ElectricDrill.AstraRpgHealth.Experience;
using ElectricDrill.AstraRpgHealth.Heal;
using NUnit.Framework;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests.DamagePipeline
{
    public class ApplyPercentageDmgModifiersStepTests
    {
        private class MockDamageType : DamageTypeSO
        {
            public static MockDamageType Create(string name = "MockType", Stat percentageStat = null)
            {
                var t = CreateInstance<MockDamageType>();
                t.name = name;
                t.PercentageDamageModificationStat = percentageStat;
                return t;
            }
        }

        private class MockDamageSource : DamageSourceSO
        {
            public static MockDamageSource Create(string name = "MockSource", Stat percentageStat = null)
            {
                var s = CreateInstance<MockDamageSource>();
                s.name = name;
                s.PercentageDamageModificationStat = percentageStat;
                return s;
            }
        }

        private class MockConfig : IAstraRpgHealthConfig
        {
            public SerializableDictionary<HealSourceSO, Stat> HealSourceModifications { get; set; }
            public Stat GenericPercentageDamageModificationStat { get; set; }
            public Stat GenericFlatDamageModificationStat { get; set; }
            
            // Other required properties (not used in these tests)
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

        // Concrete stats component to avoid null Stats and allow deterministic values
        private class TestStats : EntityStats
        {
            public long genericModValue;
            public long sourceModValue;
            public long typeModValue;
            public Stat genericStat;
            public Stat sourceStat;
            public Stat typeStat;

            public override long Get(Stat stat)
            {
                if (stat == genericStat) return genericModValue;
                if (stat == sourceStat) return sourceModValue;
                if (stat == typeStat) return typeModValue;
                return 0;
            }
        }

        private (EntityCore target, EntityCore dealer, TestStats targetStats, TestStats dealerStats) MakeEntities(
            long genericModValue = 0,
            long sourceModValue = 0,
            long typeModValue = 0,
            Stat genericStat = null,
            Stat sourceStat = null,
            Stat typeStat = null)
        {
            var targetGo = new GameObject("Target");
            var dealerGo = new GameObject("Dealer");

            var targetCore = targetGo.AddComponent<EntityCore>();
            var dealerCore = dealerGo.AddComponent<EntityCore>();

            var targetStats = targetGo.AddComponent<TestStats>();
            targetStats.genericModValue = genericModValue;
            targetStats.sourceModValue = sourceModValue;
            targetStats.typeModValue = typeModValue;
            targetStats.genericStat = genericStat;
            targetStats.sourceStat = sourceStat;
            targetStats.typeStat = typeStat;

            var dealerStats = dealerGo.AddComponent<TestStats>();

            return (targetCore, dealerCore, targetStats, dealerStats);
        }

        private DamageInfo MakeDamageInfo(long raw, DamageTypeSO type, DamageSourceSO source, EntityCore target, EntityCore dealer)
        {
            var pre = PreDamageContext.Builder
                .WithAmount(raw)
                .WithType(type)
                .WithSource(source)
                .WithTarget(target)
                .WithDealer(dealer)
                .Build();
            return new DamageInfo(pre);
        }

        [TearDown]
        public void Cleanup()
        {
            AstraRpgHealthConfigProvider.Reset();
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(go);
        }

        private static Stat CreateStat(string name)
        {
            var stat = ScriptableObject.CreateInstance<Stat>();
            stat.name = name;
            return stat;
        }

        [Test]
        public void ApplyDmgModifiersStep_SetsAllDamageImmuneReason_WhenGenericModifierIsNegative100()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericDmgMod");

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: -100,
                genericStat: genericStat);

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(0, info.Amounts.Current);
            Assert.IsTrue((info.Reasons & DamagePreventionReason.AllDamageImmune) != 0);
            Assert.AreEqual(typeof(ApplyPercentageDmgModifiersStep), info.TerminationStepType);
        }

        [Test]
        public void ApplyDmgModifiersStep_SetsAllDamageImmuneReason_WhenGenericModifierIsLessThanNegative100()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericDmgMod");

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: -150,
                genericStat: genericStat);

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(0, info.Amounts.Current);
            Assert.IsTrue((info.Reasons & DamagePreventionReason.AllDamageImmune) != 0);
            Assert.AreEqual(typeof(ApplyPercentageDmgModifiersStep), info.TerminationStepType);
        }

        [Test]
        public void ApplyDmgModifiersStep_DoesNotSetAllDamageImmuneReason_WhenGenericModifierIsGreaterThanNegative100()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericDmgMod");

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: -50,
                genericStat: genericStat);

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(50, info.Amounts.Current); // 100 - 50% = 50
            Assert.IsFalse((info.Reasons & DamagePreventionReason.AllDamageImmune) != 0);
            Assert.IsNull(info.TerminationStepType);
        }

        [Test]
        public void ApplyDmgModifiersStep_SetsDamageSourceImmuneReason_WhenSourceModifierIsNegative100()
        {
            const long raw = 100;
            var sourceModStat = CreateStat("SourceMod");

            var source = MockDamageSource.Create("TestSource", sourceModStat);
            
            var (target, dealer, _, _) = MakeEntities(
                sourceModValue: -100,
                sourceStat: sourceModStat);

            // No dictionary config needed
            var config = new MockConfig();
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(0, info.Amounts.Current);
            Assert.IsTrue((info.Reasons & DamagePreventionReason.DamageSourceImmune) != 0);
            Assert.AreEqual(typeof(ApplyPercentageDmgModifiersStep), info.TerminationStepType);
        }

        [Test]
        public void ApplyDmgModifiersStep_SetsDamageTypeImmuneReason_WhenTypeModifierIsNegative100()
        {
            const long raw = 100;
            var typeModStat = CreateStat("TypeMod");

            var type = MockDamageType.Create("TestType", typeModStat);
            
            var (target, dealer, _, _) = MakeEntities(
                typeModValue: -100,
                typeStat: typeModStat);

            // No dictionary config needed
            var config = new MockConfig();
            AstraRpgHealthConfigProvider.Instance = config;

            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(0, info.Amounts.Current);
            Assert.IsTrue((info.Reasons & DamagePreventionReason.DamageTypeImmune) != 0);
            Assert.AreEqual(typeof(ApplyPercentageDmgModifiersStep), info.TerminationStepType);
        }

        [Test]
        public void ApplyDmgModifiersStep_PrioritizesGenericImmunity_OverSourceAndTypeModifiers()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericDmgMod");
            var sourceStat = CreateStat("SourceMod");
            var typeStat = CreateStat("TypeMod");

            var type = MockDamageType.Create("TestType", typeStat);
            var source = MockDamageSource.Create("TestSource", sourceStat);

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: -100, // Generic immunity
                sourceModValue: 50, // Would increase damage
                typeModValue: 50, // Would increase damage
                genericStat: genericStat,
                sourceStat: sourceStat, // Changed: Explicitly passed sourceStat
                typeStat: typeStat);


            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = genericStat,
                // No dictionaries
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(0, info.Amounts.Current);
            // Should set AllDamageImmune, NOT DamageSourceImmune or DamageTypeImmune
            Assert.IsTrue((info.Reasons & DamagePreventionReason.AllDamageImmune) != 0);
            Assert.IsFalse((info.Reasons & DamagePreventionReason.DamageSourceImmune) != 0);
            Assert.IsFalse((info.Reasons & DamagePreventionReason.DamageTypeImmune) != 0);
        }

        [Test]
        public void ApplyDmgModifiersStep_AppliesCumulativeModifiers_WhenNoneReachImmunity()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericDmgMod");
            var sourceStat = CreateStat("SourceDmgMod");
            var typeStat = CreateStat("TypeDmgMod");

            var type = MockDamageType.Create("TestType", typeStat);
            var source = MockDamageSource.Create("TestSource", sourceStat);

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: -20, // -20%
                sourceModValue: -30, // -30%
                typeModValue: 10, // +10%
                genericStat: genericStat,
                sourceStat: sourceStat,
                typeStat: typeStat);

            var config = new MockConfig
            {
                GenericPercentageDamageModificationStat = genericStat,
                // No dictionaries
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            // Net: -20 -30 +10 = -40%
            // 100 * -0.40 = -40
            // 100 + (-40) = 60
            Assert.AreEqual(60, info.Amounts.Current);
            Assert.AreEqual(DamagePreventionReason.None, info.Reasons);
            Assert.IsNull(info.TerminationStepType);
        }

        [Test]
        public void ApplyDmgModifiersStep_DoesNotModify_WhenNoConfigProvided()
        {
            const long raw = 100;
            
            var (target, dealer, _, _) = MakeEntities();
            
            // No config set
            AstraRpgHealthConfigProvider.Instance = null;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyPercentageDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(raw, info.Amounts.Current);
            Assert.AreEqual(DamagePreventionReason.None, info.Reasons);
        }
    }
}

