using System.Collections;
using ElectricDrill.AstraRpgFramework.Experience;
using ElectricDrill.AstraRpgFramework.Ownership;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraHealth.Experience;
using ElectricDrill.AstraHealth.Experience.Strategies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ElectricDrill.AstraRpgHealthTests.Tests.PlayMode.TestHealthFactory;

namespace ElectricDrill.AstraRpgHealthTests.Tests.PlayMode
{
    public class ExpCollectorOwnershipTests
    {
        private sealed class TestExpSource : MonoBehaviour, IExpSource
        {
            public bool Harvested { get; set; }
            public long Exp => 50;
        }

        // Minimal working growth formula: only level 1's constant is needed since the test never
        // levels up, but NextLevelTotalExperience() unconditionally asserts a formula is assigned.
        private static GrowthFormulaSO CreateFlatGrowthFormula(int maxLevel, long expForLevel2)
        {
            var formula = ScriptableObject.CreateInstance<GrowthFormulaSO>();
            formula._maxLevel = ScriptableObject.CreateInstance<IntVarSO>();
            formula._maxLevel.Value = maxLevel;
            formula._useConstantAtLvl1 = true;
            formula._constantAtLvl1 = expForLevel2;
            formula.OnValidate();
            return formula;
        }

        [UnityTest]
        public IEnumerator ShipExpCollector_GainsXp_ForKillDealtByOwnedWeapon()
        {
            yield return null;

            var ship = CreateEntity("Ship");
            var weapon = CreateOwnedEntity(ship.Core, "Weapon", sharedConfig: ship.Config, sharedEvents: ship.Events);
            var victim = CreateEntity("Victim", sharedConfig: ship.Config, sharedEvents: ship.Events, maxHp: 10);
            var expSource = victim.Go.AddComponent<TestExpSource>();

            ship.Core.Level.ExperienceGrowthFormula = CreateFlatGrowthFormula(maxLevel: 20, expForLevel2: 1000);
            ship.Config.PerformerAttribution = EntityAttribution.Root;
            ship.Config.DefaultExpCollectionStrategy = ScriptableObject.CreateInstance<DirectKillExpStrategySO>();
            ship.Go.AddComponent<ExpCollector>();

            yield return null; // let Start() run for the entities created above

            long expBefore = ship.Core.Level.CurrentTotalExperience;

            // Weapon (owned by ship) deals the lethal blow.
            victim.Health.TakeDamage(BuildPre(20, weapon, victim));

            Assert.IsTrue(expSource.Harvested, "The kill's experience source must be harvested.");
            Assert.AreEqual(expBefore + expSource.Exp, ship.Core.Level.CurrentTotalExperience,
                "The ship must gain XP for a kill dealt by its owned weapon.");

            Object.DestroyImmediate(ship.Go);
            Object.DestroyImmediate(weapon.Go);
            Object.DestroyImmediate(victim.Go);
            Object.DestroyImmediate(ship.DefaultDamageType);
            Object.DestroyImmediate(ship.DefaultDamageSource);
            Object.DestroyImmediate(weapon.DefaultDamageType);
            Object.DestroyImmediate(weapon.DefaultDamageSource);
            Object.DestroyImmediate(victim.DefaultDamageType);
            Object.DestroyImmediate(victim.DefaultDamageSource);
            Object.DestroyImmediate(ship.Config);
            Object.DestroyImmediate(ship.Events.PreDmg);
            Object.DestroyImmediate(ship.Events.DamageResolution);
            Object.DestroyImmediate(ship.Events.MaxHpChanged);
            Object.DestroyImmediate(ship.Events.Changed);
            Object.DestroyImmediate(ship.Events.Died);
            Object.DestroyImmediate(ship.Events.PreHeal);
            Object.DestroyImmediate(ship.Events.Healed);
            Object.DestroyImmediate(ship.Events.Resurrected);
        }
    }
}
