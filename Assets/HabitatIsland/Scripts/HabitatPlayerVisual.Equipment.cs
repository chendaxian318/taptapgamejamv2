using UnityEngine;

namespace HabitatIsland
{
    public sealed partial class HabitatPlayerVisual
    {
        enum HandEquipment { None, Rod, Shovel, Magnet }
        sealed class EquipmentInstance
        {
            public Transform root, primary, secondary, tip;
            public HabitatEquipmentAnimation animation;
        }

        const float RodLength = 1.30f, ShovelLength = .90f, DigSeconds = .72f;
        EquipmentInstance rodEquipment, shovelEquipment, magnetEquipment, activeEquipment;
        HandEquipment equipped;
        Transform equipmentSocket, rightPalmBone, leftPalmBone;
        LineRenderer fishingLine;
        Material fishingLineMaterial;
        bool triedRod, triedShovel, triedMagnet, equipmentBlocked, equipmentFishing, hasEquipmentTarget;
        int equipmentTool = -1, equipmentActionTool = -1, equipmentTickFrame = -1;
        string equipmentAction = "";
        float equipmentActionTime;
        Vector3 equipmentTarget;

        public string EquippedTool => equipped.ToString();
        public string ActiveEquipmentKind => EquippedTool;
        public string EquipmentAction => equipmentAction;
        public Transform EquipmentRoot => activeEquipment != null ? activeEquipment.root : null;
        public Transform EquipmentSocket => equipmentSocket;
        public Transform PrimaryEquipmentGrip => activeEquipment != null ? activeEquipment.primary : null;
        public Transform SecondaryEquipmentGrip => activeEquipment != null ? activeEquipment.secondary : null;
        public Vector3 RightPalmPosition => rightHand ? PalmPosition(rightHand, rightPalmBone) : transform.position;
        public Vector3 LeftPalmPosition => leftHand ? PalmPosition(leftHand, leftPalmBone) : transform.position;
        public float PrimaryGripError => PrimaryEquipmentGrip ? Vector3.Distance(PrimaryEquipmentGrip.position, RightPalmPosition) : 0f;
        public float SecondaryGripError => SecondaryEquipmentGrip ? Vector3.Distance(SecondaryEquipmentGrip.position, LeftPalmPosition) : 0f;
        public HabitatEquipmentAnimation EquipmentAnimation => activeEquipment != null ? activeEquipment.animation : null;
        public LineRenderer FishingLine => fishingLine;

        // Called by the view, including while the ecology simulation is paused for fishing.
        // Inventory, selected tool and gameplay transactions remain owned by HabitatGame.
        public void RefreshEquipment(HabitatState state, int selectedTool, bool hide, bool fishingOpen)
        {
            if (selectedTool != equipmentTool) CancelEquipmentAction();
            equipmentTool = selectedTool;
            equipmentFishing = fishingOpen || fishingPoseStage > 0;
            bool shovel = selectedTool == 1 || selectedTool==5 || equipmentAction == "Dig" && equipmentActionTool == selectedTool;
            equipmentBlocked = hide || state == null || !IsModelActive || !rightHand
                || motion != HabitatPlayerMotion.Walk || falling || landingTime > 0f
                || state.carryingAnimal >= 0
                || state.carryingProp != PropKind.None && !(shovel && state.carryingProp == PropKind.Climb);
            if (equipmentBlocked) { HideEquipment(); return; }
            HandEquipment desired = shovel ? HandEquipment.Shovel
                : selectedTool==6 ? HandEquipment.Magnet : (selectedTool == 2 || selectedTool == 4 && equipmentFishing) && state.rod ? HandEquipment.Rod : HandEquipment.None;
            if (desired == HandEquipment.None) { HideEquipment(); return; }
            ShowEquipment(desired);
            if (activeEquipment == null) return;
            if (equipmentTickFrame == Time.frameCount) return;
            equipmentTickFrame = Time.frameCount;
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, .1f);
            if (activeEquipment.animation) activeEquipment.animation.Tick(dt);
            if (!string.IsNullOrEmpty(equipmentAction))
            {
                equipmentActionTime += dt;
                bool done = equipmentAction == "Dig" ? equipmentActionTime >= DigSeconds
                    : activeEquipment.animation && activeEquipment.animation.Finished;
                if (done) FinishEquipmentAction(equipmentFishing);
            }
        }

        public void PlayToolAction(string action)
        {
            if (equipmentBlocked || !IsModelActive || !rightHand) return;
            HandEquipment desired = action == "Dig" ? HandEquipment.Shovel
                : action == "Cast" || action == "Hook_Pull" ? HandEquipment.Rod : HandEquipment.None;
            if (desired == HandEquipment.None) return;
            ShowEquipment(desired);
            if (activeEquipment == null) return;
            if (desired == HandEquipment.Rod && (!activeEquipment.animation
                || !activeEquipment.animation.Play(action, false, 1f, true))) return;
            equipmentAction = action; equipmentActionTime = 0f; equipmentActionTool = equipmentTool;
        }

        public void PlayToolAction(string action, Vector3 worldTarget)
        {
            PlayToolAction(action);
            if (equipmentAction != action || !float.IsFinite(worldTarget.x)
                || !float.IsFinite(worldTarget.y) || !float.IsFinite(worldTarget.z)) return;
            equipmentTarget = worldTarget; hasEquipmentTarget = true;
        }

        public void CancelEquipmentAction()
        {
            fishingPoseStage = 0;
            equipmentFishing = false;
            FinishEquipmentAction(false);
        }

        void FinishEquipmentAction(bool keepFishingTarget)
        {
            equipmentAction = ""; equipmentActionTime = 0f; equipmentActionTool = -1;
            if (!keepFishingTarget) hasEquipmentTarget = false;
            if (fishingLine && !keepFishingTarget) fishingLine.enabled = false;
            if (rodEquipment != null && rodEquipment.animation)
                rodEquipment.animation.Play("Idle_Sway", true);
        }

        void HideEquipment()
        {
            equipmentFishing = false; hasEquipmentTarget = false;
            CancelEquipmentAction();
            if (rodEquipment != null) rodEquipment.root.gameObject.SetActive(false);
            if (shovelEquipment != null) shovelEquipment.root.gameObject.SetActive(false);
            if (magnetEquipment != null) magnetEquipment.root.gameObject.SetActive(false);
            equipped = HandEquipment.None; activeEquipment = null;
        }

        void ShowEquipment(HandEquipment desired)
        {
            if (!equipmentSocket)
            {
                equipmentSocket = new GameObject("Traveler right palm tool socket").transform;
                equipmentSocket.SetParent(rightHand, false);
                rightPalmBone = FindEquipmentBone(rightHand, "mixamorig:RightHandMiddle1");
                leftPalmBone = FindEquipmentBone(leftHand, "mixamorig:LeftHandMiddle1");
            }
            // Cancel the inherited character import scale, then size the tool in world units.
            Vector3 scale = rightHand.lossyScale;
            equipmentSocket.localScale = new Vector3(1f / Mathf.Max(.00001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(.00001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.00001f, Mathf.Abs(scale.z)));
            if (desired == HandEquipment.Rod && !triedRod)
            { triedRod = true; rodEquipment = CreateEquipment("Models/FishingRod/Rod", RodLength, "Grip_CTRL"); }
            if (desired == HandEquipment.Shovel && !triedShovel)
            { triedShovel = true; shovelEquipment = CreateEquipment("Models/Shovel/Shovel", ShovelLength, "Grip_Primary"); }
            if(desired==HandEquipment.Magnet&&!triedMagnet)
            {triedMagnet=true;magnetEquipment=CreateEquipment("Models/Magnet/Magnet",.42f,"Grip_Primary");}
            EquipmentInstance next = desired == HandEquipment.Rod ? rodEquipment : desired==HandEquipment.Magnet ? magnetEquipment : shovelEquipment;
            if (activeEquipment != next && activeEquipment != null) activeEquipment.root.gameObject.SetActive(false);
            activeEquipment = next; equipped = next == null ? HandEquipment.None : desired;
            if (next == null) return;
            next.root.gameObject.SetActive(true);
            if (desired == HandEquipment.Rod && next.animation && string.IsNullOrEmpty(equipmentAction))
                next.animation.Play("Idle_Sway", true);
        }

        EquipmentInstance CreateEquipment(string path, float worldLength, string gripName)
        {
            GameObject prefab = Resources.Load<GameObject>(path);
            if (!prefab) return null;
            GameObject instance = Instantiate(prefab, equipmentSocket, false);
            instance.name = path.EndsWith("Rod") ? "Held fishing rod" : path.EndsWith("Magnet")?"Held magnet":"Held shovel";
            Transform root = instance.transform;
            root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;
            var result = new EquipmentInstance { root = root,
                animation = instance.GetComponentInChildren<HabitatEquipmentAnimation>(true),
                primary = FindEquipmentBone(root, gripName), secondary = FindEquipmentBone(root, "Grip_Secondary"),
                tip = FindEquipmentBone(root, "Rod_Tip_Attachment") };
            if (result.animation) { result.animation.Play("Idle_Sway", true); result.animation.Tick(0f); }
            Bounds bounds = EquipmentBounds(root);
            root.localScale = Vector3.one * (worldLength / Mathf.Max(.001f, bounds.size.y));
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; }
            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            return result;
        }

        static Bounds EquipmentBounds(Transform root)
        {
            Bounds result = default; bool any = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds local = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(point, Vector3.zero); any = true; } else result.Encapsulate(point);
                }
            }
            return result;
        }

        static Transform FindEquipmentBone(Transform root, string name)
        {
            if (!root) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }

        bool TryGetEquipmentFacing(out Vector3 facing)
        {
            facing = (fishingPoseStage>0 ? fishingFacingTarget : equipmentTarget) - transform.position; facing.y = 0f;
            return activeEquipment != null && hasEquipmentTarget && (equipmentFishing || equipmentAction.Length > 0)
                && facing.sqrMagnitude > .0001f;
        }

        static Vector3 PalmPosition(Transform hand, Transform fingerBase)
            => fingerBase ? Vector3.Lerp(hand.position, fingerBase.position, .65f) : hand.position;

        void PoseEquipment()
        {
            if (activeEquipment == null || !activeEquipment.root.gameObject.activeSelf || !rightHand || !leftHand || !rightArm) return;
            if (motion != HabitatPlayerMotion.Walk || falling || landingTime > 0f) { HideEquipment(); return; }
            float dig = equipmentAction == "Dig" ? Mathf.Sin(Mathf.Clamp01(equipmentActionTime / DigSeconds) * Mathf.PI) : 0f;
            float side = facingRoot.InverseTransformPoint(rightArm.position).x >= 0f ? 1f : -1f;
            Vector3 rightTarget = equipped == HandEquipment.Rod ? new Vector3(side * .12f, .78f, .18f)
                : new Vector3(side * .08f, .82f - dig * .02f, .10f + dig * .03f);
            float rodPitch=37f;
            if(equipped==HandEquipment.Rod)ApplyFishingBodyPose(side,ref rightTarget,ref rodPitch);
            PoseArm(rightArm, rightForearm, rightHand, rightTarget, new Vector3(side * .42f, .48f, .05f), 1f);
            Quaternion orientation = facingRoot.rotation * (equipped == HandEquipment.Rod
                ? Quaternion.Euler(rodPitch, 0f, 0f) : equipped==HandEquipment.Magnet ? Quaternion.Euler(-90f,0,0):Quaternion.Euler(-24f - dig * 18f, 0f, -side * 48f));
            equipmentSocket.SetPositionAndRotation(PalmPosition(rightHand, rightPalmBone), orientation);
            // Grip bones, rather than the prefab bounds center, are the attachment pivot.
            if (activeEquipment.primary)
                equipmentSocket.position += PalmPosition(rightHand, rightPalmBone) - activeEquipment.primary.position;
            if(equipped==HandEquipment.Magnet)return;
            Vector3 leftGrip = activeEquipment.secondary ? activeEquipment.secondary.position
                : equipmentSocket.position + orientation * new Vector3(0, -.10f, 0);
            // The existing solver positions the wrist; offset it so the palm lands on the grip.
            // Refine twice because bending the forearm rotates the wrist-to-palm offset.
            for (int i = 0; i < 2; i++)
            {
                Vector3 reelOffset = fishingPoseStage==2 ? facingRoot.right*(Mathf.Sin(fishingPoseTime*10f)*.055f)+facingRoot.up*(Mathf.Cos(fishingPoseTime*10f)*.055f) : Vector3.zero;
                Vector3 leftTarget = leftGrip + reelOffset - (PalmPosition(leftHand, leftPalmBone) - leftHand.position);
                PoseArmWorld(leftArm, leftForearm, leftHand, leftTarget,
                    facingRoot.TransformPoint(new Vector3(-side * .40f, .45f, .12f)), 1f);
            }
            UpdateFishingLine();
        }

        void UpdateFishingLine()
        {
            bool visible = equipped == HandEquipment.Rod && activeEquipment.tip && hasEquipmentTarget
                && (equipmentFishing || equipmentAction == "Cast" || equipmentAction == "Hook_Pull");
            if (!visible) { if (fishingLine) fishingLine.enabled = false; return; }
            if (!fishingLine)
            {
                var lineObject = new GameObject("Fishing rod visual line"); lineObject.transform.SetParent(transform, false);
                fishingLine = lineObject.AddComponent<LineRenderer>(); fishingLine.useWorldSpace = true;
                fishingLine.positionCount = 9; fishingLine.widthMultiplier = .016f;
                fishingLine.numCapVertices = 2; fishingLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fishingLine.receiveShadows = false;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader)
                {
                    fishingLineMaterial = new Material(shader) { name = "Fishing line runtime" };
                    fishingLineMaterial.color = new Color(.87f, .80f, .57f, 1f);
                    fishingLine.sharedMaterial = fishingLineMaterial;
                }
            }
            fishingLine.enabled = true;
            Vector3 start = activeEquipment.tip.position;
            Vector3 end=equipmentTarget;
            if(fishingPoseStage==1){float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.85f,fishingPoseTime));end=Vector3.Lerp(start,equipmentTarget,travel)+Vector3.up*Mathf.Sin(travel*Mathf.PI)*.5f;}
            for (int i = 0; i < fishingLine.positionCount; i++)
            {
                float t = (float)i / (fishingLine.positionCount - 1);
                fishingLine.SetPosition(i, Vector3.Lerp(start, end, t) - Vector3.up * (.12f * 4f * t * (1f - t)));
            }
        }

        void OnDisable() { HideEquipment(); }
        void OnDestroy() { if (fishingLineMaterial) Destroy(fishingLineMaterial); }
    }
}
