using System.Collections;
using ElectricDrill.AstraRpgFramework.Utils.Executables;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests.TestUtils
{
    public class DestroyImmediateGameAction : GameAction<Component>
    {
        public override IEnumerator Execute(Component component) {
            if (component != null && component.gameObject != null) {
                Object.DestroyImmediate(component.gameObject);
            }
            yield break;
        }
    }
}