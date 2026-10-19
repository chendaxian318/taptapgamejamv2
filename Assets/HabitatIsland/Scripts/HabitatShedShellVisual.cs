using UnityEngine;
namespace HabitatIsland
{
    public sealed class HabitatShedShellVisual:MonoBehaviour
    {
        float elapsed;
        void Update()
        {
            elapsed=Mathf.Min(1,elapsed+Time.unscaledDeltaTime);
            float remaining=1-elapsed;
            transform.localPosition=new Vector3(.18f,.28f*remaining*remaining,-.12f);
            transform.localRotation=Quaternion.Euler(0,25,remaining*32);
        }
    }
}
