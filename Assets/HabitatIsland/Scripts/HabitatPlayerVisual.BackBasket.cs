using UnityEngine;

namespace HabitatIsland
{
    public sealed partial class HabitatPlayerVisual
    {
        const string BackBasketPath = "Models/BackBasket/Basket";
        const float BackBasketScale = .50f / .65f;
        Transform backBasketSocket, backBasketBone;
        Vector3 backBasketBoneOffset;
        Quaternion backBasketBoneRotation;

        public Transform BackBasketRoot { get; private set; }
        public Transform BackBasketRim { get; private set; }
        public Transform BackBasketAttachmentBone => backBasketBone;

        void InitializeBackBasket()
        {
            if (BackBasketRoot || !ModelRoot || !facingRoot) return;
            backBasketBone = FindEquipmentBone(ModelRoot, "mixamorig:Spine2");
            if (!backBasketBone) backBasketBone = FindEquipmentBone(ModelRoot, "mixamorig:Spine1");
            GameObject prefab = Resources.Load<GameObject>(BackBasketPath);
            if (!prefab || !backBasketBone) return;
            // Store the fitting in the animated chest's space. The socket itself
            // lives outside the squashed landing pose so the woven basket stays rigid.
            Vector3 contact = facingRoot.TransformPoint(new Vector3(0f, .62f, -.17f));
            Quaternion orientation = facingRoot.rotation * Quaternion.Euler(0f, 180f, 0f);
            backBasketBoneOffset = backBasketBone.InverseTransformPoint(contact);
            backBasketBoneRotation = Quaternion.Inverse(backBasketBone.rotation) * orientation;
            backBasketSocket = new GameObject("Traveler back basket socket").transform;
            backBasketSocket.SetParent(facingRoot, false);
            GameObject instance = Instantiate(prefab, backBasketSocket, false);
            instance.name = "Traveler back basket v2";
            BackBasketRoot = instance.transform;
            BackBasketRoot.localScale = Vector3.one * BackBasketScale;
            BackBasketRim = FindEquipmentBone(BackBasketRoot, "BasketRim");
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            UpdateBackBasket();
        }

        void UpdateBackBasket()
        {
            if (!backBasketSocket || !backBasketBone) return;
            backBasketSocket.gameObject.SetActive(IsModelActive);
            if (!IsModelActive) return;
            backBasketSocket.SetPositionAndRotation(backBasketBone.TransformPoint(backBasketBoneOffset),
                backBasketBone.rotation * backBasketBoneRotation);
        }
    }
}
