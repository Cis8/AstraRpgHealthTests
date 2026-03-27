using System;
using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgFramework.Experience;
using ElectricDrill.AstraRpgFramework.GameActions.Actions.Component;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraRpgHealth;
using ElectricDrill.AstraRpgHealth.Config;
using ElectricDrill.AstraRpgHealth.Damage;
using ElectricDrill.AstraRpgHealth.Damage.CalculationPipeline;
using ElectricDrill.AstraRpgHealth.Events;
using ElectricDrill.AstraRpgHealth.Exceptions;
using ElectricDrill.AstraRpgHealth.Heal;
using ElectricDrill.AstraRpgHealth.Resurrection;
using Moq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ElectricDrill.AstraRpgHealthTests.Tests.Runtime
{
    public class EntityHealthDeadStateTests
    {
        private const long MaxHp = 100;

        private class MockDamageSource : DamageSourceSO
        {
            public static MockDamageSource Create()
            {
                var s = CreateInstance<MockDamageSource>();
                s.name = "TestDmgSource";
                return s;
            }
        }
        
        private class MockHealSource : HealSourceSO
        {
            public static MockHealSource Create()
            {
                var s = CreateInstance<MockHealSource>();
                s.name = "TestHealSource";
                return s;
            }
        }

        private class MockDamageType : DamageTypeSO
        {
            public static MockDamageType Create()
            {
                var t = CreateInstance<MockDamageType>();
                return t;
            }
        }

        private class TestDamageCalculationStrategy : DamageCalculationStrategySO
        {
            private Func<DamageInfo, DamageInfo> _fn;
            public static TestDamageCalculationStrategy Create(Func<DamageInfo, DamageInfo> fn)
            {
                var inst = CreateInstance<TestDamageCalculationStrategy>();
                inst._fn = fn;
                return inst;
            }
            public override DamageInfo CalculateDamage(DamageInfo data) => _fn?.Invoke(data) ?? data;
        }

        private AstraRpgHealthConfigSO _config;
        private GameObject _go;
        private EntityHealth _entityHealth;
        private Mock<EntityCore> _mockEntityCore;
        private Mock<EntityStats> _mockEntityStats;
        private Mock<EntityCore> _mockDealerCore;
        private Mock<EntityStats> _mockDealerStats;
        private MockDamageSource _damageSource;
        private MockDamageType _damageType;
        private MockHealSource _healSource;

        [SetUp]
        public void Setup()
        {
            _damageSource = MockDamageSource.Create();
            _damageType = MockDamageType.Create();
            _healSource = MockHealSource.Create();

            // Create and configure the config with required and optional events before the GO activates
            _config = ScriptableObject.CreateInstance<AstraRpgHealthConfigSO>();
            _config.DefaultDamageCalculationCalculationStrategy = TestDamageCalculationStrategy.Create(d => d);
            _config.DefaultResurrectionSource = _healSource;
            _config.GlobalPreDamageInfoEvent = ScriptableObject.CreateInstance<PreDamageGameEvent>();
            _config.GlobalDamageResolutionEvent = ScriptableObject.CreateInstance<DamageResolutionGameEvent>();
            _config.GlobalEntityDiedEvent = ScriptableObject.CreateInstance<EntityDiedGameEvent>();
            _config.GlobalMaxHealthChangedEvent = ScriptableObject.CreateInstance<EntityMaxHealthChangedGameEvent>();
            _config.GlobalGainedHealthEvent = ScriptableObject.CreateInstance<EntityGainedHealthGameEvent>();
            _config.GlobalLostHealthEvent = ScriptableObject.CreateInstance<EntityLostHealthGameEvent>();
            _config.GlobalPreHealEvent = ScriptableObject.CreateInstance<PreHealGameEvent>();
            _config.GlobalEntityHealedEvent = ScriptableObject.CreateInstance<EntityHealedGameEvent>();
            _config.GlobalEntityResurrectedEvent = ScriptableObject.CreateInstance<EntityResurrectedGameEvent>();
            AstraRpgHealthConfigProvider.Instance = _config;

            _go = new GameObject("Entity");
            _mockEntityCore = new Mock<EntityCore>();
            _mockEntityStats = new Mock<EntityStats>();
            _mockEntityCore.Setup(c => c.Level).Returns(new EntityLevel());
            _mockEntityCore.Setup(c => c.Stats).Returns(_mockEntityStats.Object);
            _mockEntityStats.Setup(s => s.StatSet).Returns(ScriptableObject.CreateInstance<StatSet>());
            _mockEntityStats.Setup(s => s.Get(It.IsAny<Stat>())).Returns(0L);

            _mockDealerCore = new Mock<EntityCore>();
            _mockDealerStats = new Mock<EntityStats>();
            _mockDealerCore.Setup(c => c.Level).Returns(new EntityLevel());
            _mockDealerCore.Setup(c => c.Stats).Returns(_mockDealerStats.Object);
            _mockDealerStats.Setup(s => s.StatSet).Returns(ScriptableObject.CreateInstance<StatSet>());
            _mockDealerStats.Setup(s => s.Get(It.IsAny<Stat>())).Returns(0L);

            _go.AddComponent<EntityHealth>();
            _entityHealth = _go.GetComponent<EntityHealth>();
            _entityHealth._entityCore = _mockEntityCore.Object;
            _entityHealth._entityStats = _mockEntityStats.Object;

            _entityHealth._baseMaxHp = new LongRef { UseConstant = true, ConstantValue = MaxHp };
            _entityHealth._totalMaxHp = new LongRef { UseConstant = true, ConstantValue = MaxHp };
            _entityHealth._hp = new LongRef { UseConstant = true, ConstantValue = MaxHp };
            _entityHealth._deathThreshold = LongVarFactory.CreateLongVar(0);
            _entityHealth._barrier = new LongRef { UseConstant = true };
            _entityHealth.OverrideOnDeathGameAction = ScriptableObject.CreateInstance<DoNothingComponentGameAction>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_damageSource);
            Object.DestroyImmediate(_damageType);
            Object.DestroyImmediate(_healSource);
            AstraRpgHealthConfigProvider.Reset();
        }

        private PreDamageContext CreateDamageInfo(long amount)
        {
            return PreDamageContext.Builder
                .WithAmount(amount)
                .WithType(_damageType)
                .WithSource(_damageSource)
                .WithTarget(_mockEntityCore.Object)
                .WithDealer(_mockDealerCore.Object)
                .Build();
        }

        private PreHealContext CreateHealInfo(long amount)
        {
            return PreHealContext.Builder
                .WithAmount(amount)
                .WithSource(_healSource)
                .WithTarget(_mockEntityCore.Object)
                .WithHealer(_mockEntityCore.Object)
                .Build();
        }

        [Test]
        public void TestHealingDeadEntityThrowsException()
        {
            // Kill the entity
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());

            // Try to heal dead entity
            var ex = Assert.Throws<DeadEntityException>(() => 
                _entityHealth.Heal(CreateHealInfo(50))
            );
            
            Assert.IsNotNull(ex);
            Assert.AreEqual("Heal", ex.AttemptedOperation);
            Assert.AreEqual(0, ex.CurrentHp);
            Assert.AreEqual(_go.name, ex.EntityName);
        }

        [Test]
        public void TestSetHpToMaxOnDeadEntityThrowsException()
        {
            // Kill the entity
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());

            // Try to set HP to max on dead entity
            var ex = Assert.Throws<DeadEntityException>(() => 
                _entityHealth.SetHpToMax()
            );
            
            Assert.IsNotNull(ex);
            Assert.AreEqual("SetHpToMax", ex.AttemptedOperation);
        }

        [Test]
        public void TestTakeDamageOnDeadEntityIsPrevent()
        {
            // Kill the entity
            var firstDamage = _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());
            Assert.AreEqual(DamageOutcome.Applied, firstDamage.Outcome);

            // Try to damage dead entity
            var secondDamage = _entityHealth.TakeDamage(CreateDamageInfo(50));
            
            Assert.AreEqual(DamageOutcome.Prevented, secondDamage.Outcome);
            Assert.IsTrue((secondDamage.Reasons & DamagePreventionReason.EntityDead) != 0);
            Assert.AreEqual(0, _entityHealth.Hp);
        }

        [Test]
        public void TestIsDeadReturnsTrueAfterFatalDamage()
        {
            Assert.IsFalse(_entityHealth.IsDead());
            Assert.IsTrue(_entityHealth.IsAlive());

            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));

            Assert.IsTrue(_entityHealth.IsDead());
            Assert.IsFalse(_entityHealth.IsAlive());
        }

        [Test]
        public void TestResurrectWithHpRestoresEntity()
        {
            // Kill the entity
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());
            Assert.AreEqual(0, _entityHealth.Hp);

            // Resurrect with 50 HP (uses DefaultResurrectionSource from config)
            _entityHealth.Resurrect(50);

            Assert.IsFalse(_entityHealth.IsDead());
            Assert.IsTrue(_entityHealth.IsAlive());
            Assert.AreEqual(50, _entityHealth.Hp);
        }

        [Test]
        public void TestResurrectWithPercentageRestoresEntity()
        {
            // Kill the entity
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());

            // Resurrect with 75% HP (uses DefaultResurrectionSource from config)
            _entityHealth.Resurrect(new Percentage(75));

            Assert.IsFalse(_entityHealth.IsDead());
            Assert.AreEqual(75, _entityHealth.Hp);
        }

        [Test]
        public void TestResurrectAliveEntityThrowsException()
        {
            Assert.IsFalse(_entityHealth.IsDead());

            var ex = Assert.Throws<InvalidOperationException>(() => 
                _entityHealth.Resurrect(PreHealContext.Builder
                    .WithAmount(50)
                    .WithSource(_healSource)
                    .WithTarget(_mockEntityCore.Object)
                    .WithHealer(null)
                    .Build())
            );
            
            Assert.IsNotNull(ex);
            StringAssert.Contains("already alive", ex.Message);
        }

        [Test]
        public void TestHealingAfterResurrectionWorks()
        {
            // Kill and resurrect
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            _entityHealth.Resurrect(50);
            Assert.AreEqual(50, _entityHealth.Hp);

            // Heal should work now
            _entityHealth.Heal(CreateHealInfo(30));
            Assert.AreEqual(80, _entityHealth.Hp);
        }

        [Test]
        public void TestDamageAfterResurrectionWorks()
        {
            // Kill and resurrect
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            _entityHealth.Resurrect(50);

            // Damage should work now
            var dmgResult = _entityHealth.TakeDamage(CreateDamageInfo(20));
            Assert.AreEqual(DamageOutcome.Applied, dmgResult.Outcome);
            Assert.AreEqual(30, _entityHealth.Hp);
        }

        [Test]
        public void TestMaxHpChangeOnDeadEntitySkipsHealthAdjustment()
        {
            // Kill the entity
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());
            Assert.AreEqual(0, _entityHealth.Hp);

            // Change max HP
            _entityHealth.AddMaxHpFlatModifier(50, EntityHealth.HpBehaviourOnMaxHpIncrease.AddHealthUpToMaxHp);

            // HP should still be 0 (dead), not adjusted
            Assert.AreEqual(0, _entityHealth.Hp);
            Assert.AreEqual(150, _entityHealth.MaxHp);
            Assert.IsTrue(_entityHealth.IsDead());
        }

        [Test]
        public void TestResurrect_AppliesHealSourcePercentageModifier()
        {
            // Create a real StatSet containing the percentage stat
            var percentageStat = ScriptableObject.CreateInstance<Stat>();
            var statSet = ScriptableObject.CreateInstance<StatSet>();
            statSet._stats.Add(percentageStat);

            var resurrectionSource = MockHealSource.Create();
            resurrectionSource.PercentageHealModificationStat = percentageStat;

            // Mock: entity StatSet contains the stat and returns +50%
            _mockEntityStats.Setup(s => s.StatSet).Returns(statSet);
            _mockEntityStats.Setup(s => s.Get(percentageStat)).Returns(50L); // +50%

            // Kill the entity
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());

            // Re-inject stats mock to ensure it survives Unity null-check after TakeDamage
            _entityHealth._entityStats = _mockEntityStats.Object;

            // Resurrect with base 40 HP → 40 * 1.5 = 60
            _entityHealth.Resurrect(PreHealContext.Builder
                .WithAmount(40)
                .WithSource(resurrectionSource)
                .WithTarget(_mockEntityCore.Object)
                .WithHealer(null)
                .Build());

            Assert.IsFalse(_entityHealth.IsDead());
            Assert.AreEqual(60, _entityHealth.Hp);
        }

        [Test]
        public void TestResurrect_AppliesHealSourceFlatModifier()
        {
            // Create a real StatSet containing the flat stat
            var flatStat = ScriptableObject.CreateInstance<Stat>();
            var statSet = ScriptableObject.CreateInstance<StatSet>();
            statSet._stats.Add(flatStat);

            var resurrectionSource = MockHealSource.Create();
            resurrectionSource.FlatHealModificationStat = flatStat;

            // Mock: entity StatSet contains the stat and returns +10 flat
            _mockEntityStats.Setup(s => s.StatSet).Returns(statSet);
            _mockEntityStats.Setup(s => s.Get(flatStat)).Returns(10L); // +10 flat

            // Kill the entity
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());

            // Re-inject stats mock to ensure it survives Unity null-check after TakeDamage
            _entityHealth._entityStats = _mockEntityStats.Object;

            // Resurrect with base 30 HP → 30 + 10 = 40
            _entityHealth.Resurrect(PreHealContext.Builder
                .WithAmount(30)
                .WithSource(resurrectionSource)
                .WithTarget(_mockEntityCore.Object)
                .WithHealer(null)
                .Build());

            Assert.IsFalse(_entityHealth.IsDead());
            Assert.AreEqual(40, _entityHealth.Hp);
        }

        [Test]
        public void TestResurrect_FallbackWhenModifiersReduceHpBelowDeathThreshold()
        {
            // Create a real StatSet containing the flat stat
            var flatStat = ScriptableObject.CreateInstance<Stat>();
            var statSet = ScriptableObject.CreateInstance<StatSet>();
            statSet._stats.Add(flatStat);

            var resurrectionSource = MockHealSource.Create();
            resurrectionSource.FlatHealModificationStat = flatStat;

            // -100 flat → effective HP would be <= 0 (death threshold)
            _mockEntityStats.Setup(s => s.StatSet).Returns(statSet);
            _mockEntityStats.Setup(s => s.Get(flatStat)).Returns(-100L);

            // Kill the entity (death threshold is 0)
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            Assert.IsTrue(_entityHealth.IsDead());

            // Re-inject stats mock to ensure it survives Unity null-check after TakeDamage
            _entityHealth._entityStats = _mockEntityStats.Object;

            // Resurrect with base 30 HP; after -100 flat → ≤ 0 → fallback to deathThreshold(0) + 1 = 1
            _entityHealth.Resurrect(PreHealContext.Builder
                .WithAmount(30)
                .WithSource(resurrectionSource)
                .WithTarget(_mockEntityCore.Object)
                .WithHealer(null)
                .Build());

            Assert.IsFalse(_entityHealth.IsDead());
            Assert.IsTrue(_entityHealth.IsAlive());
            Assert.AreEqual(1, _entityHealth.Hp);
        }

        [Test]
        public void TestResurrect_RaisesResurrectedEventWithActualHp()
        {
            ResurrectionContext capturedContext = null;
            var resurrectEvent = ScriptableObject.CreateInstance<EntityResurrectedGameEvent>();
            resurrectEvent.OnEventRaised += ctx => capturedContext = ctx;
            _config.GlobalEntityResurrectedEvent = resurrectEvent;

            // Kill and resurrect without modifiers → HP = 50 (uses DefaultResurrectionSource from config)
            _entityHealth.TakeDamage(CreateDamageInfo(MaxHp));
            _entityHealth.Resurrect(50);

            Assert.IsNotNull(capturedContext);
            // PreviousValue is the HP before resurrection (entity was dead at 0)
            Assert.AreEqual(0, capturedContext.PreviousValue);
            // NewValue = PreviousValue + NetAmount = 0 + 50 = 50
            Assert.AreEqual(50, capturedContext.NewValue);
            // ReceivedHeal encapsulates the full heal result
            Assert.IsNotNull(capturedContext.ReceivedHeal);
            Assert.AreEqual(50, capturedContext.ReceivedHeal.HealAmount.RawAmount);
            Assert.AreEqual(50, capturedContext.ReceivedHeal.HealAmount.NetAmount);
        }
    }
}



