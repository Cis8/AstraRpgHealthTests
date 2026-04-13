using System.Reflection;
using System.Threading;
using ElectricDrill.AstraRpgFramework;
using ElectricDrill.AstraRpgFramework.Contexts;
using ElectricDrill.AstraRpgFramework.GameActions;
using ElectricDrill.AstraRpgHealth.Damage;
using ElectricDrill.AstraRpgHealth.Events.Contexts;
using ElectricDrill.AstraRpgHealth.GameActions.Actions.WithIHasEntity;
using NUnit.Framework;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests
{
    public class EntityContextCastProjectionGameActionTests
    {
        // Unity cannot instantiate generic ScriptableObject types via CreateInstance<T>().
        // Concrete non-generic subclasses are required for each context type used in tests.
        private class RecordingGameAction<TContext> : GameAction<TContext>
        {
            public int CallCount { get; private set; }
            public TContext LastContext { get; private set; }

#pragma warning disable CS1998
            public override async Awaitable ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastContext = context;
            }
#pragma warning restore CS1998
        }

        private sealed class RecordingPreDamageAction : RecordingGameAction<PreDamageContext> { }
        private sealed class RecordingEntityDiedAction : RecordingGameAction<EntityDiedContext> { }
        private sealed class RecordingDamageResolutionAction : RecordingGameAction<DamageResolutionContext> { }

        private sealed class DummyEntityContext : IHasEntity
        {
            public EntityCore Entity => null;
        }

        private sealed class TestDamageSource : DamageSourceSO
        {
        }

        private sealed class TestDamageType : DamageTypeSO
        {
        }

        [Test]
        public void PreDamageProjection_ForwardsPayload_WhenRuntimeTypeMatches()
        {
            var projection = ScriptableObject.CreateInstance<EntityContextToPreDamageContextProjectionGameAction>();
            var innerAction = ScriptableObject.CreateInstance<RecordingPreDamageAction>();
            SetInnerAction(projection, innerAction);

            var context = CreatePreDamageContext();

            projection.ExecuteAsync(context);

            Assert.That(innerAction.CallCount, Is.EqualTo(1));
            Assert.That(innerAction.LastContext, Is.SameAs(context));
        }

        [Test]
        public void EntityDiedProjection_ForwardsPayload_WhenRuntimeTypeMatches()
        {
            var projection = ScriptableObject.CreateInstance<EntityContextToEntityDiedContextProjectionGameAction>();
            var innerAction = ScriptableObject.CreateInstance<RecordingEntityDiedAction>();
            SetInnerAction(projection, innerAction);

            var context = new EntityDiedContext(null, null, CreateDamageResolutionContext());

            projection.ExecuteAsync(context);

            Assert.That(innerAction.CallCount, Is.EqualTo(1));
            Assert.That(innerAction.LastContext, Is.SameAs(context));
        }

        [Test]
        public void DamageResolutionProjection_ForwardsPayload_WhenRuntimeTypeMatches()
        {
            var projection = ScriptableObject.CreateInstance<EntityContextToDamageResolutionContextProjectionGameAction>();
            var innerAction = ScriptableObject.CreateInstance<RecordingDamageResolutionAction>();
            SetInnerAction(projection, innerAction);

            var context = CreateDamageResolutionContext();

            projection.ExecuteAsync(context);

            Assert.That(innerAction.CallCount, Is.EqualTo(1));
            Assert.That(innerAction.LastContext, Is.SameAs(context));
        }

        [Test]
        public void Projection_DoesNothing_WhenRuntimeTypeDoesNotMatch()
        {
            var projection = ScriptableObject.CreateInstance<EntityContextToPreDamageContextProjectionGameAction>();
            var innerAction = ScriptableObject.CreateInstance<RecordingPreDamageAction>();
            SetInnerAction(projection, innerAction);

            projection.ExecuteAsync(new DummyEntityContext());

            Assert.That(innerAction.CallCount, Is.Zero);
            Assert.That(innerAction.LastContext, Is.Null);
        }

        private static void SetInnerAction<TProjectedContext>(
            ScriptableObject projection,
            GameAction<TProjectedContext> innerAction)
            where TProjectedContext : class, IHasEntity
        {
            var field = projection.GetType().BaseType?.GetField("_innerAction", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field!.SetValue(projection, innerAction);
        }

        private static PreDamageContext CreatePreDamageContext()
        {
            var damageType = ScriptableObject.CreateInstance<TestDamageType>();
            var damageSource = ScriptableObject.CreateInstance<TestDamageSource>();

            return PreDamageContext.Builder
                .WithAmount(10)
                .WithType(damageType)
                .WithSource(damageSource)
                .WithTarget(null)
                .Build();
        }

        private static DamageResolutionContext CreateDamageResolutionContext()
        {
            return DamageResolutionContext.Prevented(DamagePreventionReason.EntityDead, CreatePreDamageContext());
        }
    }
}
