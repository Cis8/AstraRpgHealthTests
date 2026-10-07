using System.Collections.Generic;
using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgFramework.Config;
using ElectricDrill.AstraRpgFramework.Experience;
using ElectricDrill.AstraRpgFramework.Ownership;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraHealth.Config;
using ElectricDrill.AstraHealth.Damage;
using ElectricDrill.AstraHealth.Damage.CalculationPipeline;
using ElectricDrill.AstraHealth.DamageMitigationFunctions;
using ElectricDrill.AstraHealth.DefensePenetrationFunctions;
using ElectricDrill.AstraHealth.ReductionFunctions;
using ElectricDrill.AstraRpgHealthTests.TestUtils;
using NUnit.Framework;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests.DamagePipeline
{
    /// <summary>
    /// Tests of <see cref="HyperbolicDamageMitigationFnSO"/> and <see cref="HyperbolicDefensePenetrationFnSO"/>
    /// (mitigation % = X / (X + K)), of the <see cref="ReductionContext"/> that carries levels to them, and of
    /// the pipeline plumbing that fills it.
    /// </summary>
    public class HyperbolicReductionFnTests
    {
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp() => AstraFrameworkConfigProvider.Instance = MockAstraFrameworkConfig.CreateMinimal();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                Object.DestroyImmediate(go);
            foreach (var o in _created)
                if (o) Object.DestroyImmediate(o);
            _created.Clear();
            AstraFrameworkConfigProvider.Reset();
        }

        // ── Fixtures ──────────────────────────────────────────────────────────────

        private T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _created.Add(o);
            return o;
        }

        private HyperbolicDamageMitigationFnSO Mitigation(
            double k, double perLevel = 0d, ReductionLevelSource source = ReductionLevelSource.Performer)
        {
            var fn = Make<HyperbolicDamageMitigationFnSO>();
            fn.Configure(k, perLevel, source);
            return fn;
        }

        private HyperbolicDefensePenetrationFnSO Penetration(
            double k, double perLevel = 0d, ReductionLevelSource source = ReductionLevelSource.Performer)
        {
            var fn = Make<HyperbolicDefensePenetrationFnSO>();
            fn.Configure(k, perLevel, source);
            return fn;
        }

        /// <summary>An <see cref="EntityLevel"/> that reports a fixed level, with no experience machinery.</summary>
        private class FixedEntityLevel : EntityLevel
        {
            private readonly int _level;
            public FixedEntityLevel(int level) => _level = level;
            public override int Level { get => _level; set { } }
        }

        private class TestDamageType : DamageTypeSO
        {
            public static TestDamageType Create(
                StatSO def = null, DamageMitigationFnSO dmgFn = null, StatSO pierce = null, DefensePenetrationFnSO penFn = null)
            {
                var t = CreateInstance<TestDamageType>();
                t.DefensiveStat = def;
                t.DamageMitigationFn = dmgFn;
                t.DefensiveStatPiercedBy = pierce;
                t.DefensePenetrationFn = penFn;
                return t;
            }
        }

        private class TestDamageSource : DamageSourceSO
        {
            public static TestDamageSource Create() => CreateInstance<TestDamageSource>();
        }

        /// <summary>Records the context it receives and leaves the damage unchanged.</summary>
        private class SpyMitigationFn : DamageMitigationFnSO
        {
            public ReductionContext? Seen;
            public override long CalculateMitigatedDamage(long amount, double defensiveStatValue, RoundingMode roundingMode)
                => amount;
            public override long CalculateMitigatedDamage(long amount, double defensiveStatValue, RoundingMode roundingMode,
                in ReductionContext context)
            {
                Seen = context;
                return amount;
            }
        }

        /// <summary>A function written before <see cref="ReductionContext"/> existed: it only knows the abstract overload.</summary>
        private class LegacyHalvingMitigationFn : DamageMitigationFnSO
        {
            public override long CalculateMitigatedDamage(long amount, double defensiveStatValue, RoundingMode roundingMode)
                => amount / 2;
        }

        private EntityCore MakeEntity(string name, int level, StatSO stat = null, long statValue = 0)
        {
            var go = new GameObject(name);
            var core = go.AddComponent<EntityCore>();
            var stats = go.AddComponent<EntityStats>();
            var statSet = Make<StatSetSO>();
            if (stat != null)
                statSet._stats.Add(stat);
            stats.SetFixedStatSet(statSet);
            if (stat != null)
                stats.SetFixed(stat, statValue);
            core._stats = stats;
            core.Level = new FixedEntityLevel(level);
            return core;
        }

        private DamageInfo MakeInfo(long amount, DamageTypeSO type, EntityCore target, EntityCore performer = null,
            IAstraHealthConfig config = null)
        {
            var builder = PreDamageContext.Builder
                .WithAmount(amount)
                .WithType(type)
                .WithSource(TestDamageSource.Create())
                .WithTarget(target);
            if (performer != null)
                builder.WithPerformer(performer);
            return new DamageInfo(builder.Build(), config);
        }

        // ── Mitigation: the formula ───────────────────────────────────────────────

        [TestCase(100d, 100d, 1000L, 500L, TestName = "X equal to K halves the damage")]
        [TestCase(100d, 50d, 300L, 200L, TestName = "K=100, X=50: a third is mitigated")]
        [TestCase(100d, 300d, 400L, 100L, TestName = "K=100, X=300: three quarters are mitigated")]
        [TestCase(400d, 100d, 1000L, 800L, TestName = "K=400, X=100: a fifth is mitigated")]
        public void Mitigation_FollowsXOverXPlusK(double k, double x, long amount, long expected)
        {
            var fn = Mitigation(k);

            Assert.AreEqual(expected, fn.CalculateMitigatedDamage(amount, x, RoundingMode.Round));
        }

        [TestCase(0d)]
        [TestCase(-50d)]
        public void Mitigation_ZeroOrNegativeDefense_LeavesDamageUnchanged(double x)
        {
            var fn = Mitigation(100);

            Assert.AreEqual(777L, fn.CalculateMitigatedDamage(777, x, RoundingMode.Floor));
        }

        [TestCase(RoundingMode.Round, 67L)]
        [TestCase(RoundingMode.Floor, 66L)]
        [TestCase(RoundingMode.Ceil, 67L)]
        public void Mitigation_RoundsTheFractionalResultWithTheGivenMode(RoundingMode mode, long expected)
        {
            // 100 × 100 / (50 + 100) = 66.67
            var fn = Mitigation(100);

            Assert.AreEqual(expected, fn.CalculateMitigatedDamage(100, 50, mode));
        }

        [Test]
        public void Mitigation_ZeroK_MitigatesAllDamage()
        {
            var fn = Mitigation(0);

            Assert.AreEqual(0L, fn.CalculateMitigatedDamage(500, 1, RoundingMode.Round));
        }

        [Test]
        public void Mitigation_NegativeConfiguredK_IsTreatedAsZero()
        {
            var fn = Mitigation(-50);

            Assert.AreEqual(0L, fn.CalculateMitigatedDamage(500, 10, RoundingMode.Round));
        }

        [Test]
        public void Mitigation_HugeValues_StayWithinZeroAndAmount()
        {
            var fn = Mitigation(100);
            const long amount = long.MaxValue / 2;

            var result = fn.CalculateMitigatedDamage(amount, 1e15, RoundingMode.Round);

            Assert.That(result, Is.InRange(0L, amount));
        }

        // ── Mitigation: K depends on level ────────────────────────────────────────

        [TestCase(1, 485L, TestName = "WoW-like K at level 1 is 485")]
        [TestCase(60, 5500L, TestName = "WoW-like K at level 60 is 5500")]
        public void Mitigation_PerLevel_GivesHalfMitigationWhenXEqualsK(int level, long k)
        {
            var fn = Mitigation(400, 85);

            var result = fn.CalculateMitigatedDamage(1000, k, RoundingMode.Round, new ReductionContext(1, level));

            Assert.AreEqual(500L, result);
        }

        [Test]
        public void Mitigation_PerformerSource_UsesTheAttackersLevel()
        {
            // K = 100 × 50 = 5000, X = 1000: 600 × 5000 / 6000 = 500
            var fn = Mitigation(0, 100, ReductionLevelSource.Performer);

            var result = fn.CalculateMitigatedDamage(600, 1000, RoundingMode.Round, new ReductionContext(targetLevel: 10, performerLevel: 50));

            Assert.AreEqual(500L, result);
        }

        [Test]
        public void Mitigation_TargetSource_UsesTheTargetsLevel()
        {
            // K = 100 × 10 = 1000, X = 1000: half
            var fn = Mitigation(0, 100, ReductionLevelSource.Target);

            var result = fn.CalculateMitigatedDamage(600, 1000, RoundingMode.Round, new ReductionContext(targetLevel: 10, performerLevel: 50));

            Assert.AreEqual(300L, result);
        }

        [Test]
        public void Mitigation_PerformerSource_WithoutPerformer_FallsBackToTheTargetsLevel()
        {
            // K = 100 × 10 = 1000, X = 1000: half
            var fn = Mitigation(0, 100, ReductionLevelSource.Performer);

            var result = fn.CalculateMitigatedDamage(600, 1000, RoundingMode.Round, new ReductionContext(targetLevel: 10, performerLevel: 0));

            Assert.AreEqual(300L, result);
        }

        [Test]
        public void Mitigation_ContextFreeOverload_EvaluatesKAtLevelOne()
        {
            var fn = Mitigation(400, 85); // K at level 1 = 485

            Assert.AreEqual(500L, fn.CalculateMitigatedDamage(1000, 485, RoundingMode.Round));
        }

        // ── Penetration ───────────────────────────────────────────────────────────

        [Test]
        public void Penetration_PiercingEqualToK_HalvesTheDefense()
        {
            var fn = Penetration(100);

            var result = fn.CalculatePiercedDefense(100, 80, Make<StatSO>(), applyClamp: false);

            Assert.AreEqual(40d, result, 1e-9);
        }

        [TestCase(0L)]
        [TestCase(-20L)]
        public void Penetration_ZeroOrNegativePiercing_LeavesTheDefenseUnchanged(long piercing)
        {
            var fn = Penetration(100);

            Assert.AreEqual(80d, fn.CalculatePiercedDefense(piercing, 80, Make<StatSO>()), 1e-9);
        }

        [Test]
        public void Penetration_PerLevel_UsesTheAttackersLevel()
        {
            // K = 400 + 85 × 60 = 5500, P = 5500: half of 200
            var fn = Penetration(400, 85);

            var result = fn.CalculatePiercedDefense(5500, 200, Make<StatSO>(), new ReductionContext(targetLevel: 1, performerLevel: 60), applyClamp: false);

            Assert.AreEqual(100d, result, 1e-9);
        }

        [Test]
        public void Penetration_ClampsToTheStatBoundsOnlyWhenAsked()
        {
            var stat = Make<StatSO>();
            stat.HasMinValue = true;
            stat.MinValue = 50;
            var fn = Penetration(100); // P = 100 pierces 80 down to 40

            Assert.AreEqual(50d, fn.CalculatePiercedDefense(100, 80, stat, applyClamp: true), 1e-9);
            Assert.AreEqual(40d, fn.CalculatePiercedDefense(100, 80, stat, applyClamp: false), 1e-9);
        }

        [Test]
        public void Penetration_WithoutStat_DoesNotThrowWhenClampIsRequested()
        {
            var fn = Penetration(100);

            Assert.DoesNotThrow(() => fn.CalculatePiercedDefense(100, 80, null, applyClamp: true));
        }

        [Test]
        public void LevelScaledConstant_Sanitize_RaisesNegativeValuesToZero()
        {
            var k = new LevelScaledConstant(-5d, -3d, ReductionLevelSource.Performer);

            k.Sanitize();

            Assert.AreEqual(0d, k.BaseValue);
            Assert.AreEqual(0d, k.PerLevel);
        }

        // ── Calculator ────────────────────────────────────────────────────────────

        [Test]
        public void Calculator_WithoutContext_EvaluatesLevelDependentFunctionsAtLevelOne()
        {
            var fn = Mitigation(400, 85); // K at level 1 = 485

            var result = DamageMitigationCalculator.CalculateReducedDmg(
                1000, 0, null, 485, null, fn, HealthRoundingSettings.Default);

            Assert.AreEqual(500L, result);
        }

        [Test]
        public void Calculator_WithContext_EvaluatesLevelDependentFunctionsAtThoseLevels()
        {
            var fn = Mitigation(400, 85); // K at level 60 = 5500

            var result = DamageMitigationCalculator.CalculateReducedDmg(
                1000, 0, null, 5500, null, fn, HealthRoundingSettings.Default, new ReductionContext(1, 60));

            Assert.AreEqual(500L, result);
        }

        // ── ApplyDefenseStep: pipeline plumbing ───────────────────────────────────

        [Test]
        public void ApplyDefenseStep_WithBothHyperbolicFunctions_PiercesThenMitigates()
        {
            var defStat = Make<StatSO>();
            var pierceStat = Make<StatSO>();
            var target = MakeEntity("Target", 1, defStat, 200);
            var attacker = MakeEntity("Attacker", 1, pierceStat, 100);
            // Defense 200 pierced by 100 (K 100) leaves 100; then mitigation 100 / (100 + 100) = 50%.
            var dmgType = TestDamageType.Create(defStat, Mitigation(100), pierceStat, Penetration(100));

            var processed = new ApplyDefenseStep().Process(MakeInfo(1000, dmgType, target, attacker));

            Assert.AreEqual(500L, processed.Amounts.Current);
        }

        [Test]
        public void ApplyDefenseStep_PassesBothEntitiesLevelsToTheFunction()
        {
            var defStat = Make<StatSO>();
            var spy = Make<SpyMitigationFn>();
            var target = MakeEntity("Target", 10, defStat, 100);
            var attacker = MakeEntity("Attacker", 60);
            var dmgType = TestDamageType.Create(defStat, spy);

            new ApplyDefenseStep().Process(MakeInfo(100, dmgType, target, attacker));

            Assert.IsTrue(spy.Seen.HasValue, "The pipeline must call the context overload.");
            Assert.AreEqual(10, spy.Seen.Value.TargetLevel);
            Assert.AreEqual(60, spy.Seen.Value.PerformerLevel);
        }

        [Test]
        public void ApplyDefenseStep_WithoutPerformer_PassesNoPerformerLevel()
        {
            var defStat = Make<StatSO>();
            var spy = Make<SpyMitigationFn>();
            var target = MakeEntity("Target", 10, defStat, 100);
            var dmgType = TestDamageType.Create(defStat, spy);

            new ApplyDefenseStep().Process(MakeInfo(100, dmgType, target));

            Assert.IsFalse(spy.Seen.Value.HasPerformer);
            Assert.AreEqual(10, spy.Seen.Value.TargetLevel);
        }

        [Test]
        public void ApplyDefenseStep_PerformerSource_AttackerLevelDrivesK()
        {
            var defStat = Make<StatSO>();
            // K = 400 + 85 × 60 = 5500 = X: half of the damage
            var dmgType = TestDamageType.Create(defStat, Mitigation(400, 85));
            var target = MakeEntity("Target", 10, defStat, 5500);
            var attacker = MakeEntity("Attacker", 60);

            var processed = new ApplyDefenseStep().Process(MakeInfo(1000, dmgType, target, attacker));

            Assert.AreEqual(500L, processed.Amounts.Current);
        }

        [Test]
        public void ApplyDefenseStep_TargetSource_DefenderLevelDrivesK()
        {
            var defStat = Make<StatSO>();
            // K = 400 + 85 × 10 = 1250, X = 5500: 1000 × 1250 / 6750 = 185.19
            var dmgType = TestDamageType.Create(defStat, Mitigation(400, 85, ReductionLevelSource.Target));
            var target = MakeEntity("Target", 10, defStat, 5500);
            var attacker = MakeEntity("Attacker", 60);

            var processed = new ApplyDefenseStep().Process(MakeInfo(1000, dmgType, target, attacker));

            Assert.AreEqual(185L, processed.Amounts.Current);
        }

        [Test]
        public void ApplyDefenseStep_UnderRootAttribution_UsesTheOwnersLevelAsTheAttackersLevel()
        {
            var defStat = Make<StatSO>();
            // A level 1 weapon owned by a level 50 hero attacks as level 50: K = 400 + 85 × 50 = 4650 = X.
            var dmgType = TestDamageType.Create(defStat, Mitigation(400, 85));
            var target = MakeEntity("Target", 10, defStat, 4650);
            var weapon = MakeEntity("Weapon", 1);
            weapon.Owner = MakeEntity("Hero", 50);
            var config = new MockAstraHealthConfig { DamageStatsAttribution = EntityAttribution.Root };

            var processed = new ApplyDefenseStep().Process(MakeInfo(1000, dmgType, target, weapon, config));

            Assert.AreEqual(500L, processed.Amounts.Current);
        }

        [Test]
        public void ApplyDefenseStep_UnderDirectAttribution_UsesThePerformersOwnLevel()
        {
            var defStat = Make<StatSO>();
            // The same weapon under Direct attribution: K = 400 + 85 × 1 = 485, X = 4650: 1000 × 485 / 5135 = 94.45
            var dmgType = TestDamageType.Create(defStat, Mitigation(400, 85));
            var target = MakeEntity("Target", 10, defStat, 4650);
            var weapon = MakeEntity("Weapon", 1);
            weapon.Owner = MakeEntity("Hero", 50);
            var config = new MockAstraHealthConfig { DamageStatsAttribution = EntityAttribution.Direct };

            var processed = new ApplyDefenseStep().Process(MakeInfo(1000, dmgType, target, weapon, config));

            Assert.AreEqual(94L, processed.Amounts.Current);
        }

        [Test]
        public void ApplyDefenseStep_FunctionWrittenWithoutContext_KeepsWorking()
        {
            var defStat = Make<StatSO>();
            var dmgType = TestDamageType.Create(defStat, Make<LegacyHalvingMitigationFn>());
            var target = MakeEntity("Target", 1, defStat, 100);
            var attacker = MakeEntity("Attacker", 1);

            var processed = new ApplyDefenseStep().Process(MakeInfo(1000, dmgType, target, attacker));

            Assert.AreEqual(500L, processed.Amounts.Current);
        }
    }
}
