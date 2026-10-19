using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland
{
    // A finished slatted wooden chest built from reusable meshes and the delivered
    // basket's wood materials. It does not modify its source model or materials.
    // Progress is exclusively driven by the gameplay transaction, never by Update.
    public sealed class HabitatChestVisual : MonoBehaviour
    {
        Transform lid, glowRoot;
        Light contentLight;
        Material glowMaterial;
        Mesh plankMesh, diamondMesh, glowMesh;
        readonly List<Transform> motes = new List<Transform>();
        public float OpeningProgress { get; private set; }
        public float LidAngle => lid ? Mathf.DeltaAngle(0, lid.localEulerAngles.x) : 0;
        public bool UsesDeliveredWood { get; private set; }

        public void Build(Material woodFallback, Material edgeFallback, Material metal, Material interior)
        {
            if (lid) return;
            Material wood = Resources.Load<Material>("Models/BackBasket/Basket_Backboard_Wood");
            Material edge = Resources.Load<Material>("Models/BackBasket/Basket_Backboard_Edges");
            Material deliveredMetal = Resources.Load<Material>("Models/BackBasket/Basket_Attachments_Gold");
            UsesDeliveredWood = wood && edge;
            wood = wood ? wood : woodFallback; edge = edge ? edge : edgeFallback;
            if (deliveredMetal) metal = deliveredMetal;
            plankMesh = RoundedUnitBox(); diamondMesh = Diamond(); glowMesh = GlowCone();
            Part("Inset floor", transform, new Vector3(0, .065f, 0), new Vector3(.60f, .07f, .43f), interior);
            // Individual bevelled slats and recessed seams, rather than a single solid cube.
            for (int layer = 0; layer < 3; layer++)
            {
                float y = .115f + layer * .105f;
                Part("Front wood slat " + layer, transform, new Vector3(0, y, -.242f), new Vector3(.66f, .098f, .055f), wood);
                Part("Rear wood slat " + layer, transform, new Vector3(0, y, .242f), new Vector3(.66f, .098f, .055f), wood);
                foreach (int sign in new[] { -1, 1 })
                    Part("Side wood slat " + sign + "/" + layer, transform, new Vector3(sign * .322f, y, 0), new Vector3(.055f, .098f, .45f), wood);
            }
            foreach (int sign in new[] { -1, 1 })
            {
                Part("Front bottom rail " + sign, transform, new Vector3(0, .035f, sign * .235f), new Vector3(.72f, .055f, .065f), edge);
                Part("Side bottom rail " + sign, transform, new Vector3(sign * .33f, .035f, 0), new Vector3(.065f, .055f, .50f), edge);
                Part("Upper rim front " + sign, transform, new Vector3(0, .365f, sign * .242f), new Vector3(.72f, .038f, .065f), edge);
                Part("Upper rim side " + sign, transform, new Vector3(sign * .333f, .365f, 0), new Vector3(.055f, .038f, .48f), edge);
                Part("Hinge bracket " + sign, transform, new Vector3(sign * .225f, .35f, .277f), new Vector3(.08f, .075f, .035f), metal);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Corner band " + sign + "/" + side, transform, new Vector3(sign * .302f, .205f, side * .277f), new Vector3(.047f, .31f, .025f), metal);
                    for (int rivet = 0; rivet < 3; rivet++)
                        Part("Rivet", transform, new Vector3(sign * .302f, .09f + rivet * .11f, side * .294f), Vector3.one * .023f, metal);
                }
            }
            Part("Lock keeper", transform, new Vector3(0, .282f, -.285f), new Vector3(.108f, .105f, .035f), metal);
            Part("Keyhole", transform, new Vector3(0, .287f, -.307f), new Vector3(.018f, .039f, .009f), interior);
            lid = Child("Rear hinge / movable lid", transform); lid.localPosition = new Vector3(0, .385f, .25f);
            for (int segment = 0; segment < 5; segment++)
            {
                float u = (segment + .5f) / 5f, z = -.5f * u;
                float y = .022f + .105f * Mathf.Sin(u * Mathf.PI);
                float angle = Mathf.Atan(.105f * Mathf.PI / .5f * Mathf.Cos(u * Mathf.PI)) * Mathf.Rad2Deg;
                Transform slat = Part("Arched lid plank " + segment, lid, new Vector3(0, y, z), new Vector3(.71f, .052f, .111f), wood);
                slat.localRotation = Quaternion.Euler(angle, 0, 0);
                foreach (int sign in new[] { -1, 1 })
                {
                    Transform band = Part("Lid band " + segment + "/" + sign, lid, new Vector3(sign * .255f, y + .033f, z), new Vector3(.045f, .018f, .113f), metal);
                    band.localRotation = slat.localRotation;
                }
            }
            Part("Lid front lip", lid, new Vector3(0, .018f, -.515f), new Vector3(.73f, .05f, .04f), edge);
            Part("Lid back lip", lid, new Vector3(0, .018f, .015f), new Vector3(.73f, .05f, .04f), edge);
            Part("Moving latch", lid, new Vector3(0, -.025f, -.53f), new Vector3(.065f, .11f, .025f), metal);

            glowRoot = Child("Contents light", transform);
            glowRoot.localPosition = new Vector3(0, .23f, 0);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader)
            {
                glowMaterial = new Material(shader) { name = "Chest content glow (runtime)", hideFlags = HideFlags.DontSave };
                glowMaterial.SetFloat("_Surface", 1); glowMaterial.SetFloat("_Blend", 0);
                glowMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); glowMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
                glowMaterial.SetFloat("_ZWrite", 0); glowMaterial.SetFloat("_Cull", (float)CullMode.Off);
                glowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glowMaterial.renderQueue = 3000;
                MeshPart("Warm light rays", glowRoot, glowMesh, glowMaterial);
                for (int i = 0; i < 7; i++)
                {
                    Transform mote = MeshPart("Contents sparkle " + i, glowRoot, diamondMesh, glowMaterial);
                    mote.localScale = Vector3.one * (.028f + i % 3 * .008f); motes.Add(mote);
                }
            }
            contentLight = Child("Warm point light", glowRoot).gameObject.AddComponent<Light>();
            contentLight.type = LightType.Point; contentLight.color = new Color(1f, .73f, .28f);
            contentLight.range = 1.5f; contentLight.shadows = LightShadows.None;
            SetOpening(0);
        }

        public void SetOpening(float progress)
        {
            OpeningProgress = Mathf.Clamp01(progress);
            if (!lid) return;
            float lift = Mathf.SmoothStep(0, 1, Mathf.Clamp01((OpeningProgress - .08f) / .70f));
            lid.localRotation = Quaternion.Euler(105f * lift, 0, 0);
            float reveal = Mathf.SmoothStep(0, 1, Mathf.Clamp01((OpeningProgress - .22f) / .42f));
            glowRoot.gameObject.SetActive(reveal > 0);
            if (contentLight) contentLight.intensity = 1.25f * reveal;
            if (glowMaterial) glowMaterial.SetColor("_BaseColor", new Color(1f, .77f, .32f, reveal * .28f));
            for (int i = 0; i < motes.Count; i++)
            {
                float phase = OpeningProgress * .8f + i * .137f;
                float height = Mathf.Repeat(phase, .8f);
                motes[i].localPosition = new Vector3(Mathf.Sin(i * 2.4f) * .20f, .05f + height, Mathf.Cos(i * 2.4f) * .14f);
                motes[i].localRotation = Quaternion.Euler(20, phase * 210, 25);
            }
        }

        Transform Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        { Transform part = MeshPart(name, parent, plankMesh, material); part.localPosition = position; part.localScale = scale; return part; }
        static Transform Child(string name, Transform parent)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static Transform MeshPart(string name, Transform parent, Mesh mesh, Material material)
        {
            var t = Child(name, parent); t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; return t;
        }
        static Mesh RoundedUnitBox()
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            const int divisions = 4;
            foreach (Vector3 normal in axes)
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                for (int iy = 0; iy <= divisions; iy++) for (int ix = 0; ix <= divisions; ix++)
                {
                    Vector3 p = normal * .5f + u * ((float)ix / divisions - .5f) + v * ((float)iy / divisions - .5f);
                    Vector3 inner = new Vector3(Mathf.Clamp(p.x, -.43f, .43f), Mathf.Clamp(p.y, -.43f, .43f), Mathf.Clamp(p.z, -.43f, .43f));
                    Vector3 n = (p - inner).normalized;
                    vertices.Add(inner + n * .07f); normals.Add(n); uv.Add(new Vector2((float)ix / divisions, (float)iy / divisions));
                }
                for (int iy = 0; iy < divisions; iy++) for (int ix = 0; ix < divisions; ix++)
                {
                    int a = start + iy * (divisions + 1) + ix, b = a + 1, c = a + divisions + 1, d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }
            var mesh = new Mesh { name = "Chest bevelled board", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh Diamond()
        {
            var mesh = new Mesh { name = "Chest sparkle", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { Vector3.up, Vector3.down, Vector3.right * .5f, Vector3.forward * .5f, Vector3.left * .5f, Vector3.back * .5f };
            mesh.triangles = new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh GlowCone()
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f, next = angle + .12f;
                int n = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(angle) * .08f, 0, Mathf.Sin(angle) * .08f));
                vertices.Add(new Vector3(Mathf.Cos(angle) * .29f, .75f, Mathf.Sin(angle) * .29f));
                vertices.Add(new Vector3(Mathf.Cos(next) * .29f, .75f, Mathf.Sin(next) * .29f));
                indices.Add(n); indices.Add(n + 1); indices.Add(n + 2);
            }
            var mesh = new Mesh { name = "Chest radial light rays", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        void OnDestroy()
        {
            if (plankMesh) Destroy(plankMesh); if (diamondMesh) Destroy(diamondMesh); if (glowMesh) Destroy(glowMesh);
            if (glowMaterial) Destroy(glowMaterial);
        }
    }
}
