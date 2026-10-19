using UnityEngine;

namespace HabitatIsland
{
    public enum HabitatPlayerMotion { Walk, Push, Pull, Climb, Fall, Ride }

    // Presentation only. Grid and harbor logic remain authoritative for destinations.
    [DisallowMultipleComponent]
    public sealed partial class HabitatPlayerVisual : MonoBehaviour
    {
        const string ModelPath = "Models/MeshyTraveler/Player";
        public const float CharacterScale = .75f;
        public const float WalkSpeed = 2.5f;
        public const float HandlingSpeed = 1.5f;
        const float CarryHeight = 1.80f * CharacterScale;
        const float IdleDelay = .10f;
        static readonly int MoveSpeedId = UnityEngine.Animator.StringToHash("MoveSpeed");

        Transform facingRoot, poseRoot, leftArm, leftForearm, leftHand, rightArm, rightForearm, rightHand;
        Vector3 previousPosition;
        float stillTime, moveSpeed, handlingBlend, fallBlend, landingTime, requestedUntil;
        bool hasMoveSpeed, falling;
        HabitatPlayerMotion motion = HabitatPlayerMotion.Walk;

        public Animator Animator { get; private set; }
        public Transform ModelRoot { get; private set; }
        public Transform CarryAnchor { get; private set; }
        public bool IsModelActive => ModelRoot && ModelRoot.gameObject.activeInHierarchy;
        public float MoveSpeed => moveSpeed;
        public HabitatPlayerMotion Motion => motion;
        public float TravelSpeed => motion == HabitatPlayerMotion.Push || motion == HabitatPlayerMotion.Pull || motion == HabitatPlayerMotion.Ride ? HandlingSpeed : WalkSpeed;
        public float HandlingPoseWeight => handlingBlend;
        public float FallPoseWeight => fallBlend;
        public bool IsFalling => falling;
        public bool IsLanding => landingTime > 0f;

        public bool Initialize()
        {
            if (ModelRoot) return true;
            GameObject prefab = Resources.Load<GameObject>(ModelPath);
            if (!prefab) return false;
            facingRoot = new GameObject("Traveler facing").transform;
            facingRoot.SetParent(transform, false);
            poseRoot = new GameObject("Traveler action pose").transform;
            poseRoot.SetParent(facingRoot, false);
            GameObject instance = Instantiate(prefab, poseRoot, false);
            instance.name = "Meshy traveler";
            ModelRoot = instance.transform;
            ModelRoot.localScale *= CharacterScale;
            CarryAnchor = new GameObject("Traveler carry anchor").transform;
            CarryAnchor.SetParent(transform, false);
            CarryAnchor.localPosition = Vector3.up * CarryHeight;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; }
            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
            foreach (Transform bone in instance.GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "mixamorig:LeftArm": leftArm = bone; break;
                    case "mixamorig:LeftForeArm": leftForearm = bone; break;
                    case "mixamorig:LeftHand": leftHand = bone; break;
                    case "mixamorig:RightArm": rightArm = bone; break;
                    case "mixamorig:RightForeArm": rightForearm = bone; break;
                    case "mixamorig:RightHand": rightHand = bone; break;
                }
            }
            Animator = instance.GetComponentInChildren<Animator>(true);
            if (Animator)
            {
                Animator.applyRootMotion = false;
                Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (Animator.runtimeAnimatorController)
                    foreach (AnimatorControllerParameter parameter in Animator.parameters)
                        if (parameter.nameHash == MoveSpeedId && parameter.type == AnimatorControllerParameterType.Float)
                            hasMoveSpeed = true;
            }
            ResetVisual(Vector3.forward);
            InitializeBackBasket();
            return true;
        }

        public void SetMotion(HabitatPlayerMotion value)
        {
            motion = value;
            if (value != HabitatPlayerMotion.Walk) HideEquipment();
            requestedUntil = Time.unscaledTime + .12f;
            if(value==HabitatPlayerMotion.Ride)
            {
                moveSpeed=handlingBlend=0f;
                if(Animator&&hasMoveSpeed)Animator.SetFloat(MoveSpeedId,0f);
            }
        }

        public void NotifyFall(float dropHeight)
        {
            if (dropHeight < .08f) return;
            falling = true;
            landingTime = 0f;
            SetMotion(HabitatPlayerMotion.Fall);
        }

        public void NotifyLanded()
        {
            if (!falling) return;
            falling = false;
            landingTime = .32f;
            motion = HabitatPlayerMotion.Walk;
        }

        public void ResetVisual(Vector3 facing)
        {
            EndRecoveryPose();
            HideEquipment();
            previousPosition = transform.position;
            stillTime = IdleDelay;
            moveSpeed = handlingBlend = fallBlend = landingTime = 0f;
            falling = false; motion = HabitatPlayerMotion.Walk;
            if (poseRoot) { poseRoot.localPosition = Vector3.zero; poseRoot.localRotation = Quaternion.identity; poseRoot.localScale = Vector3.one; }
            facing.y = 0f;
            if (facingRoot && facing.sqrMagnitude > .001f)
                facingRoot.rotation = Quaternion.LookRotation(facing, Vector3.up);
            if (!Animator || !Animator.runtimeAnimatorController) return;
            Animator.Rebind();
            if (hasMoveSpeed) Animator.SetFloat(MoveSpeedId, 0f);
            Animator.Play("Locomotion", 0, 0f);
            Animator.Update(0f);
        }

        public void Render(float unscaledDeltaTime, Vector3 facing)
        {
            if (!IsModelActive) return;
            float dt = float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime)
                ? 0f : Mathf.Clamp(unscaledDeltaTime, 0f, .08f);
            Vector3 displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            displacement.y = 0f;
            facing.y = 0f;
            if (TryGetEquipmentFacing(out Vector3 toolFacing)) facing = toolFacing;
            if (facing.sqrMagnitude > .001f)
                facingRoot.rotation = Quaternion.Slerp(facingRoot.rotation,
                    Quaternion.LookRotation(facing, Vector3.up), 1f - Mathf.Exp(-dt * 18f));
            if (dt <= .00001f) return;
            float speed = Mathf.Min(WalkSpeed, displacement.magnitude / dt);
            if (speed > .05f) stillTime = 0f; else stillTime += dt;
            if (!falling && stillTime > IdleDelay && Time.unscaledTime > requestedUntil)
                motion = HabitatPlayerMotion.Walk;
            // The new travel speed uses Walking rather than the old Walk/Run mix.
            float targetSpeed = stillTime >= IdleDelay || falling || motion == HabitatPlayerMotion.Ride ? 0f : 1.6f * Mathf.Clamp01(speed / WalkSpeed);
            moveSpeed = Mathf.MoveTowards(moveSpeed, targetSpeed, dt * 14f);
            if (Animator && hasMoveSpeed) Animator.SetFloat(MoveSpeedId, moveSpeed);
            bool handling = motion == HabitatPlayerMotion.Push || motion == HabitatPlayerMotion.Pull;
            handlingBlend = Mathf.MoveTowards(handlingBlend, handling ? 1f : 0f, dt * 9f);
            fallBlend = Mathf.MoveTowards(fallBlend, falling ? 1f : 0f, dt * 12f);
            landingTime = Mathf.Max(0f, landingTime - dt);
        }

        void LateUpdate()
        {
            if (!IsModelActive || !poseRoot) return;
            float crouch = landingTime > 0f ? Mathf.Sin((1f - landingTime / .32f) * Mathf.PI) : 0f;
            float lean = (motion == HabitatPlayerMotion.Pull ? -9f : 10f) * handlingBlend;
            poseRoot.localRotation = Quaternion.Euler(lean - 5f * fallBlend + 9f * crouch, 0f, 0f);
            poseRoot.localPosition = Vector3.up * (-.025f * handlingBlend - .035f * crouch);
            poseRoot.localScale = new Vector3(1f + .025f * crouch, 1f - .10f * crouch, 1f + .025f * crouch);
            CarryAnchor.localPosition = Vector3.up * (CarryHeight - .1f * crouch);
            if (handlingBlend > .001f)
            {
                float reach = motion == HabitatPlayerMotion.Pull ? .27f : .36f;
                PoseArm(leftArm, leftForearm, leftHand, new Vector3(.23f, .63f, reach), new Vector3(.45f, .45f, .12f), handlingBlend);
                PoseArm(rightArm, rightForearm, rightHand, new Vector3(-.23f, .63f, reach), new Vector3(-.45f, .45f, .12f), handlingBlend);
            }
            float balance = Mathf.Max(fallBlend, crouch * .65f);
            if (balance > .001f)
            {
                PoseArm(leftArm, leftForearm, leftHand, new Vector3(.44f, .69f, .04f), new Vector3(.40f, .52f, -.10f), balance);
                PoseArm(rightArm, rightForearm, rightHand, new Vector3(-.44f, .69f, .04f), new Vector3(-.40f, .52f, -.10f), balance);
            }
            PoseEquipment();
            PoseRecoveryNet();
            UpdateBackBasket();
        }

        // Solve in world space because the imported rig has nonstandard bone axes.
        // Animator runs first; its original walk animation remains on the legs.
        void PoseArm(Transform upper, Transform lower, Transform hand, Vector3 targetLocal, Vector3 hintLocal, float weight)
        {
            if (!upper || !lower || !hand) return;
            float side = facingRoot.InverseTransformPoint(upper.position).x >= 0f ? 1f : -1f;
            targetLocal.x = Mathf.Abs(targetLocal.x) * side;
            hintLocal.x = Mathf.Abs(hintLocal.x) * side;
            Vector3 target = facingRoot.TransformPoint(targetLocal);
            PoseArmWorld(upper, lower, hand, target, facingRoot.TransformPoint(hintLocal), weight);
        }

        void PoseArmWorld(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 hintWorld, float weight)
        {
            if (!upper || !lower || !hand) return;
            Vector3 shoulder = upper.position;
            float a = Vector3.Distance(shoulder, lower.position), b = Vector3.Distance(lower.position, hand.position);
            if (a < .001f || b < .001f) return;
            Vector3 toTarget = target - shoulder;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            Vector3 axis = toTarget.normalized;
            Vector3 hint = hintWorld - shoulder;
            Vector3 bend = Vector3.ProjectOnPlane(hint, axis).normalized;
            if (bend.sqrMagnitude < .1f) bend = facingRoot.up;
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            Vector3 elbow = shoulder + axis * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Quaternion upperTarget = Quaternion.FromToRotation(lower.position - shoulder, elbow - shoulder) * upper.rotation;
            upper.rotation = Quaternion.Slerp(upper.rotation, upperTarget, weight);
            Quaternion lowerTarget = Quaternion.FromToRotation(hand.position - lower.position, shoulder + axis * distance - lower.position) * lower.rotation;
            lower.rotation = Quaternion.Slerp(lower.rotation, lowerTarget, weight);
        }
    }
}
