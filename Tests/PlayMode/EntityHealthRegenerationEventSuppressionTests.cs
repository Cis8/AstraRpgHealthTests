using System.Collections;
using ElectricDrill.AstraRpgFramework.Stats;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraRpgHealth.Heal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ElectricDrill.AstraRpgHealthTests.Tests.PlayMode.TestHealthFactory;

namespace ElectricDrill.AstraRpgHealthTests.Tests.PlayMode
{
    /// <summary>
    /// Tests for the event suppression feature on health regeneration (passive and manual) and lifesteal.
    /// Verifies that PreHeal and ReceivedHeal events can be suppressed based on configuration flags.
    /// 
    /// NOTE: Lifesteal is NOT configured by default - it's only enabled in lifesteal-specific tests.
    /// This ensures clean, predictable event counts in regeneration tests.
    /// </summary>
    public class EntityHealthRegenerationEventSuppressionTests
    {
        private HealthEntityBundle _entity;
        private HealthEntityBundle _target;
        private HealthEventsBundle _sharedEvents;
        private HealSource _regenHealSource;
        private HealSource _normalHealSource;
        private HealSource _lifestealHealSource;
        private Stat _passiveRegenStat;
        private Stat _manualRegenStat;
        private Stat _lifestealStat;
        private LifestealConfig _lifestealConfig;
        
        private int _preHealEventCount;
        private int _receivedHealEventCount;

        [SetUp]
        public void SetUp()
        {
            _sharedEvents = CreateSharedEvents();
            
            // Create heal sources
            _regenHealSource = ScriptableObject.CreateInstance<HealSource>();
            _regenHealSource.name = "RegenHealSource";
            _normalHealSource = ScriptableObject.CreateInstance<HealSource>();
            _normalHealSource.name = "NormalHealSource";
            _lifestealHealSource = ScriptableObject.CreateInstance<HealSource>();
            _lifestealHealSource.name = "LifestealHealSource";
            
            // Create stats
            _passiveRegenStat = ScriptableObject.CreateInstance<Stat>();
            _passiveRegenStat.name = "PassiveRegenStat";
            _manualRegenStat = ScriptableObject.CreateInstance<Stat>();
            _manualRegenStat.name = "ManualRegenStat";
            _lifestealStat = ScriptableObject.CreateInstance<Stat>();
            _lifestealStat.name = "LifestealStat";

            // Create entities
            _entity = CreateEntity("TestEntity", initializeStats: true, sharedEvents: _sharedEvents);
            _target = CreateEntity("TargetEntity", sharedConfig: _entity.Config, sharedEvents: _sharedEvents);
            
            // Configure regeneration
            _entity.Config.HealthRegenerationSource = _regenHealSource;
            _entity.Config.PassiveHealthRegenerationStat = _passiveRegenStat;
            _entity.Config.PassiveHealthRegenerationInterval = 1f;
            _entity.Config.ManualHealthRegenerationStat = _manualRegenStat;
            
            // Inject regeneration stats (10 HP/10s passive, 15 HP manual)
            InjectFlatStat(_entity.Stats, _passiveRegenStat, 10);
            InjectFlatStat(_entity.Stats, _manualRegenStat, 15);
            
            // Setup event listeners
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            _sharedEvents.PreHeal.OnEventRaised += _ => _preHealEventCount++;
            _sharedEvents.Healed.OnEventRaised += _ => _receivedHealEventCount++;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_entity.Go);
            Object.DestroyImmediate(_target.Go);
            Object.DestroyImmediate(_entity.DefaultDamageType);
            Object.DestroyImmediate(_entity.DefaultDamageSource);
            Object.DestroyImmediate(_target.DefaultDamageType);
            Object.DestroyImmediate(_target.DefaultDamageSource);
            Object.DestroyImmediate(_entity.Config);
            Object.DestroyImmediate(_regenHealSource);
            Object.DestroyImmediate(_normalHealSource);
            Object.DestroyImmediate(_lifestealHealSource);
            Object.DestroyImmediate(_passiveRegenStat);
            Object.DestroyImmediate(_manualRegenStat);
            Object.DestroyImmediate(_lifestealStat);
            if (_lifestealConfig != null) Object.DestroyImmediate(_lifestealConfig);

            Object.DestroyImmediate(_sharedEvents.PreDmg);
            Object.DestroyImmediate(_sharedEvents.DamageResolution);
            Object.DestroyImmediate(_sharedEvents.MaxHpChanged);
            Object.DestroyImmediate(_sharedEvents.Gained);
            Object.DestroyImmediate(_sharedEvents.Lost);
            Object.DestroyImmediate(_sharedEvents.Died);
            Object.DestroyImmediate(_sharedEvents.PreHeal);
            Object.DestroyImmediate(_sharedEvents.Healed);
        }

        /// <summary>
        /// Configures lifesteal for tests that need it.
        /// Call this in tests that specifically test lifesteal functionality.
        /// </summary>
        private void ConfigureLifesteal()
        {
            InjectPercentageStat(_entity.Stats, _lifestealStat, new Percentage(25));
            _lifestealConfig = AssignLifestealMapping(_entity.Config, _entity.DefaultDamageType, _lifestealStat, _lifestealHealSource);
            _entity.Config.LifestealConfig = _lifestealConfig;
        }

        #region Passive Regeneration Tests

        [UnityTest]
        public IEnumerator TestPassiveRegenerationEventsNotSuppressedByDefault()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            Assert.IsFalse(_entity.Config.SuppressPassiveRegenerationEvents);
            
            _entity.Health.TakeDamage(BuildPre(50, _entity, _entity));
            SetPrivateField(_entity.Health, "_passiveHealthRegeneration", true);
            
            yield return new WaitForSeconds(1.2f);
            
            Assert.Greater(_preHealEventCount, 0, "PreHeal event should have been raised for passive regeneration");
            Assert.Greater(_receivedHealEventCount, 0, "ReceivedHeal event should have been raised for passive regeneration");
        }

        [UnityTest]
        public IEnumerator TestPassiveRegenerationEventsSuppressedWhenConfigured()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            _entity.Config.SuppressPassiveRegenerationEvents = true;
            _entity.Health.TakeDamage(BuildPre(50, _entity, _entity));
            SetPrivateField(_entity.Health, "_passiveHealthRegeneration", true);
            
            yield return new WaitForSeconds(1.2f);
            
            Assert.AreEqual(0, _preHealEventCount, "PreHeal event should NOT have been raised");
            Assert.AreEqual(0, _receivedHealEventCount, "ReceivedHeal event should NOT have been raised");
            Assert.Greater(_entity.Health.Hp, 50, "Health should still increase despite suppression");
        }

        [UnityTest]
        public IEnumerator TestMultiplePassiveTicksWithSuppression()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            _entity.Config.SuppressPassiveRegenerationEvents = true;
            _entity.Health.TakeDamage(BuildPre(50, _entity, _entity));
            SetPrivateField(_entity.Health, "_passiveHealthRegeneration", true);
            
            long initialHp = _entity.Health.Hp;
            yield return new WaitForSeconds(3.2f);
            
            Assert.AreEqual(0, _preHealEventCount, "No events should be raised across multiple ticks");
            Assert.AreEqual(0, _receivedHealEventCount, "No events should be raised across multiple ticks");
            Assert.Greater(_entity.Health.Hp, initialHp, "Health should increase from multiple ticks");
        }

        #endregion

        #region Manual Regeneration Tests

        [UnityTest]
        public IEnumerator TestManualRegenerationEventsNotSuppressedByDefault()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            Assert.IsFalse(_entity.Config.SuppressManualRegenerationEvents);
            
            _entity.Health.TakeDamage(BuildPre(50, _entity, _entity));
            _entity.Health.ManualHealthRegenerationTick();
            yield return null;
            
            Assert.AreEqual(1, _preHealEventCount, "PreHeal event should have been raised once");
            Assert.AreEqual(1, _receivedHealEventCount, "ReceivedHeal event should have been raised once");
        }

        [UnityTest]
        public IEnumerator TestManualRegenerationEventsSuppressedWhenConfigured()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            _entity.Config.SuppressManualRegenerationEvents = true;
            _entity.Health.TakeDamage(BuildPre(50, _entity, _entity));
            _entity.Health.ManualHealthRegenerationTick();
            yield return null;
            
            Assert.AreEqual(0, _preHealEventCount, "PreHeal event should NOT have been raised");
            Assert.AreEqual(0, _receivedHealEventCount, "ReceivedHeal event should NOT have been raised");
            Assert.AreEqual(65, _entity.Health.Hp, "Health should be 65 (50 + 15)");
        }

        [UnityTest]
        public IEnumerator TestMultipleManualTicksWithSuppression()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            _entity.Config.SuppressManualRegenerationEvents = true;
            _entity.Health.TakeDamage(BuildPre(50, _entity, _entity));
            
            _entity.Health.ManualHealthRegenerationTick();
            _entity.Health.ManualHealthRegenerationTick();
            _entity.Health.ManualHealthRegenerationTick();
            yield return null;
            
            Assert.AreEqual(0, _preHealEventCount, "No events should be raised across multiple ticks");
            Assert.AreEqual(0, _receivedHealEventCount, "No events should be raised across multiple ticks");
            Assert.AreEqual(95, _entity.Health.Hp, "Health should be 95 (50 + 45)");
        }

        #endregion

        #region Normal Heal and Independence Tests

        [UnityTest]
        public IEnumerator TestNormalHealEventsNotAffectedBySuppressionFlags()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            _entity.Config.SuppressPassiveRegenerationEvents = true;
            _entity.Config.SuppressManualRegenerationEvents = true;
            
            _entity.Health.TakeDamage(BuildPre(50, _entity, _entity));
            _entity.Health.Heal(PreHealInfo.Builder
                .WithAmount(20)
                .WithSource(_normalHealSource)
                .WithHealer(_entity.Core)
                .WithTarget(_entity.Core)
                .Build());
            yield return null;
            
            Assert.AreEqual(1, _preHealEventCount, "Normal heal should raise events");
            Assert.AreEqual(1, _receivedHealEventCount, "Normal heal should raise events");
        }

        [UnityTest]
        public IEnumerator TestPassiveAndManualSuppressionFlagsAreIndependent()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            _entity.Config.SuppressPassiveRegenerationEvents = true;
            _entity.Config.SuppressManualRegenerationEvents = false;
            
            _entity.Health.TakeDamage(BuildPre(50, _entity, _entity));
            SetPrivateField(_entity.Health, "_passiveHealthRegeneration", true);
            
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0, _preHealEventCount, "No events from passive regen");
            Assert.AreEqual(0, _receivedHealEventCount, "No events from passive regen");
            
            _entity.Health.ManualHealthRegenerationTick();
            yield return null;
            
            Assert.AreEqual(1, _preHealEventCount, "Manual regen should raise events");
            Assert.AreEqual(1, _receivedHealEventCount, "Manual regen should raise events");
        }

        #endregion

        #region Lifesteal Tests

        [UnityTest]
        public IEnumerator TestLifestealEventsNotSuppressedByDefault()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            ConfigureLifesteal(); // Enable lifesteal for this test
            Assert.IsFalse(_entity.Config.SuppressLifestealEvents);
            
            _entity.Health.TakeDamage(BuildPre(30, _target, _entity));
            Assert.AreEqual(70, _entity.Health.Hp, "Entity should have 70 HP (100 - 30)");
            
            _target.Health.TakeDamage(BuildPre(40, _entity, _target));
            yield return null;
            
            Assert.AreEqual(1, _preHealEventCount, "Lifesteal should raise PreHeal event");
            Assert.AreEqual(1, _receivedHealEventCount, "Lifesteal should raise ReceivedHeal event");
            Assert.AreEqual(80, _entity.Health.Hp, "Entity should have 80 HP (70 + 10 lifesteal)");
        }

        [UnityTest]
        public IEnumerator TestLifestealEventsSuppressedWhenConfigured()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            ConfigureLifesteal(); // Enable lifesteal for this test
            _entity.Config.SuppressLifestealEvents = true;
            
            _entity.Health.TakeDamage(BuildPre(30, _target, _entity));
            Assert.AreEqual(70, _entity.Health.Hp);
            
            _target.Health.TakeDamage(BuildPre(40, _entity, _target));
            yield return null;
            
            Assert.AreEqual(0, _preHealEventCount, "Lifesteal should NOT raise events");
            Assert.AreEqual(0, _receivedHealEventCount, "Lifesteal should NOT raise events");
            Assert.AreEqual(80, _entity.Health.Hp, "Health should still increase (70 + 10)");
        }

        [UnityTest]
        public IEnumerator TestLifestealSuppressionIndependentFromRegeneration()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            ConfigureLifesteal(); // Enable lifesteal for this test
            _entity.Config.SuppressLifestealEvents = true;
            _entity.Config.SuppressManualRegenerationEvents = false;
            
            _entity.Health.TakeDamage(BuildPre(30, _target, _entity));
            _target.Health.TakeDamage(BuildPre(40, _entity, _target));
            yield return null;
            
            Assert.AreEqual(0, _preHealEventCount, "No events from lifesteal");
            Assert.AreEqual(0, _receivedHealEventCount, "No events from lifesteal");
            
            _entity.Health.ManualHealthRegenerationTick();
            yield return null;
            
            Assert.AreEqual(1, _preHealEventCount, "Manual regen should raise events");
            Assert.AreEqual(1, _receivedHealEventCount, "Manual regen should raise events");
        }

        [UnityTest]
        public IEnumerator TestMultipleLifestealTicksWithSuppression()
        {
            yield return null;
            _preHealEventCount = 0;
            _receivedHealEventCount = 0;
            
            ConfigureLifesteal(); // Enable lifesteal for this test
            _entity.Config.SuppressLifestealEvents = true;
            
            _entity.Health.TakeDamage(BuildPre(40, _target, _entity));
            long initialHp = _entity.Health.Hp;
            
            _target.Health.TakeDamage(BuildPre(20, _entity, _target));
            _target.Health.TakeDamage(BuildPre(20, _entity, _target));
            _target.Health.TakeDamage(BuildPre(20, _entity, _target));
            yield return null;
            
            Assert.AreEqual(0, _preHealEventCount, "No events across multiple lifesteal procs");
            Assert.AreEqual(0, _receivedHealEventCount, "No events across multiple lifesteal procs");
            Assert.Greater(_entity.Health.Hp, initialHp, "Health should increase from lifesteal");
        }

        #endregion
    }
}
