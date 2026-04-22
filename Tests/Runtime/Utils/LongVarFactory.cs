using ElectricDrill.AstraRpgFramework.Utils;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealthTests
{
    public static class LongVarFactory
    {
        public static LongVarSO CreateLongVar(long value)
        {
            var longVar = ScriptableObject.CreateInstance<LongVarSO>();
            longVar.Value = value;
            return longVar;
        }
    }
}