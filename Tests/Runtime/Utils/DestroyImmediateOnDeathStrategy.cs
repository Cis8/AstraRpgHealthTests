using System.Collections;
using ElectricDrill.AstraRpgHealth;
using ElectricDrill.AstraRpgHealth.Death;

namespace ElectricDrill.AstraRpgHealthTests
{
    public class DestroyImmediateOnDeathStrategy : OnDeathStrategy
    {
        public override IEnumerator Execute(EntityHealth entityHealth) {
            DestroyImmediate(entityHealth.gameObject);
            yield break;
        }
    }
}