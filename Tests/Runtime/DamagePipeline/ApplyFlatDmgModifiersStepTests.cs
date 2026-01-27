using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgFramework.Scaling.ScalingComponents;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraRpgFramework.Utils.Executables;
using ElectricDrill.AstraRpgHealth.Config;
using ElectricDrill.AstraRpgHealth.Damage;
using ElectricDrill.AstraRpgHealth.Damage.CalculationPipeline;
using ElectricDrill.AstraRpgHealth.Heal;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ElectricDrill.AstraRpgHealthTests.DamagePipeline
{
    public class ApplyFlatDmgModifiersStepTests
    {
        private class MockDamageType : DamageType
        {
            public static MockDamageType Create(string name = "MockType")
            {
                var t = CreateInstance<MockDamageType>();
                t.name = name;
                return t;
            }
        }

        private class MockDamageSource : DamageSource
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
            public SerializableDictionary<HealSource, Stat> HealSourceModifications { get; set; }
            public Stat GenericPercentageDamageModificationStat { get; set; }
            public Stat GenericFlatDamageModificationStat { get; set; }
            public SerializableDictionary<DamageType, Stat> DamageTypePercentageModifications { get; set; }
            public SerializableDictionary<DamageType, Stat> DamageTypeFlatModifications { get; set; }
            public SerializableDictionary<DamageSource, Stat> DamageSourcePercentageModifications { get; set; }
            public SerializableDictionary<DamageSource, Stat> DamageSourceFlatModifications { get; set; }
            
            // Other required properties (not used in these tests)
            public AttributesScalingComponent HealthAttributesScaling { get; set; }
            public Stat GenericHealAmountModifierStat { get; set; }
            public DamageCalculationStrategy DefaultDamageCalculationCalculationStrategy { get; set; }
            public HealSource PassiveHealthRegenerationSource { get; set; }
            public Stat PassiveHealthRegenerationStat { get; set; }
            public float PassiveHealthRegenerationInterval { get; set; }
            public Stat ManualHealthRegenerationStat { get; set; }
            public LifestealConfig LifestealConfig { get; set; }
            public GameAction<UnityEngine.Component> DefaultOnDeathGameAction { get; set; }
            public GameAction<UnityEngine.Component> DefaultOnResurrectionGameAction { get; set; }
            public HealSource DefaultResurrectionSource { get; set; }
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

        private DamageInfo MakeDamageInfo(long raw, DamageType type, DamageSource source, EntityCore target, EntityCore dealer)
        {
            var pre = PreDamageInfo.Builder
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
        public void ApplyFlatDmgModifiersStep_AppliesGenericFlatReduction()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericFlatMod");

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: -20, // -20 flat damage
                genericStat: genericStat);

            var config = new MockConfig
            {
                GenericFlatDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(80, info.Amounts.Current); // 100 - 20 = 80
        }

        [Test]
        public void ApplyFlatDmgModifiersStep_AppliesGenericFlatIncrement()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericFlatMod");

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: 15, // +15 flat damage
                genericStat: genericStat);

            var config = new MockConfig
            {
                GenericFlatDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(115, info.Amounts.Current); // 100 + 15 = 115
        }

        [Test]
        public void ApplyFlatDmgModifiersStep_AppliesSourceFlatModification()
        {
            const long raw = 100;
            var sourceModStat = CreateStat("SourceFlatMod");

            var source = MockDamageSource.Create("TestSource");
            
            var (target, dealer, _, _) = MakeEntities(
                sourceModValue: -30, // -30 flat
                sourceStat: sourceModStat);

            var config = new MockConfig
            {
                DamageSourceFlatModifications = new SerializableDictionary<DamageSource, Stat>
                {
                    { source, sourceModStat }
                }
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(70, info.Amounts.Current); // 100 - 30 = 70
        }

        [Test]
        public void ApplyFlatDmgModifiersStep_AppliesTypeFlatModification()
        {
            const long raw = 100;
            var typeModStat = CreateStat("TypeFlatMod");

            var type = MockDamageType.Create("TestType");
            
            var (target, dealer, _, _) = MakeEntities(
                typeModValue: 25, // +25 flat
                typeStat: typeModStat);

            var config = new MockConfig
            {
                DamageTypeFlatModifications = new SerializableDictionary<DamageType, Stat>
                {
                    { type, typeModStat }
                }
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(125, info.Amounts.Current); // 100 + 25 = 125
        }

        [Test]
        public void ApplyFlatDmgModifiersStep_AppliesCumulativeFlatModifications()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericFlatMod");
            var sourceStat = CreateStat("SourceFlatMod");
            var typeStat = CreateStat("TypeFlatMod");

            var type = MockDamageType.Create("TestType");
            var source = MockDamageSource.Create("TestSource");

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: -10, // -10 flat
                sourceModValue: -5,   // -5 flat
                typeModValue: 20,     // +20 flat
                genericStat: genericStat,
                sourceStat: sourceStat,
                typeStat: typeStat);

            var config = new MockConfig
            {
                GenericFlatDamageModificationStat = genericStat,
                DamageSourceFlatModifications = new SerializableDictionary<DamageSource, Stat>
                {
                    { source, sourceStat }
                },
                DamageTypeFlatModifications = new SerializableDictionary<DamageType, Stat>
                {
                    { type, typeStat }
                }
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            // Net: -10 -5 +20 = +5
            // 100 + 5 = 105
            Assert.AreEqual(105, info.Amounts.Current);
        }

        [Test]
        public void ApplyFlatDmgModifiersStep_ClampsDamageToZero_WhenNegativeResult()
        {
            const long raw = 50;
            var genericStat = CreateStat("GenericFlatMod");

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: -100, // -100 flat would result in negative
                genericStat: genericStat);

            var config = new MockConfig
            {
                GenericFlatDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            // 50 - 100 = -50, but should be clamped to 0
            Assert.AreEqual(0, info.Amounts.Current);
        }

        [Test]
        public void ApplyFlatDmgModifiersStep_DoesNotModify_WhenNoConfigProvided()
        {
            const long raw = 100;
            
            var (target, dealer, _, _) = MakeEntities();
            
            // No config set
            AstraRpgHealthConfigProvider.Instance = null;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(raw, info.Amounts.Current);
        }

        [Test]
        public void ApplyFlatDmgModifiersStep_HandlesZeroModifications()
        {
            const long raw = 100;
            var genericStat = CreateStat("GenericFlatMod");

            var (target, dealer, _, _) = MakeEntities(
                genericModValue: 0, // No modification
                genericStat: genericStat);

            var config = new MockConfig
            {
                GenericFlatDamageModificationStat = genericStat
            };
            AstraRpgHealthConfigProvider.Instance = config;

            var type = MockDamageType.Create();
            var source = MockDamageSource.Create();
            var info = MakeDamageInfo(raw, type, source, target, dealer);

            var step = new ApplyFlatDmgModifiersStep();
            step.Process(info);

            Assert.AreEqual(100, info.Amounts.Current); // No change
        }
    }
}
