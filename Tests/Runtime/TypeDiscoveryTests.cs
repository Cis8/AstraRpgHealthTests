#if UNITY_EDITOR
using System.Linq;
using ElectricDrill.AstraRpgFramework.Editor.Utils;
using ElectricDrill.AstraRpgFramework.Utils;
using ElectricDrill.AstraHealth.Damage.CalculationPipeline;
using NUnit.Framework;

// Test types marked with ExcludeFromDerivedTypePicker attribute

[ExcludeFromDerivedTypePicker] public class TypeDiscoveryTestStepAlpha : DamageStep { public override string DisplayName => "Alpha"; public override DamageInfo ProcessStep(DamageInfo d)=>d; }
[ExcludeFromDerivedTypePicker] public class TypeDiscoveryTestStepBeta  : DamageStep { public override string DisplayName => "Beta";  public override DamageInfo ProcessStep(DamageInfo d)=>d; }

namespace ElectricDrill.AstraRpgHealthTests.Editor
{
    /// <summary>
    /// Tests for TypeDiscovery utility that discovers concrete types derived from a base type.
    /// </summary>
    public class TypeDiscoveryTests
    {
        [Test]
        public void GetConcreteDerivedTypes_ExcludesTypesWithAttribute()
        {
            // TypeDiscovery should automatically exclude types marked with ExcludeFromDerivedTypePickerAttribute
            // even when excludeTests is false and no custom filter is provided
            var types = TypeDiscovery.GetConcreteDerivedTypes<DamageStep>(excludeTests: false);

            // Types marked with [ExcludeFromDerivedTypePicker] should not be present
            Assert.IsFalse(types.Contains(typeof(TypeDiscoveryTestStepAlpha)),
                "TypeDiscoveryTestStepAlpha should be excluded due to [ExcludeFromDerivedTypePicker] attribute");
            Assert.IsFalse(types.Contains(typeof(TypeDiscoveryTestStepBeta)),
                "TypeDiscoveryTestStepBeta should be excluded due to [ExcludeFromDerivedTypePicker] attribute");
        }

        [Test]
        public void GetConcreteDerivedTypes_FindsConcreteTypes()
        {
            // Should find concrete DamageStep implementations
            var types = TypeDiscovery.GetConcreteDerivedTypes<DamageStep>(excludeTests: false);

            Assert.IsNotEmpty(types, "Should find at least some DamageStep implementations");
            Assert.IsTrue(types.All(t => !t.IsAbstract), "All returned types should be concrete (non-abstract)");
            Assert.IsTrue(types.All(t => typeof(DamageStep).IsAssignableFrom(t)),
                "All returned types should derive from DamageStep");
        }

        [Test]
        public void GetConcreteDerivedTypes_RespectsCustomFilter()
        {
            // Custom filter should further restrict the results
            var types = TypeDiscovery.GetConcreteDerivedTypes<DamageStep>(
                excludeTests: false,
                filter: t => t.Name.Contains("NonExistentPattern"));

            Assert.IsEmpty(types, "Custom filter should exclude all types when no matches are found");
        }

        [Test]
        public void GetConcreteDerivedTypes_ExcludesAbstractTypes()
        {
            // Verify that abstract types are not included
            var types = TypeDiscovery.GetConcreteDerivedTypes<DamageStep>(excludeTests: false);

            Assert.IsFalse(types.Any(t => t.IsAbstract),
                "No abstract types should be returned");
        }

        [Test]
        public void GetConcreteDerivedTypes_ResultsAreSorted()
        {
            // Results should be sorted by name for consistent display
            var types = TypeDiscovery.GetConcreteDerivedTypes<DamageStep>(excludeTests: false);

            if (types.Length > 1)
            {
                var names = types.Select(t => t.Name).ToArray();
                var sortedNames = names.OrderBy(n => n).ToArray();

                CollectionAssert.AreEqual(sortedNames, names,
                    "Types should be sorted alphabetically by name");
            }
        }
    }
}
#endif
