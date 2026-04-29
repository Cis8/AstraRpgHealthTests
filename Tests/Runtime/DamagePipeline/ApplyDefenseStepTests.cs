using System.Linq;
using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraHealth.Damage;
using ElectricDrill.AstraHealth.Damage.CalculationPipeline;
using ElectricDrill.AstraHealth.DamageMitigationFunctions;
using ElectricDrill.AstraHealth.DefensePenetrationFunctions;
using NUnit.Framework;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests.DamagePipeline
{
    public class ApplyDefenseStepTests
    {
        private class MockFlatDamageMitigationFn : FlatDamageMitigationFnSO
        {
            private long _result;
            public void Set(long r) => _result = r;
            public override long CalculateMitigatedDamage(long amount, double defensiveStatValue,
                RoundingMode roundingMode) => _result;
        }

        private class MockFlatDefensePenetrationFn : FlatDefensePenetrationFnSO
        {
            private long _result;
            public void Set(long r) => _result = r;
            public override double CalculatePiercedDefense(long piercingStatValue, long defensiveStatValue, StatSO defensiveStat, bool clampDef = true) => _result;
        }

        private class MockDamageType : DamageTypeSO
        {
            public static MockDamageType Create(StatSO def = null, DamageMitigationFnSO damageFn = null, StatSO pierce = null, DefensePenetrationFnSO defenseFn = null) {
                var t = CreateInstance<MockDamageType>();
                t.DefensiveStat = def;
                t.DamageMitigationFn = damageFn;
                t.DefensiveStatPiercedBy = pierce;
                t.DefensePenetrationFn = defenseFn;
                return t;
            }
        }

        private class MockDamageSource : DamageSourceSO
        {
            public static MockDamageSource Create() {
                var s = CreateInstance<MockDamageSource>();
                s.name = "TestSource";
                return s;
            }
        }

        // Concrete stats component to avoid null Stats and allow deterministic values
        private class TestStats : EntityStats
        {
            public long defensiveValue;
            public long piercingValue;
            public StatSO defensiveStat;
            public StatSO piercingStat;

            public override long Get(StatSO stat)
            {
                if (stat == defensiveStat) return defensiveValue;
                if (stat == piercingStat) return piercingValue;
                return 0;
            }
        }

        private DamageInfo MakeDamageInfo(long raw, DamageTypeSO type, EntityCore target, EntityCore dealer)
        {
            // Build the required PreDamageContext first (new DamageInfo ctor requirement)
            var pre = PreDamageContext.Builder
                .WithAmount(raw)
                .WithType(type)
                .WithSource(MockDamageSource.Create())
                .WithTarget(target)
                .WithPerformer(dealer)
                .Build();
            
            return new DamageInfo(pre);
        }

        private (EntityCore target, EntityCore dealer, TestStats targetStats, TestStats dealerStats) MakeEntities(
            long defensiveValue = 0,
            long piercingValue = 0,
            StatSO defensiveStat = null,
            StatSO piercingStat = null)
        {
            var targetGo = new GameObject("Target");
            var dealerGo = new GameObject("Dealer");

            var targetCore = targetGo.AddComponent<EntityCore>();
            var dealerCore = dealerGo.AddComponent<EntityCore>();

            var targetStats = targetGo.AddComponent<TestStats>();
            targetStats.defensiveValue = defensiveValue;
            targetStats.defensiveStat = defensiveStat;

            var dealerStats = dealerGo.AddComponent<TestStats>();
            dealerStats.piercingValue = piercingValue;
            dealerStats.piercingStat = piercingStat;

            // Ensure EntityCore.Stats returns these instances (assign internal field if accessible)
            targetCore._stats = targetStats;
            dealerCore._stats = dealerStats;

            return (targetCore, dealerCore, targetStats, dealerStats);
        }

        [TearDown]
        public void CleanupScene() {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyDefenseStep_ReducesDamage_WithDefensiveStat()
        {
            const long RAW = 100;
            const long DEF_VAL = 30;
            const long EXPECTED = 70;

            var defStat = ScriptableObject.CreateInstance<StatSO>();
            var dmgFn = ScriptableObject.CreateInstance<MockFlatDamageMitigationFn>();
            dmgFn.Set(EXPECTED);

            var (target, dealer, _, _) = MakeEntities(defensiveValue: DEF_VAL, defensiveStat: defStat);

            var dmgType = MockDamageType.Create(def: defStat, damageFn: dmgFn);
            var info = MakeDamageInfo(RAW, dmgType, target, dealer);

            var step = new ApplyDefenseStep();

            var processed = step.Process(info);

            Assert.AreEqual(EXPECTED, processed.Amounts.Current);
            var rec1 = processed.Amounts.Records.Last();
            Assert.AreEqual(RAW, rec1.Pre);
            Assert.AreEqual(EXPECTED, rec1.Post);
        }

        [Test]
        public void ApplyDefenseStep_ReducesDamage_WithPiercing()
        {
            const long RAW = 120;
            const long DEF_VAL = 40;
            const long PIERCING_VAL = 10;
            const long REDUCED_DEF = 30; // after piercing
            const long EXPECTED = 90; // mocked final dmg

            var defStat = ScriptableObject.CreateInstance<StatSO>();
            var pierceStat = ScriptableObject.CreateInstance<StatSO>();

            var defFn = ScriptableObject.CreateInstance<MockFlatDefensePenetrationFn>();
            defFn.Set(REDUCED_DEF);

            var dmgFn = ScriptableObject.CreateInstance<MockFlatDamageMitigationFn>();
            dmgFn.Set(EXPECTED);

            var (target, dealer, _, _) = MakeEntities(
                defensiveValue: DEF_VAL,
                piercingValue: PIERCING_VAL,
                defensiveStat: defStat,
                piercingStat: pierceStat);

            var dmgType = MockDamageType.Create(def: defStat, damageFn: dmgFn, pierce: pierceStat, defenseFn: defFn);
            var info = MakeDamageInfo(RAW, dmgType, target, dealer);

            var step = new ApplyDefenseStep();
            var processed = step.Process(info);

            Assert.AreEqual(EXPECTED, processed.Amounts.Current);
            var rec2 = processed.Amounts.Records.Last();
            Assert.AreEqual(RAW, rec2.Pre);
            Assert.AreEqual(EXPECTED, rec2.Post);
        }

        [Test]
        public void ApplyDefenseStep_SetsDefenseAbsorbedReason_WhenDefenseFullyAbsorbsDamage()
        {
            const long RAW = 50;
            const long EXPECTED = 0; // fully absorbed

            var defStat = ScriptableObject.CreateInstance<StatSO>();
            var dmgFn = ScriptableObject.CreateInstance<MockFlatDamageMitigationFn>();
            dmgFn.Set(EXPECTED);

            var (target, dealer, _, _) = MakeEntities(defensiveValue: 100, defensiveStat: defStat);

            var dmgType = MockDamageType.Create(def: defStat, damageFn: dmgFn);
            var info = MakeDamageInfo(RAW, dmgType, target, dealer);

            var step = new ApplyDefenseStep();
            step.Process(info);

            Assert.AreEqual(0, info.Amounts.Current);
            Assert.IsTrue((info.Reasons & DamagePreventionReason.DefenseAbsorbed) != 0);
            Assert.AreEqual(typeof(ApplyDefenseStep), info.TerminationStepType);
        }

        [Test]
        public void ApplyDefenseStep_DoesNotSetDefenseAbsorbedReason_WhenDefensePartiallyReducesDamage()
        {
            const long RAW = 100;
            const long EXPECTED = 40; // partially reduced

            var defStat = ScriptableObject.CreateInstance<StatSO>();
            var dmgFn = ScriptableObject.CreateInstance<MockFlatDamageMitigationFn>();
            dmgFn.Set(EXPECTED);

            var (target, dealer, _, _) = MakeEntities(defensiveValue: 50, defensiveStat: defStat);

            var dmgType = MockDamageType.Create(def: defStat, damageFn: dmgFn);
            var info = MakeDamageInfo(RAW, dmgType, target, dealer);

            var step = new ApplyDefenseStep();
            step.Process(info);

            Assert.AreEqual(EXPECTED, info.Amounts.Current);
            Assert.IsFalse((info.Reasons & DamagePreventionReason.DefenseAbsorbed) != 0);
            Assert.IsNull(info.TerminationStepType);
        }

        [Test]
        public void ApplyDefenseStep_DoesNotSetDefenseAbsorbedReason_WhenNoDamageFunctionConfigured()
        {
            const long RAW = 80;

            var defStat = ScriptableObject.CreateInstance<StatSO>();
            var (target, dealer, _, _) = MakeEntities(defensiveValue: 200, defensiveStat: defStat);

            // No damage reduction function configured
            var dmgType = MockDamageType.Create(def: defStat, damageFn: null);
            var info = MakeDamageInfo(RAW, dmgType, target, dealer);

            var step = new ApplyDefenseStep();
            step.Process(info);

            // Damage should remain unchanged
            Assert.AreEqual(RAW, info.Amounts.Current);
            Assert.IsFalse((info.Reasons & DamagePreventionReason.DefenseAbsorbed) != 0);
        }
    }
}
