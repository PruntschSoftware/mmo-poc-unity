using Unity.Netcode.Components;
using UnityEngine;

namespace MmoPoC.Networking
{
    /// <summary>
    /// A custom NetworkTransform that allows the owner of the object to have authority over its position and rotation.
    /// Crucial for client-controlled objects (like a Player with a CharacterController).
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
