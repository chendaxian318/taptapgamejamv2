using TMPro;
using UnityEngine;

namespace HabitatIsland
{
    public sealed partial class HabitatDockPrototype
    {
        // The imported harbor supplies buildings/boat; interaction remains home, shop and boat only.
        public static readonly Color[] PrototypeColors = {
            new Color32(172,190,151,255), new Color32(222,216,184,255), new Color32(177,139,96,255),
            new Color32(96,76,57,255), new Color32(240,231,208,255), new Color32(174,109,89,255),
            new Color32(88,133,114,255), new Color32(81,135,148,255), new Color32(247,246,232,255),
            new Color32(102,172,190,255), new Color32(249,210,123,255), new Color32(132,189,197,255),
            new Color32(87,132,98,255), new Color32(101,113,79,255)
        };

        public void SetSceneResources(Material[] materials, TMP_FontAsset font)
        { palette = materials; chineseFont = font; }

        public void BuildPrototypeWorld()
        {
            if (environmentRoot) return;
            if (!chineseFont) chineseFont = Resources.Load<TMP_FontAsset>("Fonts/HabitatChinese");
            if (palette == null || palette.Length != PrototypeColors.Length)
            {
                palette = new Material[PrototypeColors.Length];
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) shader = Shader.Find("Standard");
                for (int i = 0; i < palette.Length; i++)
                {
                    palette[i] = new Material(shader) { name = "Harbor prototype " + i, color = PrototypeColors[i] };
                    if (palette[i].HasProperty("_Smoothness")) palette[i].SetFloat("_Smoothness", .18f);
                }
            }
            environmentRoot = Child("Harbor • imported Hona model", transform);
            BuildImportedHarbor();
            playerRoot = Child("Player • MeshyTraveler", environmentRoot);
            playerRoot.position = BoatApproach;
            GameObject traveler = Resources.Load<GameObject>("Models/MeshyTraveler/Player");
            if (traveler)
            {
                editorTravelerPreview = Instantiate(traveler, playerRoot, false);
                editorTravelerPreview.name = "Editor traveler preview • runtime uses HabitatPlayerVisual";
                editorTravelerPreview.transform.localScale = traveler.transform.localScale * .75f;
                foreach (Collider collider in editorTravelerPreview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (Rigidbody body in editorTravelerPreview.GetComponentsInChildren<Rigidbody>(true))
                { body.isKinematic = true; body.useGravity = false; }
                foreach (Animator animator in editorTravelerPreview.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
            }
            gearRoot = Child("Carried props", playerRoot);
            BuildCatchAndNet();
            gulls = new Transform[3];
            for (int i = 0; i < gulls.Length; i++)
            {
                gulls[i] = Child("Seabird " + (i + 1) + " • prototype", environmentRoot);
                Part("Left wing", gulls[i], PrimitiveType.Cube, new Vector3(-.3f, 0, 0), new Vector3(.62f, .055f, .20f), 8);
                Part("Right wing", gulls[i], PrimitiveType.Cube, new Vector3(.3f, 0, 0), new Vector3(.62f, .055f, .20f), 8);
                Part("Body", gulls[i], PrimitiveType.Sphere, Vector3.zero, new Vector3(.24f, .17f, .42f), 8);
                Part("Beak", gulls[i], PrimitiveType.Cube, new Vector3(.18f, 0, 0), new Vector3(.16f, .07f, .09f), 10);
            }
            SampleGulls(.4f, false);
            worldLabels = new[] {
                WorldLabel("家", HabitatImportedDockLayout.HomeLabel),
                WorldLabel("商店", HabitatImportedDockLayout.ShopLabel),
                WorldLabel("小船", BoatArrival + Vector3.up * 1.6f)
            };
            netRoot.gameObject.SetActive(false);
            if (harborCamera) SetFreeCamera(true);
        }

        /// <summary>Update an existing saved prototype without rebuilding its scene or Timeline.</summary>
        public void ApplyPlayerPresentationDefaults()
        {
            walkSpeed = 2.5f;
            GameObject traveler = Resources.Load<GameObject>("Models/MeshyTraveler/Player");
            if (editorTravelerPreview && traveler)
                editorTravelerPreview.transform.localScale = traveler.transform.localScale * .75f;
        }

        void BuildImportedHarbor()
        {
            // This standalone demonstration keeps its original combined boat and boarding route.
            // The current island map uses Harbor plus the new independent wooden vessel.
            GameObject prefab = Resources.Load<GameObject>("Models/WoodenDock/HarborPrototypeLegacy");
            if (!prefab) throw new System.InvalidOperationException("Prepare the imported wooden harbor prefab first.");
            GameObject model = Instantiate(prefab, environmentRoot, false);
            model.name = "Hona wooden harbor • original textured model";
            harborSurface = model.GetComponentInChildren<MeshCollider>();
            // The merged source includes its own buildings, boat and furniture.
            // Only add a boarding plank over the source boat-to-pier water gap.
            Vector3 a = HabitatImportedDockLayout.BoardingStart, b = HabitatImportedDockLayout.BoardingEnd;
            Transform gangway = Part("Boarding plank", environmentRoot, PrimitiveType.Cube,
                (a + b) * .5f - Vector3.up * .035f, new Vector3(HabitatImportedDockLayout.BoardingWidth, .07f, Vector3.Distance(a, b)), 2);
            gangway.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            Part("Sea", environmentRoot, PrimitiveType.Cube, new Vector3(0, -1.45f, 0), new Vector3(180, .3f, 180), 9);
            for (int i = 0; i < 18; i++)
                Part("Sea glint", environmentRoot, PrimitiveType.Cube,
                    new Vector3(-17 + (i * 7 % 34), -1.29f, -12 + (i * 5 % 24)), new Vector3(.8f + (i % 3) * .3f, .012f, .055f), 8);
            Vector3 window = HabitatImportedDockLayout.WindowTarget;
            morningPatch = null; // Actual sunlight illuminates the imported window and sill.
            windowLight = new GameObject("Morning window light", typeof(Light)).GetComponent<Light>();
            windowLight.transform.SetParent(environmentRoot, false);
            windowLight.transform.position = window + HabitatImportedDockLayout.WindowForward * .8f;
            windowLight.type = LightType.Point; windowLight.range = 5;
            windowLight.color = new Color(1f, .80f, .46f); windowLight.intensity = 0;
            boatRoot = null; // The source boat is part of the merged static mesh.
        }

        /// <summary>Explicit editor migration after installing the imported harbor, preserving Timeline assets.</summary>
        public void RebuildImportedWorld()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play before replacing scene geometry.");
            if (environmentRoot) DestroyImmediate(environmentRoot.gameObject);
            environmentRoot = null;
            BuildPrototypeWorld();
        }
        void BuildCatchAndNet()
        {
            BindCatchBasket();
            netRoot = Child("Landing net • stows during Timeline", gearRoot);
            netRoot.localPosition = new Vector3(.55f, .75f, 0);
            if (ApplyLandingNetArt()) return;
            Part("Net handle", netRoot, PrimitiveType.Cylinder, new Vector3(0, .27f, 0), new Vector3(.045f, .5f, .045f), 2);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8;
                var ring = Part("Net hoop", netRoot, PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(a) * .32f, 1.02f + Mathf.Sin(a) * .32f, 0), new Vector3(.14f, .025f, .025f), 3);
                ring.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90);
            }
            for (int i = -2; i <= 2; i++)
            {
                float span = Mathf.Sqrt(.32f * .32f - i * i * .01f) * 2;
                Part("Net cord", netRoot, PrimitiveType.Cube, new Vector3(i * .1f, 1.02f, .012f), new Vector3(.012f, span, .012f), 8);
                Part("Net cord", netRoot, PrimitiveType.Cube, new Vector3(0, 1.02f + i * .1f, .012f), new Vector3(span, .012f, .012f), 8);
            }
        }

        // Replace only the carried prop in older saved prototype scenes; retain the
        // socket, visibility and Timeline rotations authored on netRoot.
        bool ApplyLandingNetArt()
        {
            if (!netRoot) return false;
            Transform existing = netRoot.Find("Landing net art");
            if (existing)
            {
                var grip = existing.GetComponent<HabitatLandingNetGrip>();
                if (!grip) grip = existing.gameObject.AddComponent<HabitatLandingNetGrip>();
                grip.Bind(playerVisual);
                return true;
            }
            GameObject prefab = Resources.Load<GameObject>("Models/Story/LandingNet");
            if (!prefab) return false;
            for (int i = netRoot.childCount - 1; i >= 0; i--)
            {
                GameObject old = netRoot.GetChild(i).gameObject;
                old.SetActive(false);
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
            GameObject art = Instantiate(prefab, netRoot, false);
            art.name = "Landing net art";
            art.AddComponent<HabitatLandingNetGrip>().Bind(playerVisual);
            foreach (Collider collider in art.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in art.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; }
            return true;
        }

        void BindCatchBasket()
        {
            Transform rim = playerVisual ? playerVisual.BackBasketRim : null;
            if (catchRoot && ((!rim && catchRoot.parent == gearRoot) || (rim && catchRoot.parent == rim))) return;
            // Saved prototype scenes still contain the original geometric basket.
            // Retire that temporary object when the runtime traveler supplies its own basket.
            ReleaseCatchContents();
            catchRoot = Child(rim ? "Opening catch • permanent basket contents" : "Full catch basket • fallback", rim ? rim : gearRoot);
            if (rim)
            {
                Vector3 scale = rim.lossyScale;
                catchRoot.localScale = new Vector3(.75f / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                    .75f / Mathf.Max(.0001f, Mathf.Abs(scale.y)), .75f / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            }
            else
            {
                catchRoot.localPosition = new Vector3(0, .88f, -.37f);
                Part("Basket body", catchRoot, PrimitiveType.Cube, Vector3.zero, new Vector3(.72f, .53f, .50f), 2);
                Part("Basket rim", catchRoot, PrimitiveType.Cube, new Vector3(0, .28f, 0), new Vector3(.79f, .07f, .56f), 3);
            }
            for (int i = 0; i < 4; i++)
            {
                var fish = Part("Catch fish " + i, catchRoot, PrimitiveType.Capsule,
                    new Vector3(-.23f + i * .15f, (rim ? .025f : .34f) + i % 2 * .04f, 0),
                    new Vector3(.12f, .10f, .31f), 11);
                fish.localRotation = Quaternion.Euler(0, 20 - i * 13, 0);
            }
        }

        void ReleaseCatchContents()
        {
            if (!catchRoot) return;
            // catchRoot owns only this sequence's contents (or its resource-missing fallback).
            // Never deactivate or destroy the traveler's persistent basket or rim anchor.
            catchRoot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(catchRoot.gameObject);
            else DestroyImmediate(catchRoot.gameObject);
            catchRoot = null;
        }

        Transform WorldLabel(string caption, Vector3 position)
        {
            var label = new GameObject(caption + " • landmark label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(environmentRoot, false); label.transform.localPosition = position;
            label.font = chineseFont; label.text = caption; label.fontSize = 5;
            label.color = new Color32(34, 69, 62, 255); label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(4, .8f);
            label.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (harborCamera) label.transform.rotation = harborCamera.transform.rotation;
            return label.transform;
        }

        static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); return child;
        }

        Transform Part(string name, Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, int color)
        {
            GameObject item = GameObject.CreatePrimitive(shape); item.name = name;
            item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = palette[color];
            Collider collider = item.GetComponent<Collider>();
            if (collider) { collider.enabled = false; if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
            return item.transform;
        }
    }
}
