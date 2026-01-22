using System.Threading;
using ElectricDrill.AstraRpgFramework.Utils.Executables;
using UnityEngine;

namespace ElectricDrill.AstraRpgHealth.Tests.Runtime.Utils
{
    /// <summary>
    /// Test action that immediately destroys a Component's GameObject.
    /// </summary>
    public class DestroyImmediateGameAction : GameAction<Component>
    {
        public override Awaitable ExecuteAsync(Component context, CancellationToken cancellationToken = default)
        {
            if (context != null && context.gameObject != null)
            {
                Object.DestroyImmediate(context.gameObject);
            }
            return Awaitable.NextFrameAsync(cancellationToken);
        }
    }
}