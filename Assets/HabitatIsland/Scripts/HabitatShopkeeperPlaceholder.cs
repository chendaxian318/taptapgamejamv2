using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HabitatIsland
{
    /// <summary>Persistent harbor presentation. It owns no quest, collision or interaction rules.</summary>
    [DisallowMultipleComponent]
    public sealed class HabitatShopkeeperPlaceholder : MonoBehaviour
    {
        public const string ResourcePath = "Models/Story/Shopkeeper";
        [Tooltip("Optional shopkeeper prefab. Empty loads Resources/Models/Story/Shopkeeper, then uses simple geometry.")]
        public GameObject prefab;
        public Transform PlaceholderRoot => actorRoot;
        public bool UsingArtistPrefab { get; private set; }
        HabitatGame owner;
        Transform boundModel, actorRoot, sign;
        GameObject appliedPrefab;
        Animator animator;
        bool wasInteracting, hasTalking, hasGreet;
        public Animator Animator => animator;
        readonly List<Material> materials = new List<Material>();

        public static HabitatShopkeeperPlaceholder Ensure(HabitatGame game)
        {
            if (!game) return null;
            var instance = game.GetComponent<HabitatShopkeeperPlaceholder>();
            if (!instance) instance = game.gameObject.AddComponent<HabitatShopkeeperPlaceholder>();
            instance.owner = game;
            instance.Refresh();
            return instance;
        }

        public void Refresh()
        {
            Transform model = owner && HabitatLevels.IsHomeHarbor(owner.State) && owner.View != null && owner.View.Harbor.Ready ? owner.View.Harbor.ModelTransform : null;
            if (!model) { if (actorRoot) Clear(); return; }
            if (actorRoot && model == boundModel && prefab == appliedPrefab) return;
            Clear(); boundModel = model; appliedPrefab = prefab;
            actorRoot = new GameObject("Harbor shopkeeper").transform;
            actorRoot.SetParent(model, false);
            actorRoot.gameObject.hideFlags = HideFlags.DontSave;
            // Offset from the interaction anchor so the actor stands beside the route.
            Vector3 local = HabitatImportedDockLayout.ShopApproach + new Vector3(-1.5f, 0, .5f);
            if (!HabitatImportedDockLayout.IsWalkable(local, .12f))
                local = HabitatImportedDockLayout.ShopApproach + new Vector3(0, 0, -.75f);
            Vector3 world = model.TransformPoint(local);
            Vector3 origin = model.TransformPoint(new Vector3(local.x, .45f, local.z));
            foreach (MeshCollider surface in model.GetComponentsInChildren<MeshCollider>())
                if (surface.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, .8f * Mathf.Abs(model.lossyScale.y)) && hit.normal.y > .5f)
                { world.y = hit.point.y + .02f; break; }
            actorRoot.position = world;
            actorRoot.localScale = Vector3.one / Mathf.Max(.001f, Mathf.Abs(model.lossyScale.y));
            Vector3 facing = model.TransformPoint(HabitatImportedDockLayout.ShopApproach) - world;
            facing.y = 0;
            actorRoot.rotation = Quaternion.LookRotation(facing.sqrMagnitude > .001f ? facing : model.forward);
            GameObject asset = prefab ? prefab : Resources.Load<GameObject>(ResourcePath);
            UsingArtistPrefab = asset;
            if (asset)
            {
                GameObject art = Instantiate(asset, actorRoot, false); art.name = "Shopkeeper art";
                // Match the current 75%-scale traveler while preserving the source prefab.
                art.transform.localScale *= .84f;
                foreach (Collider collider in art.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (Rigidbody body in art.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
                animator = art.GetComponentInChildren<Animator>(true);
                if (animator && animator.runtimeAnimatorController)
                {
                    animator.applyRootMotion = false;
                    foreach (AnimatorControllerParameter parameter in animator.parameters)
                    {
                        if (parameter.name == "Talking" && parameter.type == AnimatorControllerParameterType.Bool) hasTalking = true;
                        if (parameter.name == "Greet" && parameter.type == AnimatorControllerParameterType.Trigger) hasGreet = true;
                    }
                }
            }
            else BuildFallback();
            BuildSign();
        }

        void LateUpdate()
        {
            if (!owner) return;
            Refresh();
            bool nearby = owner.State != null && owner.State.onHarbor && owner.View.Harbor.NearShop(owner.State);
            bool talking = nearby && owner.Hud && owner.Hud.ShopkeeperSpeaking;
            bool interacting = talking || (nearby && owner.Hud && owner.Hud.ShopOpen);
            if (animator && animator.runtimeAnimatorController)
            {
                if (hasTalking) animator.SetBool("Talking", talking);
                if (hasGreet && interacting && !wasInteracting) animator.SetTrigger("Greet");
                animator.speed = owner.Story && owner.Story.IsPaused ? 0f : 1f;
            }
            wasInteracting = interacting;
            if (sign && owner.View && owner.View.GameCamera) sign.rotation = owner.View.GameCamera.transform.rotation;
        }

        void BuildFallback()
        {
            Material jacket = Material(new Color(.12f, .36f, .42f));
            Material skin = Material(new Color(.87f, .61f, .40f));
            Material apron = Material(new Color(.94f, .82f, .57f));
            Material boots = Material(new Color(.24f, .15f, .10f));
            Material hat = Material(new Color(.82f, .50f, .18f));
            Part("Left boot", PrimitiveType.Cube, new Vector3(-.14f, .10f, .05f), new Vector3(.23f, .20f, .34f), boots);
            Part("Right boot", PrimitiveType.Cube, new Vector3(.14f, .10f, .05f), new Vector3(.23f, .20f, .34f), boots);
            Part("Left leg", PrimitiveType.Capsule, new Vector3(-.14f, .36f, 0), new Vector3(.20f, .20f, .20f), jacket);
            Part("Right leg", PrimitiveType.Capsule, new Vector3(.14f, .36f, 0), new Vector3(.20f, .20f, .20f), jacket);
            Part("Coat", PrimitiveType.Capsule, new Vector3(0, .79f, 0), new Vector3(.52f, .32f, .34f), jacket);
            Part("Apron", PrimitiveType.Cube, new Vector3(0, .73f, .19f), new Vector3(.40f, .53f, .055f), apron);
            Part("Head", PrimitiveType.Sphere, new Vector3(0, 1.25f, .015f), new Vector3(.39f, .40f, .37f), skin);
            Part("Nose", PrimitiveType.Sphere, new Vector3(0, 1.24f, .205f), new Vector3(.10f, .09f, .10f), skin);
            Part("Left eye", PrimitiveType.Sphere, new Vector3(-.08f, 1.30f, .19f), new Vector3(.033f, .038f, .025f), boots);
            Part("Right eye", PrimitiveType.Sphere, new Vector3(.08f, 1.30f, .19f), new Vector3(.033f, .038f, .025f), boots);
            Part("Hat brim", PrimitiveType.Cylinder, new Vector3(0, 1.45f, 0), new Vector3(.63f, .04f, .57f), hat);
            Part("Hat crown", PrimitiveType.Cylinder, new Vector3(0, 1.53f, 0), new Vector3(.38f, .09f, .35f), hat);
            Transform left = Part("Left sleeve", PrimitiveType.Capsule, new Vector3(-.34f, .85f, 0), new Vector3(.19f, .24f, .19f), jacket);
            left.localRotation = Quaternion.Euler(0, 0, -15);
            Transform right = Part("Right sleeve", PrimitiveType.Capsule, new Vector3(.34f, .85f, 0), new Vector3(.19f, .24f, .19f), jacket);
            right.localRotation = Quaternion.Euler(0, 0, 15);
            Part("Left hand", PrimitiveType.Sphere, new Vector3(-.40f, .58f, .02f), new Vector3(.17f, .19f, .17f), skin);
            Part("Right hand", PrimitiveType.Sphere, new Vector3(.40f, .58f, .02f), new Vector3(.17f, .19f, .17f), skin);
        }

        void BuildSign()
        {
            sign = new GameObject("Shopkeeper sign").transform; sign.SetParent(actorRoot, false);
            sign.localPosition = new Vector3(0, UsingArtistPrefab ? 1.65f : 1.94f, 0);
            Material board = Material(new Color(.15f, .32f, .29f));
            GameObject backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Shop sign board"; backing.transform.SetParent(sign, false);
            backing.transform.localScale = new Vector3(1.00f, .34f, .04f);
            backing.GetComponent<Renderer>().sharedMaterial = board;
            Collider box = backing.GetComponent<Collider>(); box.enabled = false; Destroy(box);
            var text = new GameObject("Shopkeeper label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            text.transform.SetParent(sign, false); text.transform.localPosition = new Vector3(0, 0, -.03f);
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/HabitatChinese");
            if (font) text.font = font;
            text.text = "商店老板"; text.fontSize = 2.25f;
            text.color = new Color(1f, .91f, .65f); text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(.92f, .30f);
        }

        Material Material(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .12f);
            materials.Add(material); return material;
        }

        Transform Part(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape); part.name = name;
            part.transform.SetParent(actorRoot, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            return part.transform;
        }

        void Clear()
        {
            if (actorRoot) { actorRoot.gameObject.SetActive(false); Destroy(actorRoot.gameObject); }
            actorRoot = sign = boundModel = null; UsingArtistPrefab = false;
            animator = null; hasTalking = hasGreet = wasInteracting = false;
            foreach (Material material in materials) if (material) Destroy(material);
            materials.Clear();
        }
        void OnDisable() { Clear(); }
        void OnDestroy() { Clear(); }
    }
}
