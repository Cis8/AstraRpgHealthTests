using System;
using System.Reflection;
using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgHealth.Events;
using ElectricDrill.AstraRpgHealth.Events.Contexts;
using ElectricDrill.AstraRpgHealth.Triggers;
using ElectricDrill.AstraRpgFrameworkTests.Utils;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ElectricDrill.AstraRpgHealthTests.Tests.Runtime.Triggers
{
    [TestFixture]
    public class EntityDiedGameEventTriggerTests
    {
        private EntityDiedGameEvent _event;
        private EntityDiedGameEventTrigger _trigger;
        private GameObject _goA;
        private GameObject _goB;
        private EntityCore _holderA;
        private EntityCore _holderB;

        [SetUp]
        public void Setup()
        {
            _event = ScriptableObject.CreateInstance<EntityDiedGameEvent>();
            _trigger = new EntityDiedGameEventTrigger();
            var field = typeof(EntityDiedGameEventTrigger)
                .GetField("_gameEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, "Reflection lookup failed: EntityDiedGameEventTrigger._gameEvent field not found. Has it been renamed?");
            field.SetValue(_trigger, _event);

            _goA = new GameObject("HolderA");
            _goB = new GameObject("HolderB");
            _holderA = _goA.AddComponent<EntityCoreMock>();
            _holderB = _goB.AddComponent<EntityCoreMock>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_goA);
            Object.DestroyImmediate(_goB);
            Object.DestroyImmediate(_event);
        }

        private EntityDiedContext MakeContext(EntityCore victim)
            => new EntityDiedContext(victim, null, null);

        [Test]
        public void Broadcast_TwoHolders_BothReceivePayload()
        {
            object receivedA = null, receivedB = null;
            _trigger.Subscribe(_holderA, p => receivedA = p);
            _trigger.Subscribe(_holderB, p => receivedB = p);

            var ctx = MakeContext(_holderA);
            _event.Raise(ctx);

            Assert.AreSame(ctx, receivedA, "HolderA callback must receive the raised context");
            Assert.AreSame(ctx, receivedB, "HolderB callback must receive the raised context");
        }

        [Test]
        public void Payload_IsBoxedAsObject()
        {
            object received = null;
            _trigger.Subscribe(_holderA, p => received = p);

            var ctx = MakeContext(_holderA);
            _event.Raise(ctx);

            Assert.IsInstanceOf<EntityDiedContext>(received);
            Assert.AreSame(ctx, (EntityDiedContext)received);
        }

        [Test]
        public void MultiCallback_SameHolder_AllCallbacksFire()
        {
            int count1 = 0, count2 = 0;
            Action<object> cb1 = _ => count1++;
            Action<object> cb2 = _ => count2++;

            _trigger.Subscribe(_holderA, cb1);
            _trigger.Subscribe(_holderA, cb2);

            _event.Raise(MakeContext(null));

            Assert.AreEqual(1, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void SameCallbackTwice_IsIdempotent()
        {
            int count = 0;
            Action<object> cb = _ => count++;

            _trigger.Subscribe(_holderA, cb);
            _trigger.Subscribe(_holderA, cb);

            _event.Raise(MakeContext(null));

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Unsubscribe_SpecificCallback_OnlyThatOneIsRemoved()
        {
            int count1 = 0, count2 = 0;
            Action<object> cb1 = _ => count1++;
            Action<object> cb2 = _ => count2++;

            _trigger.Subscribe(_holderA, cb1);
            _trigger.Subscribe(_holderA, cb2);
            _trigger.Unsubscribe(_holderA, cb1);

            _event.Raise(MakeContext(null));

            Assert.AreEqual(0, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Unsubscribe_OneHolder_DoesNotAffectOtherHolder()
        {
            int countA = 0, countB = 0;
            Action<object> cbA = _ => countA++;
            Action<object> cbB = _ => countB++;

            _trigger.Subscribe(_holderA, cbA);
            _trigger.Subscribe(_holderB, cbB);
            _trigger.Unsubscribe(_holderA, cbA);

            _event.Raise(MakeContext(null));

            Assert.AreEqual(0, countA);
            Assert.AreEqual(1, countB);
        }

        [Test]
        public void Unsubscribe_NonExistentPair_DoesNotThrow()
        {
            Action<object> cb = _ => { };
            Assert.DoesNotThrow(() => _trigger.Unsubscribe(_holderA, cb));
        }
    }
}
