using UnityEngine;

namespace HabitatIsland
{
    /// <summary>Keep the cinematic net's grip on the animated palm while its parent owns stow rotation/visibility.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class HabitatLandingNetGrip : MonoBehaviour
    {
        Transform hand, finger;

        public void Bind(HabitatPlayerVisual player)
        {
            hand = finger = null;
            if (!player) return;
            foreach (Transform bone in player.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "mixamorig:RightHand") hand = bone;
                else if (bone.name == "mixamorig:RightHandMiddle1") finger = bone;
            }
            LateUpdate();
        }

        void LateUpdate()
        {
            if (!hand) return;
            transform.position = finger ? Vector3.Lerp(hand.position, finger.position, .65f) : hand.position;
            Transform socket = transform.parent;
            if (socket && socket.parent)
            {
                // glTF's mirrored character can put RightHand on local -X. Tilt
                // and stow outwards on that side, keeping the hoop clear of the hat.
                bool mirrored = socket.parent.InverseTransformPoint(hand.position).x < 0f;
                float angle = Mathf.DeltaAngle(0f, socket.localEulerAngles.z);
                float outward = (mirrored ? -1f : 1f) * (angle - 17f);
                transform.rotation = socket.parent.rotation * Quaternion.Euler(0, 0, outward)
                    * Quaternion.Euler(0, 180, 0); // Basket projects forward, away from the hat/back basket.
            }
        }
    }
}
