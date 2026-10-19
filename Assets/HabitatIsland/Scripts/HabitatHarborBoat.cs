using UnityEngine;

namespace HabitatIsland
{
    /// <summary>The same authored vessel is used at the berth, in the opening and when sailing.</summary>
    public sealed class HabitatHarborBoat : MonoBehaviour
    {
        public Transform Vessel { get; private set; }
        public Transform Footwell { get; private set; }
        public Transform Seat { get; private set; }
        Transform portRim, starboardRim;
        Vector3 berth;
        Quaternion berthRotation;
        public Vector3 Heading => berthRotation * Vector3.forward;
        public Vector3 Berth => berth;
        public Vector3 PassengerPosition => Footwell ? Footwell.position + Vector3.up * .018f : Vessel.position + Vector3.up * .37f;
        public Vector3 BoardingPosition
        {
            get
            {
                Vector3 dock = transform.TransformPoint(HabitatImportedDockLayout.BoardingEnd);
                if (!portRim || !starboardRim) return dock;
                return (Vector3.SqrMagnitude(portRim.position - dock) < Vector3.SqrMagnitude(starboardRim.position - dock)
                    ? portRim.position : starboardRim.position) + Vector3.up * .025f;
            }
        }

        public void Initialize(float waterLevel)
        {
            GameObject prefab = Resources.Load<GameObject>("Models/Story/DepartureBoat");
            if (!prefab || Vessel) return;
            Vessel = Instantiate(prefab, transform, false).transform;
            Vessel.name = "Wooden boat - berth and story";
            Vessel.localScale = Vector3.one / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.y));
            berth = transform.TransformPoint(HabitatImportedDockLayout.BoatArrival);
            // Keep the open footwell above the painted sea (waterLevel*.7 + .025).
            // This is a small visual draft adjustment; never distort the delivered hull.
            berth.y = HabitatRules.WaterSurfaceY((int)waterLevel) + .185f;
            Vector3 heading = berth - transform.TransformPoint(HabitatImportedDockLayout.DockArrival);
            heading.y = 0;
            berthRotation = Quaternion.LookRotation(heading.normalized);
            foreach (Transform child in Vessel.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Footwell_Aft_Surface") Footwell = child;
                if (child.name == "Seat_Center_Surface") Seat = child;
                if (child.name == "Boarding_Port_Rim") portRim = child;
                if (child.name == "Boarding_Starboard_Rim") starboardRim = child;
            }
            RestoreBerth();
        }

        public void SampleSailing(float progress, float time)
        {
            if (!Vessel) return;
            Vector3 position = berth + Heading * (8f * Mathf.SmoothStep(0f, 1f, progress));
            // A shallow draft plus subtle rocking keeps the open hull clear of water.
            position.y += Mathf.Sin(time * 2.7f) * .005f;
            Vessel.SetPositionAndRotation(position, berthRotation * Quaternion.Euler(0, 0, Mathf.Sin(time * 2.1f) * .8f));
        }

        public void RestoreBerth()
        {
            if (Vessel) Vessel.SetPositionAndRotation(berth, berthRotation);
        }
    }
}
