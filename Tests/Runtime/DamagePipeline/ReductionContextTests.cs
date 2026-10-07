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
    /// Tests of <see cref="ReductionContext"/>: what it derives from entities and from plain levels, that it reads
    /// nothing until a function asks, and that a reduction function can read other stats of the entities through it
    /// (consistently with the ownership attribution the pipeline uses for the piercing stat).
    /// </summary>
    public class ReductionContextTests
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

        /// <summary>An <see cref="EntityLevel"/> that reports a fixed level and counts how often it is read.</summary>
        private class CountingEntityLevel : EntityLevel
        {
            private readonly int _level;
            public int Reads;
            public CountingEntityLevel(int level) => _level = level;
            public override int Level { get { Reads++; return _level; } set { } }
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

        /// <summary>Keeps the context it receives and leaves the damage unchanged.</summary>
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

        /// <summary>Leaves the defense unchanged and ignores the context.</summary>
        private class IgnoringPenetrationFn : DefensePenetrationFnSO
        {
            public override double CalculatePiercedDefense(long piercingStatValue, long defensiveStatValue, StatSO defensiveStat,
                bool applyClamp = true) => defensiveStatValue;
        }

        /// <summary>
        /// A function that depends on another stat of the entities, which a context with only levels could not
        /// express. It records what it read and leaves the damage unchanged.
        /// </summary>
        private class StatProbeMitigationFn : DamageMitigationFnSO
        {
            public StatSO Stat;
            public long TargetValue = -1;
            public long PerformerValue = -1;

            public override long CalculateMitigatedDamage(long amount, double defensiveStatValue, RoundingMode roundingMode)
                => amount;

            public override long CalculateMitigatedDamage(long amount, double defensiveStatValue, RoundingMode roundingMode,
                in ReductionContext context)
            {
                TargetValue = Read(context.TargetStats);
                PerformerValue = Read(context.PerformerStats);
                return amount;
            }

            // A missing reader or a missing stat reads as 0.
            private long Read(IStatReader reader) => reader != null && reader.TryGet(Stat, out var value) ? value : 0;
        }

        private EntityCore MakeEntity(string name, EntityLevel level, StatSO stat = null, long statValue = 0)
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
            core.Level = level;
            return core;
        }

        private EntityCore MakeEntity(string name, int level, StatSO stat = null, long statValue = 0)
            => MakeEntity(name, new CountingEntityLevel(level), stat, statValue);

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

        private static MockAstraHealthConfig Attribution(EntityAttribution attribution)
            => new() { DamageStatsAttribution = attribution };

        // ── The context itself ────────────────────────────────────────────────────

        [Test]
        public void Default_HasNoEntities_TargetLevelOne_AndNoPerformer()
        {
            foreach (var context in new ReductionContext[] { ReductionContext.Default, default })
            {
                Assert.IsNull(context.Target);
                Assert.IsNull(context.Performer);
                Assert.IsNull(context.TargetStats);
                Assert.IsNull(context.PerformerStats);
                Assert.AreEqual(1, context.TargetLevel);
                Assert.AreEqual(0, context.PerformerLevel);
                Assert.IsFalse(context.HasPerformer);
            }
        }

        [TestCase(7, 60, 7, 60, true)]
        [TestCase(0, -3, 1, 0, false)]
        [TestCase(-5, 0, 1, 0, false)]
        [TestCase(3, 1, 3, 1, true)]
        public void LevelsOnlyConstructor_NormalisesTheLevels_AndHasNoStatReaders(
            int targetIn, int performerIn, int targetOut, int performerOut, bool hasPerformer)
        {
            var context = new ReductionContext(targetIn, performerIn);

            Assert.AreEqual(targetOut, context.TargetLevel);
            Assert.AreEqual(performerOut, context.PerformerLevel);
            Assert.AreEqual(hasPerformer, context.HasPerformer);
            Assert.IsNull(context.Target);
            Assert.IsNull(context.TargetStats);
            Assert.IsNull(context.PerformerStats);
        }

        [Test]
        public void EntityBacked_DerivesLevelsAndStatReadersFromTheEntities()
        {
            var target = MakeEntity("Target", 10);
            var performer = MakeEntity("Performer", 60);

            var context = new ReductionContext(target, performer);

            Assert.AreSame(target, context.Target);
            Assert.AreSame(performer, context.Performer);
            Assert.AreEqual(10, context.TargetLevel);
            Assert.AreEqual(60, context.PerformerLevel);
            Assert.IsTrue(context.HasPerformer);
            Assert.AreSame(target, context.TargetStats);
            Assert.AreSame(performer, context.PerformerStats, "Without a given reader the performer is the reader.");
        }

        [Test]
        public void EntityBacked_WithoutPerformer_HasNoPerformer()
        {
            var context = new ReductionContext(MakeEntity("Target", 10), null);

            Assert.IsFalse(context.HasPerformer);
            Assert.AreEqual(0, context.PerformerLevel);
            Assert.IsNull(context.PerformerStats);
        }

        [Test]
        public void ExplicitLevels_TakePrecedenceOverTheEntitiesLevels()
        {
            var target = MakeEntity("Target", 10);
            var performer = MakeEntity("Performer", 60);

            var context = new ReductionContext(target, performer, targetLevel: 5, performerLevel: 7);

            Assert.AreEqual(5, context.TargetLevel);
            Assert.AreEqual(7, context.PerformerLevel);
        }

        [Test]
        public void MixedSides_OneEntityAndOneExplicitLevel()
        {
            // The graph window does this: an assigned defender, and an attacker given only by a level field.
            var target = MakeEntity("Target", 10);

            var context = new ReductionContext(target, null, performerLevel: 40);

            Assert.AreEqual(10, context.TargetLevel);
            Assert.AreEqual(40, context.PerformerLevel);
            Assert.IsTrue(context.HasPerformer);
            Assert.IsNull(context.Performer);
            Assert.IsNull(context.PerformerStats);
        }

        [Test]
        public void PerformerStats_IsTheGivenReader_WhenOneIsProvided()
        {
            var performer = MakeEntity("Weapon", 1);
            var owner = MakeEntity("Hero", 50);
            var chain = new ChainedStatReader(performer, owner);

            var context = new ReductionContext(MakeEntity("Target", 1), owner, chain);

            Assert.AreSame(chain, context.PerformerStats);
        }

        // ── Nothing is read until a function asks ─────────────────────────────────

        // Level reads made by one hit. EntityStats computes a stat's value from the entity's level, so reading the
        // defensive and the piercing stat already reads both levels once: these tests compare against a baseline hit
        // whose functions ignore the context, to isolate what the context itself reads.
        private (int target, int attacker) LevelReadsOfOneHit(DamageMitigationFnSO mitigation, DefensePenetrationFnSO penetration)
        {
            var defStat = Make<StatSO>();
            var pierceStat = Make<StatSO>();
            var targetLevel = new CountingEntityLevel(10);
            var attackerLevel = new CountingEntityLevel(60);
            var target = MakeEntity("Target", targetLevel, defStat, 100);
            var attacker = MakeEntity("Attacker", attackerLevel, pierceStat, 50);
            targetLevel.Reads = 0;
            attackerLevel.Reads = 0; // whatever the setup read does not count

            var dmgType = TestDamageType.Create(defStat, mitigation, pierceStat, penetration);
            new ApplyDefenseStep().Process(MakeInfo(1000, dmgType, target, attacker));

            return (targetLevel.Reads, attackerLevel.Reads);
        }

        [Test]
        public void ApplyDefenseStep_BaselineHit_ReadsEachLevelOnlyThroughTheStats()
        {
            var (target, attacker) = LevelReadsOfOneHit(Make<SpyMitigationFn>(), Make<IgnoringPenetrationFn>());

            // Reading the target's defense and the attacker's piercing computes their stat values from the level.
            // This is the premise of the two tests below: they check that nothing is added to these reads.
            Assert.Greater(target, 0);
            Assert.Greater(attacker, 0);
        }

        [Test]
        public void ApplyDefenseStep_AddsNoLevelRead_WithFunctionsThatIgnoreTheContext()
        {
            var baseline = LevelReadsOfOneHit(Make<SpyMitigationFn>(), Make<IgnoringPenetrationFn>());

            var families = new (DamageMitigationFnSO, DefensePenetrationFnSO)[]
            {
                (Make<FlatDamageMitigationFnSO>(), Make<FlatDefensePenetrationFnSO>()),
                (Make<PercentageDamageMitigationFnSO>(), Make<PercentageDefensePenetrationFnSO>()),
                (Make<LogDamageMitigationFnSO>(), Make<LogDefensePenetrationFnSO>()),
            };
            foreach (var (mitigation, penetration) in families)
                Assert.AreEqual(baseline, LevelReadsOfOneHit(mitigation, penetration),
                    $"{mitigation.GetType().Name} must not make the context read a level.");
        }

        [TestCase(true, TestName = "Performer level source reads only the attacker's level")]
        [TestCase(false, TestName = "Target level source reads only the target's level")]
        public void ApplyDefenseStep_AddsOnlyTheLevelAHyperbolicFunctionNeeds(bool performerSource)
        {
            var baseline = LevelReadsOfOneHit(Make<SpyMitigationFn>(), Make<IgnoringPenetrationFn>());
            var hyperbolic = Make<HyperbolicDamageMitigationFnSO>();
            hyperbolic.Configure(400, 85, performerSource ? ReductionLevelSource.Performer : ReductionLevelSource.Target);

            var reads = LevelReadsOfOneHit(hyperbolic, Make<IgnoringPenetrationFn>());

            if (performerSource)
            {
                Assert.Greater(reads.attacker, baseline.attacker);
                Assert.AreEqual(baseline.target, reads.target);
            }
            else
            {
                Assert.Greater(reads.target, baseline.target);
                Assert.AreEqual(baseline.attacker, reads.attacker);
            }
        }

        // ── What the pipeline puts in it ──────────────────────────────────────────

        [Test]
        public void ApplyDefenseStep_UnderRootAttribution_TheContextsPerformerIsTheOwner()
        {
            var defStat = Make<StatSO>();
            var spy = Make<SpyMitigationFn>();
            var target = MakeEntity("Target", 10, defStat, 100);
            var weapon = MakeEntity("Weapon", 1);
            var hero = MakeEntity("Hero", 50);
            weapon.Owner = hero;

            new ApplyDefenseStep().Process(MakeInfo(100, TestDamageType.Create(defStat, spy), target, weapon,
                Attribution(EntityAttribution.Root)));

            Assert.AreSame(target, spy.Seen.Value.Target);
            Assert.AreSame(hero, spy.Seen.Value.Performer);
            Assert.AreEqual(50, spy.Seen.Value.PerformerLevel);
        }

        [Test]
        public void ApplyDefenseStep_UnderDirectAttribution_TheContextsPerformerIsThePerformer()
        {
            var defStat = Make<StatSO>();
            var spy = Make<SpyMitigationFn>();
            var target = MakeEntity("Target", 10, defStat, 100);
            var weapon = MakeEntity("Weapon", 1);
            weapon.Owner = MakeEntity("Hero", 50);

            new ApplyDefenseStep().Process(MakeInfo(100, TestDamageType.Create(defStat, spy), target, weapon,
                Attribution(EntityAttribution.Direct)));

            Assert.AreSame(weapon, spy.Seen.Value.Performer);
            Assert.AreEqual(1, spy.Seen.Value.PerformerLevel);
        }

        [Test]
        public void ApplyDefenseStep_WithoutPerformer_TheContextHasNoPerformer()
        {
            var defStat = Make<StatSO>();
            var spy = Make<SpyMitigationFn>();
            var target = MakeEntity("Target", 10, defStat, 100);

            new ApplyDefenseStep().Process(MakeInfo(100, TestDamageType.Create(defStat, spy), target));

            Assert.IsFalse(spy.Seen.Value.HasPerformer);
            Assert.IsNull(spy.Seen.Value.PerformerStats);
        }

        // ── A function that depends on another stat ───────────────────────────────

        private StatProbeMitigationFn ProbeFor(StatSO affinity)
        {
            var probe = Make<StatProbeMitigationFn>();
            probe.Stat = affinity;
            return probe;
        }

        [Test]
        public void StatFunction_ReadsTheTargetsStatFromTheTarget_AndTheAttackersFromTheAttacker()
        {
            var defStat = Make<StatSO>();
            var affinity = Make<StatSO>();
            var probe = ProbeFor(affinity);
            var target = MakeEntity("Target", 1, affinity, 40);
            var attacker = MakeEntity("Attacker", 1, affinity, 15);

            new ApplyDefenseStep().Process(MakeInfo(100, TestDamageType.Create(defStat, probe), target, attacker));

            Assert.AreEqual(40L, probe.TargetValue);
            Assert.AreEqual(15L, probe.PerformerValue);
        }

        [Test]
        public void StatFunction_UnderRootAttribution_ReadsTheOwnersStat_WhenTheWeaponLacksIt()
        {
            var defStat = Make<StatSO>();
            var affinity = Make<StatSO>();
            var probe = ProbeFor(affinity);
            var target = MakeEntity("Target", 1);
            var weapon = MakeEntity("Weapon", 1); // defines no affinity
            weapon.Owner = MakeEntity("Hero", 50, affinity, 25);

            new ApplyDefenseStep().Process(MakeInfo(100, TestDamageType.Create(defStat, probe), target, weapon,
                Attribution(EntityAttribution.Root)));

            Assert.AreEqual(25L, probe.PerformerValue, "The same fallback chain that feeds the piercing stat.");
        }

        [Test]
        public void StatFunction_UnderRootAttribution_PrefersTheWeaponsOwnStat()
        {
            var defStat = Make<StatSO>();
            var affinity = Make<StatSO>();
            var probe = ProbeFor(affinity);
            var target = MakeEntity("Target", 1);
            var weapon = MakeEntity("Weapon", 1, affinity, 5);
            weapon.Owner = MakeEntity("Hero", 50, affinity, 25);

            new ApplyDefenseStep().Process(MakeInfo(100, TestDamageType.Create(defStat, probe), target, weapon,
                Attribution(EntityAttribution.Root)));

            Assert.AreEqual(5L, probe.PerformerValue);
        }

        [Test]
        public void StatFunction_ReadsMissingStatsAsZero_AndDoesNotThrowWithoutPerformer()
        {
            var defStat = Make<StatSO>();
            var affinity = Make<StatSO>();
            var probe = ProbeFor(affinity);
            var target = MakeEntity("Target", 1); // has no affinity

            Assert.DoesNotThrow(() =>
                new ApplyDefenseStep().Process(MakeInfo(100, TestDamageType.Create(defStat, probe), target)));

            Assert.AreEqual(0L, probe.TargetValue);
            Assert.AreEqual(0L, probe.PerformerValue);
        }

        [Test]
        public void StatFunction_GivenOnlyLevels_ReadsEverythingAsZero()
        {
            var probe = ProbeFor(Make<StatSO>());

            probe.CalculateMitigatedDamage(100, 10, RoundingMode.Round, new ReductionContext(10, 60));

            Assert.AreEqual(0L, probe.TargetValue);
            Assert.AreEqual(0L, probe.PerformerValue);
        }
    }
}
