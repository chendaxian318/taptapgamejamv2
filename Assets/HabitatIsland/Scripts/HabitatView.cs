using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HabitatIsland
{
    // All visuals live here. Game rules and the scene remain independent of the art.
    public sealed class GridHit : MonoBehaviour { public int x, z; }

    public partial class HabitatView : MonoBehaviour
    {
        public Camera GameCamera { get; private set; }
        public HabitatPlayerVisual PlayerVisual { get; private set; }
        public HabitatMapHarbor Harbor { get; } = new HabitatMapHarbor();
        public bool IsPlayerMoving => player && (Vector3.Distance(player.position, playerDestination) > .015f || (PlayerVisual && PlayerVisual.IsFalling));
        public bool GridStepComplete { get; private set; } = true;
        public bool GridStepActive => gridStepActive;
        public int GridStepMovingPropCount => gridStepProps.Count;
        public bool IsCinematic => cinematicActive;
        // Inputs use the destination orientation while the visible camera eases towards it.
        public Quaternion InputCameraRotation => Quaternion.Euler(DefaultCameraPitch,-cameraYaw,0f);
        public Vector3 PlayerWorldPosition => player ? player.position : Vector3.zero;
        const float Step = .7f;
        public const float DefaultCameraPitch = 48f;
        public const float DefaultCameraYaw = 14f;
        public const float ShrimpGroundScale = 1.4f;
        public const float ShrimpCarryScale = 1f;
        const float ShrimpMetallicFactor = .12f;
        const float LocalCameraSize = 6.5f;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly Dictionary<int, ActorVisual> actors = new Dictionary<int, ActorVisual>();
        readonly Dictionary<Species, Sprite[]> animalSprites = new Dictionary<Species, Sprite[]>();
        readonly List<Spinner> spinners = new List<Spinner>();
        readonly List<Transform> seaGlints = new List<Transform>();
        readonly List<Transform> distantIslands = new List<Transform>();
        readonly List<int> staleIds = new List<int>();
        readonly List<ArtDecoration> artDecorations = new List<ArtDecoration>();
        readonly Dictionary<string, ImportedModel> importedModels = new Dictionary<string, ImportedModel>();
        readonly Animal overlayProbe = new Animal { species = Species.Jellyfish, id = -999 };
        TileVisual[] tiles;
        HabitatState state;
        Transform world, tileRoot, actorRoot, water, selection, player, playerBillboard, playerShadow, carriedVisual;
        SpriteRenderer playerSprite;
        Sprite[] heroIdle, heroFront, heroBack, heroLeft, heroRight;
        PropKind lastCarried = (PropKind)(-1);
        Vector3 cameraTarget, cameraTargetSmooth;
        Vector3 playerDestination;
        float playerFallSpeed;
        HabitatState gridStepDestination;
        HabitatPlayerMotion gridStepMotion;
        Vector3 gridStepPlayerStart;
        float gridStepDrop;
        bool gridStepActive, gridStepAwaitingTerrain, gridStepFallStarted;
        bool cinematicActive, cinematicPaused, savedPlayerVisible, savedCameraOrthographic;
        Vector3 savedPlayerPosition, savedCameraPosition;
        Quaternion savedCameraRotation;
        float savedCameraSize, savedAnimatorSpeed;
        Color savedCameraBackground;
        CameraClearFlags savedCameraClearFlags;
        readonly List<GridPropMotion> gridStepProps = new List<GridPropMotion>();
        float cameraYaw = DefaultCameraYaw, cameraSize = 6.5f, waterY, nextReport;
        float visibleCameraYaw = DefaultCameraYaw, cameraYawVelocity;
        float overviewSize = 6.5f, framingAspect, nextVisibilityCheck;
        bool followPlayer, overviewCamera, framingDirty = true;
        Bounds mapBounds;
        int oldDockLevel = -999;
        int oldWidth, oldDepth, oldDockX = -999, oldDockZ = -999, oldDockRotation = -999, oldDockWater = -999, oldDockHeight = -999, lastWater = -999;
        Material seaMaterial, selectedMaterial, heroMaterial, conceptArtMaterial, artistFrontMaterial;
        bool artistWater, artistKeeper;
        Sprite artistBoat;
        GameObject shrimpPrefab, trapPrefab, harborPrefab, bellowsPrefab;
        int lastBellowsTick = -1;
        float bellowsPulseRemaining;
        Transform mapDock;
        Material blockAtlasMaterial;
        HabitatOcclusionFade occlusion;
        readonly Color aqua = new Color(.16f, .63f, .69f);
        readonly Color grass = new Color(.38f, .58f, .48f);
        readonly Color sand = new Color(.73f, .63f, .46f);

        sealed class TileVisual
        {
            public Transform root, overlay, prop;
            public HabitatTrapVisual trap;
            public HabitatEquipmentAnimation bellows;
            public HabitatTideEngineVisual tide;
            public bool tideKnown, tidePressed;
            public float tideCompression;
            public Renderer[] overlayParts;
            public int signature = int.MinValue;
            public float top;
        }
        sealed class ActorVisual
        {
            public Animal animal;
            public Transform root, body, shadow;
            public SpriteRenderer sprite;
            public Sprite[] frames;
            public bool model;
            public HabitatShrimpVisual shrimp;
            public HabitatGoldfishVisual fish;
            public HabitatJellyfishVisual jellyfish;
            public HabitatCrabVisual crab;
            public Vector3 lastTarget;
            public float phase;
        }
        sealed class Spinner { public Transform t; public Vector3 axis; public float speed; }
        sealed class ArtDecoration { public Transform t; public Vector3 localPosition; public float bob; }
        sealed class ImportedModel { public GameObject prefab; public Bounds bounds; public bool atlas; }
        sealed class GridPropMotion
        {
            public int destinationIndex;
            public Vector3 start, end;
            public Transform root;
            public float fallingSpeed;
            public bool falling, complete, carriesPlayer;
        }

        public void Initialize(HabitatState s)
        {
            EndRecoveryPresentation(true);
            EndCinematic();
            CancelGridStep();
            state = s;
            if (!world)
            {
                Debug.Log("[Habitat] View initialization: materials and camera");
                world = new GameObject("Habitat • Island presentation").transform;
                world.SetParent(transform, false);
                tileRoot = NewRoot("Terrain", world);
                actorRoot = NewRoot("Residents", world);
                BuildMaterials();
                LoadImportedModels();
                BuildCamera();
                Debug.Log("[Habitat] View initialization: ocean");
                BuildSea();
                Debug.Log("[Habitat] View initialization: artwork");
                LoadArt();
                Debug.Log("[Habitat] View initialization: keeper");
                BuildPlayer();
                selection = NewRoot("Selected cell", world);
                BuildOutline(selection, .965f, .044f, selectedMaterial);
            }
            Debug.Log("[Habitat] View initialization: terrain");
            occlusion = GetComponent<HabitatOcclusionFade>() ?? gameObject.AddComponent<HabitatOcclusionFade>();
            occlusion.Initialize(this, tileRoot);
            EnsureTerrain(s);
            UpdateTrapVisuals(s, 0f, true);
            UpdateBellowsVisuals(s, 0f, true);
            UpdateTideEngineVisuals(s, 0f, true);
            foreach (ActorVisual actor in actors.Values)
                if (actor.root) { actor.root.gameObject.SetActive(false); Destroy(actor.root.gameObject); }
            actors.Clear(); lastCarried = (PropKind)(-1); nextReport = 0f;
            player.position = PlayerPosition(s);
            playerDestination = player.position;
            playerFallSpeed = 0f;
            if (PlayerVisual)
            {
                PlayerVisual.Initialize();
                PlayerVisual.ResetVisual(PlayerFacing(s));
                playerBillboard.gameObject.SetActive(!PlayerVisual.IsModelActive);
            }
            ResetCameraForLevel();
            RefreshDistantDecor();
            UpdateCamera(true);
            UpdateVisibleTerrain(true);
            Debug.Log("[Habitat] View initialization complete");
        }

        void BuildMaterials()
        {
            Mat("grass", grass); Mat("grassLight", new Color(.49f, .66f, .54f));
            Mat("grassDark", new Color(.27f, .45f, .39f)); Mat("sand", sand);
            Mat("sandLight", new Color(.87f, .76f, .56f)); Mat("sandDark", new Color(.55f, .44f, .32f));
            Mat("rock", new Color(.48f, .59f, .56f)); Mat("wood", new Color(.53f, .31f, .18f));
            Mat("woodLight", new Color(.82f, .56f, .3f)); Mat("cream", new Color(.99f, .95f, .8f));
            Mat("dark", new Color(.12f, .23f, .25f)); Mat("white", Color.white);
            Mat("orange", new Color(1f, .56f, .24f)); Mat("yellow", new Color(1f, .82f, .3f));
            Mat("teal", new Color(.14f, .60f, .53f)); Mat("blue", new Color(.26f, .54f, .81f));
            Mat("rose", new Color(.94f, .42f, .47f)); Mat("lavender", new Color(.68f, .57f, .89f));
            Mat("metal", new Color(.32f, .51f, .53f)); Mat("mint", new Color(.62f, .91f, .72f));
            Mat("danger", new Color(.98f, .41f, .37f), true); Mat("safe", new Color(.34f, .88f, .62f), true);
            Texture2D paintedWater = Resources.Load<Texture2D>("Art/Artist/Water");
            artistWater = paintedWater != null;
            seaMaterial = Mat("sea", artistWater ? Color.white : aqua, artistWater);
            if (artistWater)
            {
                // The source painting is not seamless. Mirroring joins identical
                // border colors instead of exposing abrupt repeat seams.
                paintedWater.wrapMode = TextureWrapMode.Mirror;
                Shader waterShader = Shader.Find("Habitat/QuietPaintedWater");
                if (waterShader)
                {
                    seaMaterial.shader = waterShader;
                    seaMaterial.SetColor("_BaseColor", new Color(.25f, .58f, .59f));
                    seaMaterial.SetFloat("_TextureStrength", .10f);
                }
                seaMaterial.SetTexture("_BaseMap", paintedWater);
            }
            Mat("foam", new Color(.76f, .97f, .95f), true);
            selectedMaterial = Mat("selected", new Color(1f, .85f, .32f), true);
            heroMaterial = SpriteMaterial();
            Shader conceptShader = Shader.Find("Habitat/WhiteKeySprite");
            if (conceptShader) conceptArtMaterial = new Material(conceptShader) { name = "Original FigJam creature art" };
            else conceptArtMaterial = heroMaterial;
            artistFrontMaterial = new Material(conceptArtMaterial) { name = "Original keeper front · annotation mask" };
            if (artistFrontMaterial.HasProperty("_TrimArtistFront")) artistFrontMaterial.SetFloat("_TrimArtistFront", 1f);
        }

        Material Mat(string key, Color color, bool unlit = false)
        {
            if (materials.TryGetValue(key, out Material found)) return found;
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            Material m = new Material(shader) { name = "Habitat " + key, color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .18f);
            materials.Add(key, m);
            return m;
        }

        Material SpriteMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (!sh) sh = Shader.Find("Sprites/Default");
            return new Material(sh) { name = "Habitat watercolor sprites" };
        }

        void LoadImportedModels()
        {
            Texture2D atlas = Resources.Load<Texture2D>("Models/KayKit/block_bits_texture");
            if (atlas)
            {
                atlas.wrapMode = TextureWrapMode.Clamp;
                blockAtlasMaterial = Mat("KayKit original block atlas", Color.white);
                blockAtlasMaterial.SetTexture("_BaseMap", atlas);
                blockAtlasMaterial.SetFloat("_Smoothness", .14f);
                // The original grass/sand atlas and UVs are correct. These dry
                // surfaces should not acquire white specular rims from URP Lit.
                if (blockAtlasMaterial.HasProperty("_SpecularHighlights"))
                {
                    blockAtlasMaterial.SetFloat("_SpecularHighlights", 0f);
                    blockAtlasMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                }
                if (blockAtlasMaterial.HasProperty("_EnvironmentReflections"))
                {
                    blockAtlasMaterial.SetFloat("_EnvironmentReflections", 0f);
                    blockAtlasMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }
            }
            CacheImportedModel("sand", "Models/KayKit/sand_A", true);
            CacheImportedModel("sandGrass", "Models/KayKit/sand_with_grass", true);
            CacheImportedModel("dirtGrass", "Models/KayKit/dirt_with_grass", true);
            CacheImportedModel("palm", "Models/Kenney/tree_palmShort", false);
            CacheImportedModel("stone", "Models/Kenney/stone_largeA", false);
            // Original Kenney .mtl Kd values, transferred to the project's URP shader.
            Mat("Kenney leafsGreen", new Color(.1607843f, .7882353f, .6705883f));
            Mat("Kenney woodBark", new Color(.8862745f, .5137255f, .3411765f));
            Mat("Kenney stone", new Color(.7215686f, .8862745f, .9098039f));
            Debug.Log("[Habitat] Imported model templates ready: " + importedModels.Count);
        }

        void CacheImportedModel(string key, string path, bool atlas)
        {
            GameObject prefab = Resources.Load<GameObject>(path);
            if (!prefab || (atlas && !blockAtlasMaterial)) return;
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            bool found = false; Bounds bounds = default;
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh; if (!mesh) continue;
                // Include FBX root axis/unit transforms as well as every nested mesh transform.
                Matrix4x4 matrix = filters[i].transform.localToWorldMatrix;
                Bounds part = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = part.center + Vector3.Scale(part.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = matrix.MultiplyPoint3x4(p);
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; } else bounds.Encapsulate(p);
                }
            }
            if (!found || bounds.size.x < .00001f || bounds.size.y < .00001f || bounds.size.z < .00001f) return;
            importedModels[key] = new ImportedModel { prefab = prefab, bounds = bounds, atlas = atlas };
        }

        bool AddImportedModel(string key, Transform parent, Vector3 floorCenter, Vector3 size, float yaw = 0)
        {
            if (!importedModels.TryGetValue(key, out ImportedModel template)) return false;
            Transform anchor = NewRoot("Imported " + key, parent); anchor.localPosition = floorCenter; anchor.localRotation = Quaternion.Euler(0, yaw, 0);
            Transform normalized = NewRoot("Bounds-normalized original mesh", anchor);
            Vector3 scale = new Vector3(size.x / template.bounds.size.x, size.y / template.bounds.size.y, size.z / template.bounds.size.z);
            normalized.localScale = scale;
            normalized.localPosition = -Vector3.Scale(new Vector3(template.bounds.center.x, template.bounds.min.y, template.bounds.center.z), scale);
            GameObject instance = Instantiate(template.prefab, normalized, false); instance.name = template.prefab.name;
            // Preserve imported transforms; normalization belongs to the parent wrapper only.
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] slots = renderers[i].sharedMaterials;
                for (int j = 0; j < slots.Length; j++)
                {
                    string source = slots[j] ? slots[j].name.ToLowerInvariant() : "";
                    slots[j] = template.atlas ? blockAtlasMaterial : materials[source.Contains("leaf") ? "Kenney leafsGreen" : source.Contains("wood") || source.Contains("bark") ? "Kenney woodBark" : "Kenney stone"];
                }
                renderers[i].sharedMaterials = slots; renderers[i].shadowCastingMode = ShadowCastingMode.On; renderers[i].receiveShadows = true;
            }
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) { colliders[i].enabled = false; Destroy(colliders[i]); }
            return true;
        }

        void BuildCamera()
        {
            Camera existing = Camera.main;
            if (existing) GameCamera = existing;
            else
            {
                GameCamera = new GameObject("Island camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                GameCamera.tag = "MainCamera";
            }
            GameCamera.orthographic = true;
            GameCamera.nearClipPlane = .1f;
            GameCamera.farClipPlane = 100f;
            GameCamera.allowHDR = true;
            GameCamera.allowMSAA = true;
            GameCamera.clearFlags = CameraClearFlags.SolidColor;
            GameCamera.backgroundColor = new Color(.66f, .89f, .89f);
            // A modest yaw and lower pitch reveal more of the keeper's face while
            // retaining orthographic grid readability and quarter-turn controls.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.72f, .84f, .84f);
            Light[] sceneLights = FindObjectsByType<Light>();
            bool hasSun = false;
            for (int i = 0; i < sceneLights.Length; i++) if (sceneLights[i].type == LightType.Directional) { hasSun = true; sceneLights[i].intensity = 1.4f; sceneLights[i].color = new Color(1f, .94f, .83f); sceneLights[i].transform.rotation = Quaternion.Euler(48, -35, 0); sceneLights[i].shadows = LightShadows.Soft; }
            if (!hasSun)
            {
                Light sun = new GameObject("Warm afternoon sun", typeof(Light)).GetComponent<Light>();
                sun.type = LightType.Directional; sun.color = new Color(1f, .94f, .83f); sun.intensity = 1.4f;
                sun.transform.rotation = Quaternion.Euler(48, -35, 0); sun.shadows = LightShadows.Soft;
            }
            (GetComponent<HabitatLighting>() ?? gameObject.AddComponent<HabitatLighting>()).Apply(GameCamera, seaMaterial);
        }

        void BuildSea()
        {
            if (artistWater)
            {
                GameObject sea = new GameObject("Hona hand-painted ocean", typeof(MeshFilter), typeof(MeshRenderer));
                sea.transform.SetParent(world, false); water = sea.transform;
                Mesh mesh = new Mesh { name = "Horizontal ocean surface" };
                mesh.vertices = new[] { new Vector3(-30,.07f,-30), new Vector3(-30,.07f,30), new Vector3(30,.07f,30), new Vector3(30,.07f,-30) };
                // The original painting repeats at its original aspect ratio over the actual XZ plane.
                // Broad repeats preserve the brushwork without competing with the gameplay grid.
                mesh.uv = new[] { Vector2.zero, new Vector2(0, 3.645f), new Vector2(2f, 3.645f), new Vector2(2f, 0) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                sea.GetComponent<MeshFilter>().sharedMesh = mesh;
                sea.GetComponent<MeshRenderer>().sharedMaterial = seaMaterial;
                sea.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            else water = Box("Lagoon", world, new Vector3(4, -.11f, 4), new Vector3(60, .12f, 60), seaMaterial).transform;
            Transform glints = NewRoot("Ocean glints", world);
            for (int i = 0; i < 35; i++)
            {
                float x = -10 + Hash(i, 2) * 28, z = -10 + Hash(i, 9) * 28;
                Transform g = Box("Sea sparkle", glints, new Vector3(x, .016f, z), new Vector3(.13f + Hash(i, 4) * .55f, .007f, .032f), materials["foam"]).transform;
                g.rotation = Quaternion.Euler(0, -8, 0); seaGlints.Add(g);
            }
            BuildRockIsland(new Vector3(-4.8f, 0, 10.5f), .7f);
            BuildRockIsland(new Vector3(11.2f, 0, 10.8f), .9f);
        }

        void BuildRockIsland(Vector3 p, float scale)
        {
            Transform r = NewRoot("Distant sandbar", world); r.position = p; r.localScale = Vector3.one * scale;
            distantIslands.Add(r);
            Sphere("Sandbar", r, new Vector3(0, -.07f, 0), new Vector3(2.8f, .35f, 1.8f), materials["sandLight"]);
            if (importedModels.ContainsKey("palm") && importedModels.ContainsKey("stone"))
            {
                AddImportedModel("stone", r, new Vector3(.38f, .015f, .13f), new Vector3(.82f, .37f, .94f), 25);
                AddImportedModel("palm", r, new Vector3(-.54f, .02f, .02f), new Vector3(.93f, 1.48f, .93f), -20);
                return;
            }
            Sphere("Rock", r, new Vector3(.35f, .19f, .15f), new Vector3(.8f, .65f, .72f), materials["rock"]);
            Sphere("Rock", r, new Vector3(-.24f, .1f, .13f), new Vector3(.55f, .42f, .45f), materials["rock"]);
            BuildPalm(r, new Vector3(-.65f, .02f, 0), .9f);
        }

        void BuildPalm(Transform parent, Vector3 p, float scale)
        {
            Transform palm = NewRoot("Palm", parent); palm.localPosition = p; palm.localScale = Vector3.one * scale;
            for (int i = 0; i < 5; i++)
                Cylinder("Trunk", palm, new Vector3(.045f * i, .1f + i * .18f, 0), new Vector3(.11f - i * .008f, .115f, .11f - i * .008f), materials["woodLight"]);
            Sphere("Crown", palm, new Vector3(.19f, .97f, 0), Vector3.one * .22f, materials["grassDark"]);
            for (int i = 0; i < 6; i++)
            {
                Transform leaf = NewRoot("Frond", palm); leaf.localPosition = new Vector3(.19f, 1.02f, 0); leaf.localRotation = Quaternion.Euler(0, i * 60, -14);
                Sphere("Leaf", leaf, new Vector3(.37f, 0, 0), new Vector3(.93f, .095f, .25f), materials[i % 2 == 0 ? "grass" : "grassDark"]);
            }
            Sphere("Coconut", palm, new Vector3(.12f, .89f, -.11f), Vector3.one * .14f, materials["wood"]);
        }

        Transform raftRipples;
        int raftRippleSignature;
        void UpdateRaftRipples(HabitatState s)
        {
            int signature=s.width*397+s.depth;
            foreach(var t in s.tiles)signature=unchecked(signature*31+(int)t.prop*7+t.rotation+t.height*13);
            signature=unchecked(signature*31+s.waterLevel);
            if(raftRipples&&signature==raftRippleSignature)return;
            raftRippleSignature=signature;
            if(!raftRipples)raftRipples=NewRoot("Raft current direction",world);
            ClearChildren(raftRipples);
            for(int z=0;z<s.depth;z++)for(int x=0;x<s.width;x++)
            {
                Tile raft=HabitatRules.TileAt(s,x,z);if(raft.prop!=PropKind.Raft)continue;
                int dx=raft.rotation%2==1?1:0,dz=dx==0?1:0;
                for(int sign=-1;sign<=1;sign+=2)for(int n=1;n<Math.Max(s.width,s.depth);n++)
                {
                    int rx=x+dx*n*sign,rz=z+dz*n*sign;var tile=HabitatRules.TileAt(s,rx,rz);
                    if(tile==null||!HabitatRules.IsWater(s,rx,rz)||tile.prop!=PropKind.None&&tile.prop!=PropKind.Shell)break;
                    Transform mark=NewRoot("Current ripple",raftRipples);mark.localPosition=new Vector3(rx,waterY+.018f,rz);
                    mark.localRotation=Quaternion.Euler(0,dx!=0?90:0,0);
                    for(int heading=-1;heading<=1;heading+=2)for(int side=-1;side<=1;side+=2)
                    {
                        var stroke=Box("Water chevron",mark,new Vector3(side*.065f,0,heading*.13f),new Vector3(.018f,.006f,.16f),materials["foam"]);
                        stroke.transform.localRotation=Quaternion.Euler(0,side*heading*48,0);
                    }
                }
            }
        }

        void EnsureTerrain(HabitatState s)
        {
            tileRoot.gameObject.SetActive(!HabitatLevels.IsHomeHarbor(s));
            bool layoutChanged = tiles == null || oldWidth != s.width || oldDepth != s.depth;
            if (layoutChanged)
            {
                ClearChildren(tileRoot); tiles = new TileVisual[s.width * s.depth];
                oldWidth = s.width; oldDepth = s.depth;
                framingDirty = true; nextVisibilityCheck = 0f;
                for (int z = 0; z < s.depth; z++) for (int x = 0; x < s.width; x++)
                {
                    TileVisual v = new TileVisual { root = NewRoot("Cell " + x + ", " + z, tileRoot) };
                    v.root.localPosition = new Vector3(x, 0, z); tiles[x + z * s.width] = v;
                }
            }
            if (layoutChanged || lastWater != s.waterLevel)
            {
                lastWater = s.waterLevel; waterY = HabitatRules.WaterSurfaceY(s.waterLevel);
                framingDirty = true; nextVisibilityCheck = 0f;
                water.localPosition = new Vector3((s.width - 1) * .5f, waterY - .07f, (s.depth - 1) * .5f);
                float stretchX = Mathf.Max(1f, (s.width + 60f) / 60f), stretchZ = Mathf.Max(1f, (s.depth + 60f) / 60f);
                if(HabitatLevels.IsHomeHarbor(s)){stretchX=Mathf.Max(stretchX,3f);stretchZ=Mathf.Max(stretchZ,3f);}
                water.localScale = artistWater ? new Vector3(stretchX, 1, stretchZ) : new Vector3(60 * stretchX, .12f, 60 * stretchZ);
                if (artistWater) seaMaterial.SetTextureScale("_BaseMap", new Vector2(stretchX, stretchZ));
                for (int i = 0; i < seaGlints.Count; i++)
                    seaGlints[i].position = new Vector3(-10 + Hash(i, 2) * (s.width + 20), waterY + .015f, -10 + Hash(i, 9) * (s.depth + 20));
            }
            for (int z = 0; z < s.depth; z++) for (int x = 0; x < s.width; x++)
            {
                Tile t = HabitatRules.TileAt(s, x, z); TileVisual v = tiles[x + z * s.width];
                int sig = unchecked(t.height * 397 ^ (int)t.prop);
                sig = unchecked(sig * 397 ^ t.rotation);
                sig = unchecked(sig * 397 ^ (t.trap ? 1 : 0));
                sig = unchecked(sig * 397 ^ s.waterLevel);
                sig = unchecked(sig * 397 ^ (int)t.coveredProp);
                // Shore foam also responds when the adjacent soil, rather than this cell, changes.
                sig = unchecked(sig * 397 ^ (HabitatRules.TileAt(s, x - 1, z)?.height ?? -1));
                sig = unchecked(sig * 397 ^ (HabitatRules.TileAt(s, x + 1, z)?.height ?? -1));
                sig = unchecked(sig * 397 ^ (HabitatRules.TileAt(s, x, z - 1)?.height ?? -1));
                sig = unchecked(sig * 397 ^ (HabitatRules.TileAt(s, x, z + 1)?.height ?? -1));
                if (sig != v.signature)
                {
                    v.signature = sig;
                    // Registration must see the rebuilt renderers, even if this cell was culled.
                    v.root.gameObject.SetActive(true);
                    RebuildTile(s, t, v, x, z);
                    framingDirty = true; nextVisibilityCheck = 0f;
                }
            }
            UpdateRaftRipples(s);
            int dockHeight = HabitatRules.Height(s, s.dockX, s.dockZ);
            if (oldDockLevel != s.levelIndex || oldDockX != s.dockX || oldDockZ != s.dockZ || oldDockRotation != s.dockRotation || oldDockWater != s.waterLevel || oldDockHeight != dockHeight)
            {
                oldDockLevel=s.levelIndex; oldDockX = s.dockX; oldDockZ = s.dockZ; oldDockRotation = s.dockRotation; oldDockWater = s.waterLevel;
                oldDockHeight = dockHeight;
                framingDirty = true;
                Transform old = world.Find("Trading dock");
                if (old)
                {
                    old.gameObject.SetActive(false);
                    old.SetParent(null);
                    Destroy(old.gameObject);
                }
                BuildDock(s);
            }
        }

        void RebuildTile(HabitatState s, Tile t, TileVisual v, int x, int z)
        {
            v.trap = null;
            v.bellows = null;
            v.tide = null;
            v.prop = null;
            ClearChildren(v.root);
            float surface = t.height * Step;
            bool wet = t.height <= s.waterLevel;
            float terrainBase = -1.1f;
            // Individual sandstone courses make every stack height legible from all angles.
            if (!wet)
            {
                if (importedModels.ContainsKey("sand") && importedModels.ContainsKey("sandGrass"))
                {
                    // Every gameplay elevation is exactly .7 high. The top mesh includes its grass cap;
                    // rounded/scalloped author geometry is scaled by full bounds, never guessed pivots.
                    for (int layer = -1; layer < t.height; layer++)
                    {
                        bool top = layer == t.height - 1;
                        string model = top ? (t.height >= 2 && importedModels.ContainsKey("dirtGrass") ? "dirtGrass" : "sandGrass") : "sand";
                        AddImportedModel(model, v.root, new Vector3(0, layer * Step + .045f, 0), new Vector3(.985f, Step, .985f), ((x + z + layer) & 3) * 90);
                    }
                }
                else
                {
                float h = surface - terrainBase;
                Box("Sandstone foundation", v.root, new Vector3(0, terrainBase + h * .5f, 0), new Vector3(.984f, h, .984f), materials["sand"]);
                for (int level = 0; level < t.height; level++)
                {
                    float y = level * Step;
                    Box("Layer seam", v.root, new Vector3(0, y + .028f, 0), new Vector3(.991f, .048f, .991f), materials["sandDark"]);
                    Box("Sand layer", v.root, new Vector3(0, y + .26f, 0), new Vector3(.991f, .19f, .991f), materials["sandLight"]);
                }
                Box("Grass lip", v.root, new Vector3(0, surface - .037f, 0), new Vector3(1f, .085f, 1f), materials["grassDark"]);
                Box("Soft turf", v.root, new Vector3(0, surface + .018f, 0), new Vector3(.956f, .052f, .956f), materials[(x + z) % 3 == 0 ? "grassLight" : "grass"]);
                }
                // Small corner tufts only on unobstructed land.
                if (t.prop == PropKind.None && (x * 3 + z * 7) % 5 == 0)
                {
                    Vector3 p = new Vector3(-.33f, surface + .07f, .33f);
                    Sphere("Turf tuft", v.root, p, new Vector3(.11f, .1f, .05f), materials["grassDark"]);
                    Sphere("Turf tuft", v.root, p + new Vector3(.05f, .015f, -.02f), new Vector3(.06f, .13f, .04f), materials["grass"]);
                    if ((x + z) % 2 == 0) Sphere("Wildflower", v.root, p + new Vector3(.10f, .08f, .04f), Vector3.one * .065f, materials["cream"]);
                }
            }
            else
            {
                // Subtle cell lines remain visible in the lagoon, with shoreline foam at dry edges.
                Material cellMat = Mat("waterCell" + ((x + z) % 3), Color.Lerp(aqua, new Color(.39f, .82f, .81f), .07f + ((x + z) % 3) * .035f));
                if (!artistWater) Box("Lagoon cell", v.root, new Vector3(0, waterY - .01f, 0), new Vector3(.986f, .008f, .986f), cellMat);
                AddShoreFoam(s, v.root, x, z);
                if ((x * 11 + z * 5) % 9 == 0) Box("Quiet ripple", v.root, new Vector3(.16f, waterY + .013f, .12f), new Vector3(.24f, .005f, .018f), materials["foam"]);
            }
            float visibleSurface = wet ? waterY + .015f : surface + .045f;
            v.top = visibleSurface;
            GameObject hit = Box("Pick cell", v.root, new Vector3(0, visibleSurface - .12f, 0), new Vector3(.99f, .24f, .99f), materials["dark"]);
            hit.GetComponent<Renderer>().enabled = false;
            hit.AddComponent<BoxCollider>();
            GridHit grid = hit.AddComponent<GridHit>(); grid.x = x; grid.z = z;
            if (t.coveredProp != PropKind.None)
            {
                var covered = BuildProp(v.root, t.coveredProp, t.coveredProp==PropKind.Float?s.waterLevel*Step+.08f:visibleSurface, t.rotation);
                if (t.coveredProp == PropKind.Tide) v.tide = covered.GetComponentInChildren<HabitatTideEngineVisual>(true);
            }
            if (t.prop != PropKind.None) v.prop = BuildProp(v.root, t.prop, PropSurfacePosition(s,z*s.width+x).y, t.rotation);
            if (t.prop == PropKind.Fan && v.prop) v.bellows = v.prop.GetComponentInChildren<HabitatEquipmentAnimation>(true);
            if (t.prop == PropKind.Tide && v.prop) v.tide = v.prop.GetComponentInChildren<HabitatTideEngineVisual>(true);
            if (t.trap) v.trap = BuildTrap(v.root, visibleSurface, TrapOccupied(s, x, z));
            v.overlay = NewRoot("Habitat overlay", v.root); v.overlay.localPosition = new Vector3(0, visibleSurface + .028f, 0);
            BuildOutline(v.overlay, .87f, .025f, materials["safe"]);
            v.overlayParts = v.overlay.GetComponentsInChildren<Renderer>();
            v.overlay.gameObject.SetActive(false);
            if (occlusion) occlusion.RegisterCell(v.root, s, x, z);
        }

        void AddShoreFoam(HabitatState s, Transform p, int x, int z)
        {
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                if (!HabitatRules.Inside(s, x + dx[i], z + dz[i])) continue;
                if (HabitatRules.TileAt(s, x + dx[i], z + dz[i]).height <= s.waterLevel) continue;
                Box("Shore foam", p, new Vector3(dx[i] * .462f, waterY + .015f, dz[i] * .462f), i < 2 ? new Vector3(.028f, .008f, .88f) : new Vector3(.88f, .008f, .028f), materials["foam"]);
            }
        }

        void BuildOutline(Transform p, float width, float stroke, Material m)
        {
            float half = width * .5f;
            Box("North", p, new Vector3(0, .005f, half), new Vector3(width, .017f, stroke), m);
            Box("South", p, new Vector3(0, .005f, -half), new Vector3(width, .017f, stroke), m);
            Box("East", p, new Vector3(half, .005f, 0), new Vector3(stroke, .017f, width), m);
            Box("West", p, new Vector3(-half, .005f, 0), new Vector3(stroke, .017f, width), m);
        }

        public HabitatChestVisual ChestVisualAt(int x,int z)
        {
            if(state==null||tiles==null||!HabitatRules.Inside(state,x,z))return null;
            Transform prop=tiles[x+z*state.width].prop;
            return prop?prop.GetComponent<HabitatChestVisual>():null;
        }

        Transform BuildProp(Transform parent, PropKind kind, float y, int rotation)
        {
            Transform p = NewRoot(kind.ToString(), parent); p.localPosition = new Vector3(0, y, 0); p.localRotation = Quaternion.Euler(0, rotation * 90, 0);
            switch (kind)
            {
                case PropKind.Crate:
                    Box("Crate body", p, new Vector3(0, .32f, 0), new Vector3(.70f, .63f, .70f), materials["woodLight"]);
                    for (int i = 0; i < 3; i++)
                    {
                        Box("Front planks", p, new Vector3(0, .12f + i * .2f, -.357f), new Vector3(.71f, .024f, .025f), materials["wood"]);
                        Box("Side planks", p, new Vector3(.357f, .12f + i * .2f, 0), new Vector3(.025f, .024f, .71f), materials["wood"]);
                    }
                    for (int i = -1; i <= 1; i += 2)
                    {
                        Box("Crate band", p, new Vector3(i * .27f, .32f, -.37f), new Vector3(.055f, .64f, .04f), materials["wood"]);
                        Box("Crate band", p, new Vector3(i * .27f, .652f, 0), new Vector3(.055f, .03f, .73f), materials["wood"]);
                    }
                    if(kind==PropKind.Float)
                    {
                        Box("Floating crate lid",p,new Vector3(0,.65f,0),new Vector3(.70f,.05f,.70f),materials["woodLight"]);
                        for(int i=-1;i<=1;i+=2)Box("Visible waterline band",p,new Vector3(i*.27f,.681f,0),new Vector3(.055f,.012f,.73f),materials["wood"]);
                    }
                    break;
                case PropKind.Raft:
                    // Thin, lashed bamboo deck: clearly distinct from a buoyant crate.
                    for(int i=0;i<6;i++)
                    {
                        var pole=Cylinder("Bamboo pole",p,new Vector3(-.35f+i*.14f,-.045f,0),new Vector3(.13f,.46f,.13f),materials[i%2==0?"woodLight":"sandLight"]);
                        pole.transform.localRotation=Quaternion.Euler(90,0,0);
                        for(int j=-1;j<=1;j++)Box("Bamboo joint",p,new Vector3(-.35f+i*.14f,.013f,j*.29f),new Vector3(.14f,.012f,.018f),materials["wood"]);
                    }
                    for(int i=-1;i<=1;i+=2)Box("Raft lashings",p,new Vector3(0,.018f,i*.25f),new Vector3(.88f,.022f,.035f),materials["wood"]);
                    break;
                case PropKind.Float:
                    goto case PropKind.Crate;
                case PropKind.Climb:
                case PropKind.ClimbBlock:
                    if (!AddImportedModel("sandGrass", p, Vector3.zero, new Vector3(.89f, .7f, .89f)))
                    {
                    Box("Soil block", p, new Vector3(0, .30f, 0), new Vector3(.85f, .62f, .85f), materials["sand"]);
                    Box("Top grass", p, new Vector3(0, .65f, 0), new Vector3(.89f, .10f, .89f), materials["grassLight"]);
                    }
                    for (int i = 0; i < 3; i++) Box("Climb steps", p, new Vector3(0, .10f + i * .20f, -.45f), new Vector3(.48f, .055f, .1f), materials["wood"]);
                    break;
                case PropKind.Road:
                    Box("Turntable paving", p, new Vector3(0, .035f, 0), new Vector3(.79f, .06f, .79f), materials["cream"]);
                    Box("Route vertical", p, new Vector3(0, .071f, .14f), new Vector3(.18f, .012f, .51f), materials["teal"]);
                    Box("Route horizontal", p, new Vector3(.14f, .072f, 0), new Vector3(.5f, .012f, .18f), materials["teal"]);
                    Cylinder("Rotatable hub", p, new Vector3(0, .088f, 0), new Vector3(.21f, .018f, .21f), materials["yellow"]);
                    break;
                case PropKind.Fan:
                    if (!bellowsPrefab) bellowsPrefab = Resources.Load<GameObject>("Models/ShellBellows/Bellows");
                    if (bellowsPrefab)
                    {
                        var model = Instantiate(bellowsPrefab, p, false);
                        model.name = "Shell bellows";
                        var animation = model.GetComponent<HabitatEquipmentAnimation>();
                        if (animation) { animation.Play("Idle"); animation.Tick(0f); }
                        break;
                    }
                    Cylinder("Fan foundation", p, new Vector3(0, .07f, 0), new Vector3(.44f, .07f, .44f), materials["cream"]);
                    Box("Fan mast", p, new Vector3(0, .36f, 0), new Vector3(.095f, .56f, .095f), materials["metal"]);
                    Transform rotor = NewRoot("Spinning rotor", p); rotor.localPosition = new Vector3(0, .68f, -.04f);
                    Sphere("Fan hub", rotor, Vector3.zero, Vector3.one * .14f, materials["yellow"]);
                    for (int i = 0; i < 4; i++)
                    {
                        Transform blade = NewRoot("Blade", rotor); blade.localRotation = Quaternion.Euler(0, 0, i * 90);
                        Sphere("Fan blade", blade, new Vector3(.18f, .06f, 0), new Vector3(.32f, .135f, .045f), materials["mint"]);
                    }
                    spinners.Add(new Spinner { t = rotor, axis = Vector3.forward, speed = 150 });
                    break;
                case PropKind.Tide:
                    if (AddTideEngine(p)) break;
                    Cylinder("Tide dial base", p, new Vector3(0, .035f, 0), new Vector3(.8f, .035f, .8f), materials["metal"]);
                    Cylinder("Pressure pad", p, new Vector3(0, .078f, 0), new Vector3(.65f, .018f, .65f), materials["blue"]);
                    Box("Wave mark", p, new Vector3(0, .1f, -.08f), new Vector3(.34f, .015f, .038f), materials["foam"]);
                    Box("Wave mark", p, new Vector3(.08f, .1f, .08f), new Vector3(.34f, .015f, .038f), materials["foam"]);
                    break;
                case PropKind.Obstacle:
                    if (AddImportedModel("stone", p, Vector3.zero, new Vector3(.79f, .51f, .72f), 20)) break;
                    Sphere("Stone", p, new Vector3(-.13f, .23f, 0), new Vector3(.62f, .57f, .68f), materials["rock"]);
                    Sphere("Stone", p, new Vector3(.24f, .15f, -.08f), new Vector3(.44f, .34f, .5f), materials["sandDark"]);
                    Sphere("Moss", p, new Vector3(-.16f, .46f, .05f), new Vector3(.36f, .11f, .34f), materials["grassDark"]);
                    break;
                case PropKind.Shell:
                    var shedShell=Resources.Load<GameObject>("Models/HermitCrab/ShedShell");
                    if(shedShell)
                    {
                        var shell=Instantiate(shedShell,p,false);
                        shell.AddComponent<HabitatShedShellVisual>();
                        break;
                    }
                    Sphere("Pearl shell", p, new Vector3(0, .16f, 0), new Vector3(.55f, .20f, .46f), materials["cream"]);
                    for (int rib = -2; rib <= 2; rib++)
                        Sphere("Shell ridge", p, new Vector3(rib * .085f, .23f, .02f), new Vector3(.035f, .035f, .34f - Mathf.Abs(rib) * .05f), materials["yellow"]);
                    break;
                case PropKind.Chest:
                    p.gameObject.AddComponent<HabitatChestVisual>().Build(materials["wood"],materials["woodLight"],materials["yellow"],materials["dark"]);
                    break;
            }
            return p;
        }

        static bool TrapOccupied(HabitatState s, int x, int z)
        {
            foreach (Animal animal in s.animals)
                if (animal.species == Species.Shrimp && !animal.carried && animal.x == x && animal.z == z) return true;
            return false;
        }

        void UpdateTrapVisuals(HabitatState s, float dt, bool reset)
        {
            if (tiles == null) return;
            for (int z = 0; z < s.depth; z++) for (int x = 0; x < s.width; x++)
            {
                HabitatTrapVisual trap = tiles[x + z * s.width].trap;
                if (!trap) continue;
                bool occupied = TrapOccupied(s, x, z);
                if (reset) trap.ResetVisual(occupied);
                else trap.Render(occupied, dt);
            }
        }

        // Animation follows existing simulation ticks; it never generates wind or moves a block.
        void UpdateBellowsVisuals(HabitatState s, float dt, bool reset)
        {
            bool ticked = !reset && lastBellowsTick >= 0 && s.tickCount > lastBellowsTick;
            if (reset || s.tickCount < lastBellowsTick) bellowsPulseRemaining = 0f;
            lastBellowsTick = s.tickCount;
            var game = HabitatGame.Instance;
            bool running = game && !game.paused && !game.StoryPlaying
                && (!game.Hud || !game.Hud.ModalOpen) && (!game.Fishing || !game.Fishing.IsOpen);
            if (ticked && !running) bellowsPulseRemaining = 1f;
            if (tiles != null) foreach (var tile in tiles)
            {
                var animation = tile.bellows;
                if (!animation) continue;
                if (reset) animation.Play("Idle", true, 1f, true);
                else if (running)
                    animation.Play("Pump_Loop", true, 1.5f / Mathf.Max(.2f, game.tuning.moveSeconds) * game.speed);
                else if (bellowsPulseRemaining > 0f)
                    animation.Play("Pump_Once", false, 1f, ticked);
                else animation.Play("Idle");
                animation.Tick(dt);
            }
            if (!running) bellowsPulseRemaining = Mathf.Max(0f, bellowsPulseRemaining - dt);
        }

        HabitatTrapVisual BuildTrap(Transform parent, float y, bool occupied)
        {
            if (!trapPrefab) trapPrefab = Resources.Load<GameObject>("Models/AnimatedTrap/Trap");
            if (trapPrefab)
            {
                GameObject model = Instantiate(trapPrefab, parent, false);
                model.name = "Animated lobster trap";
                model.transform.localPosition = new Vector3(0, y + .015f, 0);
                var visual = model.GetComponent<HabitatTrapVisual>();
                if (visual) visual.ResetVisual(occupied);
                return visual;
            }
            Transform p = NewRoot("Shrimp basket", parent); p.localPosition = new Vector3(.19f, y + .07f, .19f);
            Cylinder("Basket rim", p, Vector3.zero, new Vector3(.42f, .018f, .42f), materials["woodLight"]);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3;
                Box("Basket slat", p, new Vector3(Mathf.Cos(a) * .16f, .06f, Mathf.Sin(a) * .16f), new Vector3(.025f, .15f, .025f), materials["wood"]);
            }
            Sphere("Basket float", p, new Vector3(.2f, .10f, -.22f), Vector3.one * .1f, materials["rose"]);
            return null;
        }

        void BuildDock(HabitatState s)
        {
            Transform dock = NewRoot("Trading dock", world);
            mapDock = dock;
            int dx = s.dockX; int dz = s.dockZ;
            dock.position = new Vector3(dx, .22f + (s.waterLevel+1) * Step, dz);
            dock.rotation = DockRotation(s);
            if(!HabitatLevels.IsHomeHarbor(s)){BuildIslandLanding(dock,s);return;}
            if (BuildImportedMapDock(dock, s)) return;
            for (int i = 0; i < 6; i++) Box("Jetty plank", dock, new Vector3(0, 0, -.64f - i * .22f), new Vector3(1f, .11f, .19f), materials[i % 2 == 0 ? "woodLight" : "sand"]);
            for (int i = 0; i < 2; i++) for (int side = -1; side <= 1; side += 2)
                Cylinder("Mooring post", dock, new Vector3(side * .43f, -.04f, -.72f - i * .91f), new Vector3(.10f, .36f, .10f), materials["wood"]);
            Transform boat = NewRoot("Trading sailboat", dock); boat.localPosition = new Vector3(-1.2f, -.03f, -1.45f); boat.localRotation = Quaternion.Euler(0, -9, 0);
            if (artistBoat != null)
            {
                boat.name = "Hona original hand-painted rowing boat";
                boat.localPosition = new Vector3(-1.2f, -.18f, -1.45f);
                AddArtDecoration(boat, artistBoat, .016f);
            }
            else
            {
            Sphere("Boat hull", boat, Vector3.zero, new Vector3(.8f, .33f, 1.68f), materials["wood"]);
            Sphere("Painted gunwale", boat, new Vector3(0, .12f, 0), new Vector3(.76f, .11f, 1.60f), materials["cream"]);
            Box("Boat deck", boat, new Vector3(0, .19f, 0), new Vector3(.52f, .05f, 1.11f), materials["woodLight"]);
            Cylinder("Sail mast", boat, new Vector3(0, .83f, .15f), new Vector3(.04f, .70f, .04f), materials["wood"]);
            Triangle("Canvas sail", boat, new Vector3(.02f, .44f, .15f), new Vector3(.02f, 1.43f, .15f), new Vector3(.68f, .5f, .15f), materials["cream"]);
            Triangle("Coral pennant", boat, new Vector3(.02f, 1.40f, .15f), new Vector3(.02f, 1.55f, .15f), new Vector3(.32f, 1.47f, .15f), materials["rose"]);
            Box("Cargo", boat, new Vector3(-.13f, .36f, -.42f), new Vector3(.3f, .29f, .28f), materials["sand"]);
            Sphere("Life buoy", boat, new Vector3(-.39f, .12f, .24f), new Vector3(.12f, .28f, .28f), materials["orange"]);
            }
            // Anchor the merchant marker outside the playable tile.
            Transform sign = NewRoot("Merchant flag", dock); sign.localPosition = new Vector3(.45f, .17f, -.66f);
            Cylinder("Flag pole", sign, new Vector3(0, .43f, 0), new Vector3(.035f, .46f, .035f), materials["wood"]);
            Box("Shop flag", sign, new Vector3(.18f, .65f, 0), new Vector3(.35f, .25f, .022f), materials["teal"]);
            Sphere("Shop coin", sign, new Vector3(.18f, .65f, -.025f), new Vector3(.13f, .13f, .027f), materials["yellow"]);
        }

        void BuildIslandLanding(Transform dock, HabitatState s)
        {
            float top=Mathf.Max(HabitatRules.Height(s,s.dockX,s.dockZ)*Step,HabitatRules.WaterSurfaceY(s.waterLevel)+.025f)-dock.position.y;
            Vector3 shore=new Vector3(0,top,-.44f),end=new Vector3(0,top,-2.5f);
            for(int i=0;i<=13;i++)Box("Island landing plank",dock,new Vector3(0,top-.045f,-.4f-i*.17f),new Vector3(.94f,.09f,.18f),materials[i%2==0?"woodLight":"wood"]);
            for(int i=0;i<2;i++)for(int side=-1;side<=1;side+=2)
                Cylinder("Landing mooring post",dock,new Vector3(side*.42f,top-.1f,-.7f-i*1.6f),new Vector3(.08f,.32f,.08f),materials["wood"]);
            // Retain measured boat/story anchors, without instantiating the home or shop.
            Transform model=NewRoot("Island boat anchors",dock);
            model.localScale=Vector3.one*.7f;model.localRotation=Quaternion.Euler(0,-90,0);
            model.localPosition=new Vector3(0,top,-2f)-model.localRotation*(HabitatImportedDockLayout.BoatApproach*.7f);
            Harbor.Bind(model,dock,shore,end,.94f,true,false);
            model.gameObject.AddComponent<HabitatHarborBoat>().Initialize(s.waterLevel);
        }

        bool BuildImportedMapDock(Transform dock, HabitatState s)
        {
            if (!harborPrefab) harborPrefab = Resources.Load<GameObject>("Models/WoodenDock/Harbor");
            if (!harborPrefab) return false;
            Transform model = Instantiate(harborPrefab, dock, false).transform;
            model.name = "Wooden harbor on island map";
            const float scale = .7f;
            // The prefab is already normalized to 24 m. On the island it spans 16.8 cells,
            // with the open end facing shore; its complete mesh stays seaward of the landing.
            model.localScale = Vector3.one * scale;
            model.localRotation = Quaternion.Euler(0, -90f, 0);
            Vector3 entrance = model.localRotation * (HabitatImportedDockLayout.FromSource(-.82f,
                HabitatImportedDockLayout.SourceDeckY, .29f) * scale);
            float nearestShore = float.NegativeInfinity;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    nearestShore = Mathf.Max(nearestShore, dock.InverseTransformPoint(filter.transform.TransformPoint(corner)).z);
                }
            }
            float deckY = .025f - HabitatImportedDockLayout.SeaSurfaceY * scale - .22f;
            model.localPosition = new Vector3(-entrance.x, deckY, -.9f - nearestShore);
            entrance += model.localPosition;
            // Direct collider sampling supports feet; the Ignore Raycast layer keeps grid
            // selection from being intercepted by the port's merged mesh.
            foreach (Collider collider in model.GetComponentsInChildren<Collider>())
            { collider.enabled = true; collider.gameObject.layer = 2; }
            float shoreY = Mathf.Max(HabitatRules.Height(s, s.dockX, s.dockZ) * Step,
                HabitatRules.WaterSurfaceY(s.waterLevel) + .025f) - dock.position.y;
            Vector3 shore = new Vector3(0, shoreY, -.44f);
            Vector3 end = model.localPosition + model.localRotation *
                (HabitatImportedDockLayout.FromSource(-.64f, HabitatImportedDockLayout.SourceDeckY, .29f) * scale);
            int planks = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(shore, end) / .18f));
            for (int i = 0; i <= planks && !HabitatLevels.IsHomeHarbor(s); i++)
                Box("Shore connection plank", dock, Vector3.Lerp(shore, end, (float)i / planks) - Vector3.up * .045f,
                    new Vector3(.94f, .09f, .19f), materials[i % 2 == 0 ? "woodLight" : "wood"]);
            // Home harbor now contains only the delivered wooden dock (feedback 22:17).
            Harbor.Bind(model, dock, shore, end, HabitatLevels.IsHomeHarbor(s)?0f:.94f, false, HabitatLevels.IsHomeHarbor(s));
            model.gameObject.AddComponent<HabitatHarborBoat>().Initialize(s.waterLevel);
            return true;
        }

        void LoadArt()
        {
            Texture2D front = Resources.Load<Texture2D>("Art/Artist/HeroFront");
            Texture2D back = Resources.Load<Texture2D>("Art/Artist/HeroBack");
            artistKeeper = front != null && back != null;
            if (artistKeeper)
            {
                heroIdle = new Sprite[0]; heroLeft = new Sprite[0]; heroRight = new Sprite[0];
                heroFront = new[] { MakeArtistFront(front) };
                heroBack = new[] { SliceConcept(back, new Rect(.044f, .052f, .906f, .884f), 1.07f, "Hona hero back") };
            }
            else
            {
                // Source project can still load without the optional art folder; never substitute AI art.
                heroIdle = new Sprite[0];
                heroFront = LoadFrames("HeroFront", .91f); heroBack = LoadFrames("HeroBack", .91f);
                heroLeft = LoadFrames("HeroLeft", .91f); heroRight = LoadFrames("HeroRight", .91f);
            }
            if (heroIdle.Length == 0 && heroFront.Length == 0)
            {
                heroFront = new[] { MakeHeroSprite(0) }; heroBack = new[] { MakeHeroSprite(1) };
                heroLeft = new[] { MakeHeroSprite(2) }; heroRight = new[] { MakeHeroSprite(3) };
            }
            foreach (Species sp in Enum.GetValues(typeof(Species))) animalSprites[sp] = LoadFrames(sp.ToString(), sp == Species.Jellyfish ? .62f : sp == Species.Turtle ? .44f : .49f);
            // These are original illustrations from the supplied FigJam, sliced without changing their pixels.
            Texture2D sheet = Resources.Load<Texture2D>("Art/Artist/Creatures");
            if (!sheet) sheet = Resources.Load<Texture2D>("Art/CreatureSheet");
            if (sheet)
            {
                animalSprites[Species.Jellyfish] = new[] { SliceConcept(sheet, new Rect(.006f, .285f, .139f, .373f), .63f, "Jellyfish") };
                animalSprites[Species.Shrimp] = new[] { SliceConcept(sheet, new Rect(.421f, .625f, .2f, .338f), .54f, "Shrimp") };
                animalSprites[Species.Goldfish] = new[] { SliceConcept(sheet, new Rect(.787f, .558f, .188f, .442f), .63f, "Goldfish") };
            }
            Texture2D crab = Resources.Load<Texture2D>("Art/Crab");
            if (crab) animalSprites[Species.Crab] = new[] { SliceConcept(crab, new Rect(.067f, .075f, .92f, .79f), .62f, "Crab") };
            Texture2D harborParts = Resources.Load<Texture2D>("Art/Artist/HarborParts");
            if (harborParts) artistBoat = Sprite.Create(harborParts, new Rect(3294f / 3909f * harborParts.width, 501f / 2480f * harborParts.height, 466f / 3909f * harborParts.width, 602f / 2480f * harborParts.height), new Vector2(.5f, .025f), (602f / 2480f * harborParts.height) / 1.4f, 0, SpriteMeshType.FullRect);
        }

        Sprite MakeArtistFront(Texture2D texture)
        {
            const float height = 1.12f;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.54f, .028f), texture.height / height, 0, SpriteMeshType.FullRect);
            // The material omits palette notes in original texture UVs. FullRect avoids
            // runtime tight-mesh tessellation and never changes the artist's bitmap.
            sprite.name = "FigJam Hona original hero front";
            return sprite;
        }

        void AddArtDecoration(Transform t, Sprite sprite, float bob)
        {
            SpriteRenderer renderer = t.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sharedMaterial = heroMaterial; renderer.sortingOrder = 0;
            artDecorations.Add(new ArtDecoration { t = t, localPosition = t.localPosition, bob = bob });
        }

        Sprite SliceConcept(Texture2D texture, Rect normalized, float worldHeight, string title)
        {
            Rect r = new Rect(Mathf.Floor(normalized.x * texture.width), Mathf.Floor(normalized.y * texture.height), Mathf.Floor(normalized.width * texture.width), Mathf.Floor(normalized.height * texture.height));
            Sprite sprite = Sprite.Create(texture, r, new Vector2(.5f, 0), r.height / worldHeight, 0, SpriteMeshType.FullRect);
            sprite.name = "FigJam " + title; return sprite;
        }

        Sprite[] LoadFrames(string key, float worldHeight)
        {
            List<Sprite> frames = new List<Sprite>();
            for (int i = 0; i < 12; i++)
            {
                Sprite sprite = LoadSprite("Art/" + key + "_" + i.ToString("00"), worldHeight);
                if (!sprite) break;
                frames.Add(sprite);
            }
            if (frames.Count == 0)
            {
                Sprite sprite = LoadSprite("Art/" + key, worldHeight);
                if (sprite) frames.Add(sprite);
            }
            return frames.ToArray();
        }

        Sprite LoadSprite(string path, float worldHeight)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (!texture)
            {
                Sprite imported = Resources.Load<Sprite>(path);
                if (imported) texture = imported.texture;
            }
            if (!texture) return null;
            int width = texture.width, height = texture.height;
            Rect rect = new Rect(0, 0, width, height);
            if (texture.isReadable)
            {
                Color32[] pixels = texture.GetPixels32();
                int x0 = width, x1 = 0, y0 = height, y1 = 0;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    if (pixels[x + y * width].a < 35) continue;
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
                if (x1 > x0 && y1 > y0) rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            }
            // Billboard cards need four vertices, never runtime tight-outline tessellation.
            Sprite result = Sprite.Create(texture, rect, new Vector2(.5f, 0), rect.height / worldHeight, 0, SpriteMeshType.FullRect);
            result.name = path; return result;
        }

        void BuildPlayer()
        {
            player = NewRoot("Player · island keeper", actorRoot);
            playerShadow = Cylinder("Player contact shadow", player, new Vector3(0, .026f, 0), new Vector3(.27f, .007f, .21f), materials["teal"]).transform;
            playerBillboard = NewRoot("Keeper artwork", player);
            playerSprite = playerBillboard.gameObject.AddComponent<SpriteRenderer>(); playerSprite.sharedMaterial = artistKeeper ? artistFrontMaterial : heroMaterial;
            playerSprite.sprite = heroFront.Length > 0 ? heroFront[0] : heroIdle.Length > 0 ? heroIdle[0] : null;
            playerSprite.sortingOrder = 3;
            carriedVisual = NewRoot("Carried item", player);
            PlayerVisual = player.gameObject.AddComponent<HabitatPlayerVisual>();
            PlayerVisual.Initialize();
            playerBillboard.gameObject.SetActive(!PlayerVisual.IsModelActive);
        }

        Sprite MakeHeroSprite(int facing)
        {
            // A tiny four-way explorer fallback; imported artist frames take precedence.
            Texture2D tex = new Texture2D(64, 96, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color32[] px = new Color32[64 * 96];
            Color32 outline = new Color32(53, 68, 65, 255), skin = new Color32(249, 213, 167, 255), teal = new Color32(53, 145, 139, 255), cream = new Color32(251, 227, 159, 255), brown = new Color32(106, 77, 52, 255), scarf = new Color32(239, 129, 83, 255);
            PaintRect(px, 64, 16, 5, 13, 12, outline); PaintRect(px, 64, 36, 5, 13, 12, outline);
            PaintRect(px, 64, 20, 14, 10, 22, brown); PaintRect(px, 64, 34, 14, 10, 22, brown);
            PaintEllipse(px, 64, 32, 41, 18, 23, outline); PaintEllipse(px, 64, 32, 42, 15, 21, teal);
            PaintRect(px, 64, 25, 30, 5, 22, cream); PaintRect(px, 64, 35, 30, 5, 22, cream);
            PaintEllipse(px, 64, 12, 40, 6, 10, outline); PaintEllipse(px, 64, 12, 41, 4, 8, skin);
            PaintEllipse(px, 64, 51, 40, 6, 10, outline); PaintEllipse(px, 64, 51, 41, 4, 8, skin);
            PaintEllipse(px, 64, 32, 66, 18, 20, outline); PaintEllipse(px, 64, 32, 66, 15, 17, skin);
            if (facing == 1) PaintEllipse(px, 64, 32, 69, 15, 17, brown);
            else
            {
                int shift = facing == 2 ? -7 : facing == 3 ? 7 : 0;
                PaintRect(px, 64, 23 + shift, 66, 4, 5, outline); PaintRect(px, 64, 38 + shift, 66, 4, 5, outline);
                PaintRect(px, 64, 29 + shift, 58, 7, 2, brown);
            }
            PaintRect(px, 64, 17, 51, 30, 6, scarf); PaintRect(px, 64, 40, 43, 7, 12, scarf);
            PaintEllipse(px, 64, 32, 81, 21, 11, outline); PaintEllipse(px, 64, 32, 82, 18, 8, cream);
            PaintRect(px, 64, 7, 74, 50, 5, outline); PaintRect(px, 64, 8, 76, 48, 5, cream);
            PaintRect(px, 64, 18, 82, 28, 4, brown);
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 96), new Vector2(.5f, 0), 100);
        }

        static void PaintRect(Color32[] px, int width, int x, int y, int w, int h, Color32 c)
        { for (int j = Mathf.Max(0, y); j < Mathf.Min(96, y + h); j++) for (int i = Mathf.Max(0, x); i < Mathf.Min(width, x + w); i++) px[i + j * width] = c; }
        static void PaintEllipse(Color32[] px, int width, int cx, int cy, int rx, int ry, Color32 c)
        { for (int y = Mathf.Max(0, cy - ry); y <= Mathf.Min(95, cy + ry); y++) for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(width - 1, cx + rx); x++) if ((float)(x - cx) * (x - cx) / (rx * rx) + (float)(y - cy) * (y - cy) / (ry * ry) <= 1) px[x + y * width] = c; }

        ActorVisual CreateAnimal(Animal a)
        {
            ActorVisual v = new ActorVisual { animal = a, phase = Hash(a.id, 16) * 6.28f };
            v.root = NewRoot(HabitatRules.Name(a.species) + " " + a.id, actorRoot);
            v.body = NewRoot("Resident artwork", v.root);
            v.shadow = Cylinder("Contact shadow", v.root, new Vector3(0, .018f, 0), new Vector3(.32f, .005f, .22f), materials["teal"]).transform;
            v.frames = animalSprites[a.species];
            float modelHeight = 0f;
            if (a.species == Species.Shrimp && CreateShrimpModel(v, out modelHeight))
                v.model = true;
            else if(a.species==Species.Goldfish && Resources.Load<GameObject>("Models/Goldfish/Goldfish") is GameObject fishPrefab)
            {
                var fish=Instantiate(fishPrefab,v.body,false);fish.name="Goldfish · delivered rig";
                v.fish=fish.AddComponent<HabitatGoldfishVisual>();v.model=true;
            }
            else if(a.species==Species.Jellyfish && Resources.Load<GameObject>("Models/Jellyfish/Jellyfish") is GameObject jellyfishPrefab)
            {
                var jellyfish=Instantiate(jellyfishPrefab,v.body,false);jellyfish.name="Jellyfish · delivered rig";
                v.jellyfish=jellyfish.GetComponent<HabitatJellyfishVisual>();v.model=true;
            }
            else if(a.species==Species.Crab && Resources.Load<GameObject>("Models/HermitCrab/HermitCrab") is GameObject crabPrefab)
            {
                var crab=Instantiate(crabPrefab,v.body,false);crab.name="Hermit crab · delivered rig";
                v.crab=crab.AddComponent<HabitatCrabVisual>();v.model=true;
            }
            else if (v.frames.Length > 0)
            {
                v.sprite = v.body.gameObject.AddComponent<SpriteRenderer>(); v.sprite.sharedMaterial = v.frames[0].name.StartsWith("FigJam ", StringComparison.Ordinal) ? conceptArtMaterial : heroMaterial;
                v.sprite.sprite = v.frames[0]; v.sprite.sortingOrder = 2;
            }
            else BuildAnimalBody(v.body, a.species);
            // Habitat details remain in selection/ground overlays; residents have no head bars.
            v.root.position = AnimalPosition(state, a);
            if (v.shrimp) v.shrimp.ResetVisual(v.root.position, a.carried);
            return v;
        }

        bool CreateShrimpModel(ActorVisual visual, out float height)
        {
            height = 0f;
            if (!shrimpPrefab) shrimpPrefab = Resources.Load<GameObject>("Models/AnimatedShrimp/Shrimp");
            // Legacy static shrimp is archived outside Resources; ship the animated model.
            if (!shrimpPrefab) return false;
            // The imported prefab is uniformly scaled, bottom-centered and faces +Z.
            // Keep its source texture materials; the terrain helper replaces them.
            GameObject model = Instantiate(shrimpPrefab, visual.body, false);
            model.name = "Meshy shrimp · animated model";
            model.transform.localScale *= ShrimpGroundScale;
            visual.shrimp = model.GetComponent<HabitatShrimpVisual>();
            if (visual.shrimp) visual.shrimp.Initialize();
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody rigidbody in model.GetComponentsInChildren<Rigidbody>(true))
            { rigidbody.isKinematic = true; rigidbody.useGravity = false; }
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                // Meshy's packed map has a median metallic channel near 0.5.
                // Reduce that multiplier for a shell, retaining the source color,
                // packed texture, roughness and opaque shader on the shared asset.
                Material[] slots = renderer.sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    if (!slots[slot] || !slots[slot].HasProperty("metallicFactor")) continue;
                    MaterialPropertyBlock properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties, slot);
                    properties.SetFloat("metallicFactor", ShrimpMetallicFactor);
                    renderer.SetPropertyBlock(properties, slot);
                }
                if (!renderer.enabled) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds)
            {
                model.SetActive(false); Destroy(model);
                return false;
            }
            height = bounds.size.y;
            if (visual.shrimp) height = Mathf.Max(height, visual.shrimp.AnimationBounds.max.y * ShrimpGroundScale);
            return true;
        }

        void BuildAnimalBody(Transform p, Species species)
        {
            switch (species)
            {
                case Species.Jellyfish:
                    Sphere("Lavender bell", p, new Vector3(0, .26f, 0), new Vector3(.48f, .35f, .43f), materials["lavender"]);
                    Sphere("Bell rim", p, new Vector3(0, .13f, 0), new Vector3(.49f, .075f, .44f), materials["rose"]);
                    for (int i = 0; i < 5; i++) Capsule("Tentacle", p, new Vector3(-.16f + i * .08f, .035f, 0), new Vector3(.035f, .10f, .035f), materials[i % 2 == 0 ? "lavender" : "rose"]);
                    Face(p, new Vector3(0, .24f, -.2f), .12f);
                    break;
                case Species.Shrimp:
                    for (int i = 0; i < 4; i++) Sphere("Shrimp segment", p, new Vector3(.12f - i * .066f, .13f + Mathf.Sin(i * .7f) * .08f, i * .035f), new Vector3(.17f - i * .015f, .19f, .18f), materials[i % 2 == 0 ? "rose" : "orange"]);
                    Sphere("Tail fan", p, new Vector3(-.16f, .13f, .15f), new Vector3(.19f, .05f, .15f), materials["rose"]);
                    Segment("Antenna", p, new Vector3(.11f, .2f, -.06f), new Vector3(.24f, .31f, -.25f), .013f, materials["rose"]);
                    Segment("Antenna", p, new Vector3(.05f, .2f, -.06f), new Vector3(-.08f, .32f, -.26f), .013f, materials["rose"]);
                    Face(p, new Vector3(.08f, .18f, -.1f), .065f);
                    break;
                case Species.Goldfish:
                    Sphere("Fish body", p, new Vector3(0, .16f, -.02f), new Vector3(.35f, .31f, .39f), materials["orange"]);
                    Sphere("Tail fin", p, new Vector3(0, .16f, .25f), new Vector3(.37f, .035f, .25f), materials["yellow"]);
                    Sphere("Left fin", p, new Vector3(-.19f, .15f, 0), new Vector3(.18f, .035f, .19f), materials["yellow"]);
                    Sphere("Right fin", p, new Vector3(.19f, .15f, 0), new Vector3(.18f, .035f, .19f), materials["yellow"]);
                    Face(p, new Vector3(0, .21f, -.19f), .11f);
                    break;
                case Species.Crab:
                    Sphere("Crab carapace", p, new Vector3(0, .15f, 0), new Vector3(.43f, .23f, .31f), materials["rose"]);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int i = 0; i < 3; i++) Segment("Walking leg", p, new Vector3(side * .14f, .1f, -.08f + i * .09f), new Vector3(side * .29f, .02f, -.1f + i * .14f), .028f, materials["rose"]);
                        Sphere("Pincer", p, new Vector3(side * .28f, .17f, -.18f), new Vector3(.14f, .14f, .17f), materials["orange"]);
                        Segment("Eye stalk", p, new Vector3(side * .1f, .21f, -.1f), new Vector3(side * .1f, .31f, -.13f), .025f, materials["rose"]);
                    }
                    Face(p, new Vector3(0, .31f, -.135f), .10f);
                    break;
                case Species.Turtle:
                    Sphere("Turtle shell", p, new Vector3(0, .18f, .03f), new Vector3(.47f, .3f, .49f), materials["grassDark"]);
                    Sphere("Shell center", p, new Vector3(0, .30f, .035f), new Vector3(.25f, .07f, .27f), materials["grassLight"]);
                    Sphere("Head", p, new Vector3(0, .11f, -.29f), new Vector3(.21f, .19f, .22f), materials["mint"]);
                    for (int i = 0; i < 4; i++) Sphere("Flipper", p, new Vector3(i % 2 == 0 ? -.23f : .23f, .055f, i < 2 ? -.12f : .20f), new Vector3(.19f, .065f, .16f), materials["teal"]);
                    Face(p, new Vector3(0, .135f, -.375f), .06f);
                    break;
                case Species.Starfish:
                    Sphere("Star center", p, new Vector3(0, .08f, 0), new Vector3(.25f, .15f, .25f), materials["orange"]);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2 / 5;
                        Transform arm = Sphere("Star arm", p, new Vector3(Mathf.Sin(a) * .14f, .07f, Mathf.Cos(a) * .14f), new Vector3(.13f, .1f, .32f), materials["orange"]).transform;
                        arm.localRotation = Quaternion.Euler(0, i * 72, 0);
                        Sphere("Star spot", p, new Vector3(Mathf.Sin(a) * .15f, .126f, Mathf.Cos(a) * .15f), Vector3.one * .045f, materials["yellow"]);
                    }
                    Face(p, new Vector3(0, .13f, -.08f), .063f);
                    break;
            }
        }

        void Face(Transform p, Vector3 origin, float space)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 pt = origin + new Vector3(i * space, 0, -.012f);
                Sphere("Eye", p, pt, new Vector3(.035f, .045f, .025f), materials["dark"]);
                Sphere("Eye glint", p, pt + new Vector3(-.005f, .009f, -.012f), Vector3.one * .011f, materials["white"]);
            }
        }

        /// <summary>
        /// Animate a candidate state without committing game rules. Only this exact
        /// candidate reference bypasses replacement snapping; Undo/load still snap.
        /// </summary>
        public void BeginGridStep(HabitatState before, HabitatState destination, HabitatPlayerMotion motion)
        {
            if (before == null || destination == null) throw new ArgumentNullException("Grid step states must be present");
            if (before.width != destination.width || before.depth != destination.depth)
                throw new InvalidOperationException("A grid step cannot replace the map layout");
            if (gridStepActive && !GridStepComplete)
                throw new InvalidOperationException("Wait for the current grid step before beginning another");
            if (!world) Initialize(before);
            CancelGridStep();
            gridStepDestination = destination;
            gridStepMotion = motion;
            gridStepPlayerStart = player.position;
            playerDestination = PlayerPosition(destination);
            gridStepDrop = Mathf.Max(0f, gridStepPlayerStart.y - playerDestination.y);
            gridStepActive = true;
            gridStepAwaitingTerrain = true;
            gridStepFallStarted = false;
            GridStepComplete = false;
            playerFallSpeed = 0f;
            SetPlayerMotion(motion);

            // Tide can convert other crates in place. Such cells are not movement
            // sources/destinations: only a removed movable paired with an added
            // movable is slid. Crate/Float share one movement family.
            var sources = new List<int>();
            var destinations = new List<int>();
            for (int i = 0; i < before.tiles.Length; i++)
            {
                bool from = IsSlidingProp(before.tiles[i].prop);
                bool to = IsSlidingProp(destination.tiles[i].prop);
                if (from && (!to||before.tiles[i].coveredProp==PropKind.Float&&destination.tiles[i].prop==PropKind.Float)) sources.Add(i);
                if (to && (!from||destination.tiles[i].coveredProp==PropKind.Float&&before.tiles[i].prop==PropKind.Float)) destinations.Add(i);
            }
            foreach (int target in destinations)
            {
                int best = -1, shortest = int.MaxValue;
                int riderSource=before.playerZ*before.width+before.playerX;
                int riderTarget=destination.playerZ*destination.width+destination.playerX;
                for (int j = 0; j < sources.Count; j++)
                {
                    int source = sources[j];
                    // Reserve the passenger's exact support even if several
                    // same-family floats move in the same wind tick.
                    if(motion==HabitatPlayerMotion.Ride&&((source==riderSource)!=(target==riderTarget)))continue;
                    if (!SameSlidingFamily(before.tiles[source].prop, destination.tiles[target].prop)) continue;
                    int distance = Mathf.Abs(source % before.width - target % before.width)
                        + Mathf.Abs(source / before.width - target / before.width);
                    if (distance < shortest) { shortest = distance; best = j; }
                }
                if (best < 0) continue;
                int sourceIndex = sources[best];
                sources.RemoveAt(best);
                Transform sourceRoot = tiles != null && sourceIndex < tiles.Length ? tiles[sourceIndex].prop : null;
                gridStepProps.Add(new GridPropMotion
                {
                    destinationIndex = target,
                    start = sourceRoot ? sourceRoot.position : PropSurfacePosition(before, sourceIndex),
                    carriesPlayer=motion==HabitatPlayerMotion.Ride&&sourceIndex==riderSource
                });
            }
        }

        public void CancelGridStep()
        {
            // All moved objects are existing candidate prop roots, never clones.
            // Put their local presentation back before a new terrain/state rebuild.
            foreach (GridPropMotion prop in gridStepProps)
                if (prop.root) prop.root.position = prop.end;
            gridStepProps.Clear();
            gridStepDestination = null;
            gridStepActive = gridStepAwaitingTerrain = gridStepFallStarted = false;
            gridStepDrop = playerFallSpeed = 0f;
            GridStepComplete = true;
            if (PlayerVisual && PlayerVisual.IsFalling) PlayerVisual.ResetVisual(state != null ? PlayerFacing(state) : Vector3.forward);
        }

        static bool IsSlidingProp(PropKind kind)
            => kind == PropKind.Crate || kind == PropKind.Float || kind == PropKind.Raft;

        static bool SameSlidingFamily(PropKind a, PropKind b)
            => a == b || ((a == PropKind.Crate || a == PropKind.Float) && (b == PropKind.Crate || b == PropKind.Float));

        static Vector3 PropSurfacePosition(HabitatState s, int index)
        {
            Tile tile = s.tiles[index];
            float y = tile.height <= s.waterLevel ? HabitatRules.WaterSurfaceY(s.waterLevel)+.015f : tile.height * Step + .045f;
            if(tile.prop==PropKind.Float)y=s.waterLevel*Step+.08f;
            if(tile.prop==PropKind.Raft)y=HabitatRules.WaterSurfaceY(s.waterLevel);
            if(tile.coveredProp==PropKind.Float)y=(s.waterLevel+1)*Step+.045f;
            return new Vector3(index % s.width, y, index / s.width);
        }

        void BindGridStepProps()
        {
            if (!gridStepAwaitingTerrain) return;
            foreach (GridPropMotion prop in gridStepProps)
            {
                prop.root = tiles[prop.destinationIndex].prop;
                if (!prop.root) throw new InvalidOperationException("Grid step destination has no prop visual");
                prop.end = prop.root.position;
                prop.root.position = prop.start;
            }
            gridStepAwaitingTerrain = false;
        }

        void RenderGridStep(float dt, Vector3 target)
        {
            float speed = gridStepMotion == HabitatPlayerMotion.Push || gridStepMotion == HabitatPlayerMotion.Pull || gridStepMotion == HabitatPlayerMotion.Ride
                ? HabitatPlayerVisual.HandlingSpeed : HabitatPlayerVisual.WalkSpeed;
            if (PlayerVisual && !PlayerVisual.IsFalling && !PlayerVisual.IsLanding)
                PlayerVisual.SetMotion(gridStepMotion);
            Vector3 flat = Vector3.MoveTowards(new Vector3(player.position.x, 0f, player.position.z),
                new Vector3(target.x, 0f, target.z), speed * dt);
            float height;
            if (gridStepDrop > .08f && gridStepMotion != HabitatPlayerMotion.Ride)
            {
                // Take a visible step off the edge before gravity begins. This
                // avoids sliding through the departing tile's vertical wall.
                float distance = Vector2.Distance(new Vector2(gridStepPlayerStart.x, gridStepPlayerStart.z), new Vector2(target.x, target.z));
                float travelled = Vector2.Distance(new Vector2(gridStepPlayerStart.x, gridStepPlayerStart.z), new Vector2(flat.x, flat.z));
                if (!gridStepFallStarted && (distance < .01f || travelled >= Mathf.Min(.42f, distance * .45f)))
                {
                    gridStepFallStarted = true;
                    if (PlayerVisual) PlayerVisual.NotifyFall(gridStepDrop);
                }
                if (gridStepFallStarted)
                {
                    playerFallSpeed += 12f * dt;
                    height = Mathf.Max(target.y, player.position.y - playerFallSpeed * dt);
                    if (height <= target.y + .005f && PlayerVisual) PlayerVisual.NotifyLanded();
                }
                else height = gridStepPlayerStart.y;
            }
            else height = Mathf.MoveTowards(player.position.y, target.y, HabitatPlayerVisual.WalkSpeed * dt);
            player.position = new Vector3(flat.x, height, flat.z);

            foreach (GridPropMotion prop in gridStepProps)
            {
                if (prop.complete || !prop.root) continue;
                Vector3 position = prop.root.position;
                Vector3 horizontal = Vector3.MoveTowards(new Vector3(position.x, 0f, position.z),
                    new Vector3(prop.end.x, 0f, prop.end.z), HabitatPlayerVisual.HandlingSpeed * dt);
                float propY;
                if (prop.start.y > prop.end.y + .08f)
                {
                    float travel = Vector2.Distance(new Vector2(prop.start.x, prop.start.z), new Vector2(horizontal.x, horizontal.z));
                    if (travel >= .42f) prop.falling = true;
                    if (prop.falling)
                    {
                        prop.fallingSpeed += 12f * dt;
                        propY = Mathf.Max(prop.end.y, position.y - prop.fallingSpeed * dt);
                    }
                    else propY = prop.start.y;
                }
                else propY = Mathf.MoveTowards(position.y, prop.end.y, HabitatPlayerVisual.HandlingSpeed * dt);
                prop.root.position = new Vector3(horizontal.x, propY, horizontal.z);
                prop.complete = Vector3.SqrMagnitude(prop.root.position - prop.end) < .000001f;
                if (prop.complete) prop.root.position = prop.end;
            }
            if(gridStepMotion==HabitatPlayerMotion.Ride)
            {
                // Ride the actual animated support, including vertical motion;
                // the character must not walk ahead of its float or run in place.
                foreach(GridPropMotion prop in gridStepProps)
                    if(prop.carriesPlayer&&prop.root) { player.position=prop.root.position+(target-prop.end);break; }
            }
        }

        void UpdateGridStepCompletion()
        {
            if (!gridStepActive || GridStepComplete || gridStepAwaitingTerrain) return;
            if (Vector3.SqrMagnitude(player.position - playerDestination) > .000001f) return;
            if (PlayerVisual && (PlayerVisual.IsFalling || PlayerVisual.IsLanding)) return;
            foreach (GridPropMotion prop in gridStepProps) if (!prop.complete) return;
            GridStepComplete = true;
        }

        public bool BeginCinematic()
        {
            if (!player || !GameCamera || cinematicActive || (gridStepActive && !GridStepComplete)) return false;
            CancelGridStep();
            if (PlayerVisual) PlayerVisual.CancelEquipmentAction();
            savedPlayerPosition = player.position;
            savedPlayerVisible = player.gameObject.activeSelf;
            savedCameraPosition = GameCamera.transform.position;
            savedCameraRotation = GameCamera.transform.rotation;
            savedCameraSize = GameCamera.orthographicSize;
            savedCameraOrthographic = GameCamera.orthographic;
            savedCameraBackground = GameCamera.backgroundColor;
            savedCameraClearFlags = GameCamera.clearFlags;
            savedAnimatorSpeed = PlayerVisual && PlayerVisual.Animator ? PlayerVisual.Animator.speed : 1f;
            cinematicActive = true; cinematicPaused = false;
            if (selection) selection.gameObject.SetActive(false);
            return true;
        }

        public void SetCinematicPose(Vector3 position, Vector3 facing, bool visible = true)
        {
            if (!cinematicActive || !player) return;
            player.gameObject.SetActive(visible);
            player.position = playerDestination = position;
            if (PlayerVisual && visible && !cinematicPaused) PlayerVisual.Render(Time.unscaledDeltaTime, facing);
        }

        public void SetCinematicPaused(bool paused)
        {
            if (!cinematicActive) return;
            cinematicPaused = paused;
            if (PlayerVisual && PlayerVisual.Animator) PlayerVisual.Animator.speed = paused ? 0f : savedAnimatorSpeed;
        }

        public void SetCinematicCamera(Vector3 target, Quaternion rotation, float size)
        {
            if (!cinematicActive || !GameCamera) return;
            GameCamera.orthographic = true;
            GameCamera.orthographicSize = Mathf.Max(.5f, size);
            GameCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 23f, rotation);
            nextVisibilityCheck = 0f;
        }

        public void EndCinematic(bool restorePlayer = true)
        {
            if (!cinematicActive) return;
            cinematicActive = cinematicPaused = false;
            if (player)
            {
                player.gameObject.SetActive(savedPlayerVisible);
                if (restorePlayer) player.position = savedPlayerPosition;
                playerDestination = player.position;
            }
            if (PlayerVisual)
            {
                if (PlayerVisual.Animator) PlayerVisual.Animator.speed = savedAnimatorSpeed;
                PlayerVisual.ResetVisual(state != null ? PlayerFacing(state) : Vector3.forward);
            }
            if (GameCamera)
            {
                GameCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
                GameCamera.orthographic = savedCameraOrthographic;
                GameCamera.orthographicSize = savedCameraSize;
                GameCamera.backgroundColor = savedCameraBackground;
                GameCamera.clearFlags = savedCameraClearFlags;
            }
            framingDirty = true; nextVisibilityCheck = 0f;
        }

        public void SetPlayerMotion(HabitatPlayerMotion motion)
        {
            if (PlayerVisual) PlayerVisual.SetMotion(motion);
        }

        public void NotifyPlayerFall(float dropHeight)
        {
            playerFallSpeed = 0f;
            if (PlayerVisual) PlayerVisual.NotifyFall(dropHeight);
        }

        public bool ResidentsArrived(HabitatState s)
        {
            if(IsPlayerMoving||!GridStepComplete)return false;
            foreach(Animal a in s.animals)
            {
                if(a.carried)continue;
                if(!actors.TryGetValue(a.id,out ActorVisual visual)||!visual.root||Vector3.Distance(visual.root.position,AnimalPosition(s,a))>.02f||(visual.crab&&visual.crab.IsReacting))return false;
            }
            return true;
        }

        public void Render(HabitatState s, Tuning tuning, bool overlays)
        {
            if (!world) Initialize(s);
            bool layoutChanged = oldWidth != s.width || oldDepth != s.depth;
            if (gridStepActive && !ReferenceEquals(gridStepDestination, s)) CancelGridStep();
            bool previewingStep = gridStepActive && ReferenceEquals(gridStepDestination, s);
            bool stateReplaced = !ReferenceEquals(state, s) && !previewingStep;
            state = s; EnsureTerrain(s);
            var game = HabitatGame.Instance;
            if (PlayerVisual) PlayerVisual.RefreshEquipment(s, game ? game.tool : -1,
                cinematicActive || (game && (game.StoryPlaying || game.IsRecovering)),
                game && game.Fishing && game.Fishing.IsOpen);
            if (cinematicActive)
            {
                UpdateVisibleTerrain(false);
                if (selection) selection.gameObject.SetActive(false);
                return;
            }
            if (previewingStep) BindGridStepProps();
            if (layoutChanged) { ResetCameraForLevel(); RefreshDistantDecor(); }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .08f), time = Time.unscaledTime;
            UpdateTrapVisuals(s, dt, stateReplaced);
            UpdateBellowsVisuals(s, dt, false);
            UpdateTideEngineVisuals(s, dt, false);
            UpdateCamera(false);
            UpdateVisibleTerrain(false);
            Vector3 playerTarget = PlayerPosition(s);
            playerDestination = playerTarget;
            float move = Vector3.Distance(player.position, playerTarget);
            if (recoveryPresentationActive)
                RenderRecoveryPresentation(dt);
            else if (previewingStep && !GridStepComplete)
                RenderGridStep(dt, playerTarget);
            else if (s.onHarbor || stateReplaced)
            {
                player.position = playerTarget;
                playerFallSpeed = 0f;
                if (stateReplaced && PlayerVisual) PlayerVisual.ResetVisual(PlayerFacing(s));
            }
            else
            {
                float speed = PlayerVisual ? PlayerVisual.TravelSpeed : HabitatPlayerVisual.WalkSpeed;
                Vector3 flat = Vector3.MoveTowards(new Vector3(player.position.x, 0f, player.position.z),
                    new Vector3(playerTarget.x, 0f, playerTarget.z), speed * dt);
                float height;
                if (PlayerVisual && PlayerVisual.IsFalling)
                {
                    playerFallSpeed += 12f * dt;
                    height = Mathf.Max(playerTarget.y, player.position.y - playerFallSpeed * dt);
                    if (height <= playerTarget.y + .005f) PlayerVisual.NotifyLanded();
                }
                else height = Mathf.MoveTowards(player.position.y, playerTarget.y, HabitatPlayerVisual.WalkSpeed * dt);
                player.position = new Vector3(flat.x, height, flat.z);
            }
            bool modelActive = PlayerVisual && PlayerVisual.IsModelActive;
            if (playerBillboard.gameObject.activeSelf == modelActive)
                playerBillboard.gameObject.SetActive(!modelActive);
            if (modelActive)
                PlayerVisual.Render(dt, recoveryPresentationActive ? recoveryFacing : PlayerFacing(s));
            else
            {
                playerBillboard.rotation = GameCamera.transform.rotation;
                playerBillboard.localPosition = new Vector3(0, move > .08f ? Mathf.Abs(Mathf.Sin(time * 14f)) * .048f : Mathf.Sin(time * 2) * .009f, 0);
                Sprite[] heroFrames = SelectHero(s);
                if (heroFrames.Length > 0)
                {
                    Sprite frame = heroFrames[Mathf.FloorToInt(time * 5) % heroFrames.Length];
                    if (playerSprite.sprite != frame)
                    {
                        playerSprite.sprite = frame;
                        if (artistKeeper) playerSprite.sharedMaterial = frame == heroFront[0] ? artistFrontMaterial : conceptArtMaterial;
                    }
                }
                if (artistKeeper)
                {
                    Vector3 facing = PlayerFacing(s);
                    bool mirror = Vector3.Dot(facing, GameCamera.transform.right) > .1f;
                    playerBillboard.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
                    playerSprite.flipX = false;
                }
                else playerSprite.flipX = heroRight.Length == 0 && s.facingX > 0;
            }
            if (lastCarried != s.carryingProp)
            {
                lastCarried = s.carryingProp; ClearChildren(carriedVisual);
                carriedVisual.localPosition = new Vector3(0, 1.0f, 0); carriedVisual.localScale = Vector3.one * .36f;
                if (lastCarried != PropKind.None) BuildProp(carriedVisual, lastCarried, 0, 0);
            }
            if (modelActive) carriedVisual.position = PlayerVisual.CarryAnchor.position;
            else carriedVisual.localPosition = Vector3.up;
            bool updateReports = time >= nextReport;
            if (updateReports) nextReport = time + .20f;
            for (int i = 0; i < s.animals.Count; i++)
            {
                Animal a = s.animals[i];
                if (actors.TryGetValue(a.id, out ActorVisual v) && v.animal.species != a.species)
                { Destroy(v.root.gameObject); actors.Remove(a.id); v = null; }
                if (v == null) { v = CreateAnimal(a); actors.Add(a.id, v); }
                v.animal = a;
                Vector3 target = a.carried
                    ? (modelActive ? PlayerVisual.CarryAnchor.position : player.position + new Vector3(0, .97f, 0))
                    : AnimalPosition(s, a);
                Vector3 previousPosition = v.root.position;
                v.root.position = stateReplaced ? target : Vector3.Lerp(v.root.position, target, 1 - Mathf.Exp(-dt * 9));
                float bob = Mathf.Sin(time * 2.3f + v.phase) * .025f;
                v.body.localPosition = new Vector3(0, v.crab || v.jellyfish ? 0f:.035f + (v.shrimp ? 0f : bob), 0);
                if (v.sprite)
                {
                    v.body.rotation = GameCamera.transform.rotation;
                    v.sprite.sprite = v.frames[Mathf.FloorToInt(time * 5 + v.phase) % v.frames.Length];
                    if (Mathf.Abs(target.x - v.lastTarget.x) > .01f) v.body.localScale = new Vector3(target.x < v.lastTarget.x ? -1 : 1, 1, 1);
                }
                else if (v.model)
                {
                    // Ground residents are easier to read; carried residents keep
                    // the original footprint above the keeper's head.
                    // A captured resident fits inside the imported cage. This is
                    // display scale only; taking it out restores its usual size.
                    bool inTrap = !a.carried && HabitatRules.TileAt(s, a.x, a.z).trap;
                    v.body.localScale = Vector3.one * (a.carried ? ShrimpCarryScale / ShrimpGroundScale : inTrap ? .48f : 1f);
                    // The jellyfish origin is under its bell, not under its tentacles.
                    // In water keep that authored anchor; on solid surfaces lift the full rig.
                    if(v.jellyfish && (a.carried || inTrap || HabitatRules.Height(s,a.x,a.z)>s.waterLevel))
                        v.body.localPosition += Vector3.up * (v.jellyfish.GroundClearance*v.body.localScale.y);
                    if (inTrap) v.body.localPosition += Vector3.up * .06f;
                    Vector3 direction = a.carried ? PlayerFacing(s) : v.root.position - previousPosition;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > .000001f)
                        v.body.rotation = Quaternion.Slerp(v.body.rotation, Quaternion.LookRotation(direction, Vector3.up), 1 - Mathf.Exp(-dt * 12f));
                }
                else v.body.localRotation = Quaternion.Euler(0, Mathf.Sin(time + v.phase) * 7f, Mathf.Sin(time * 2 + v.phase) * 3);
                if (v.shrimp)
                {
                    if (stateReplaced) v.shrimp.ResetVisual(v.root.position, a.carried);
                    else v.shrimp.Render(dt, a.carried, v.root.position);
                }
                if(v.fish)v.fish.Render(dt,v.root.position,previousPosition,stateReplaced);
                if(v.jellyfish)v.jellyfish.Render(dt,v.root.position,previousPosition,stateReplaced,a.carried);
                if(v.crab)
                {
                    bool startled=false;
                    if(!stateReplaced&&(Mathf.Abs(target.x-v.lastTarget.x)+Mathf.Abs(target.z-v.lastTarget.z))>.5f)
                        foreach(Animal neighbor in s.animals)
                            if(neighbor.species==Species.Shrimp&&Mathf.Abs(neighbor.x-v.lastTarget.x)+Mathf.Abs(neighbor.z-v.lastTarget.z)<=1.01f){startled=true;break;}
                    v.crab.Render(dt,v.root.position,previousPosition,stateReplaced,startled,a.shellShed);
                }
                v.lastTarget = target;
                v.shadow.gameObject.SetActive(!a.carried);
            }
            staleIds.Clear();
            foreach (KeyValuePair<int, ActorVisual> pair in actors)
            {
                bool present = false; for (int i = 0; i < s.animals.Count; i++) if (s.animals[i].id == pair.Key) { present = true; break; }
                if (!present) staleIds.Add(pair.Key);
            }
            for (int i = 0; i < staleIds.Count; i++) { Destroy(actors[staleIds[i]].root.gameObject); actors.Remove(staleIds[i]); }
            if (updateReports) UpdateOverlays(s, tuning, overlays);
            else if (!overlays) for (int i = 0; i < tiles.Length; i++) if (tiles[i].overlay.gameObject.activeSelf) tiles[i].overlay.gameObject.SetActive(false);
            bool selected = !s.onHarbor && HabitatRules.Inside(s, s.selectedX, s.selectedZ);
            selection.gameObject.SetActive(selected);
            if (selected)
            {
                TileVisual cell = tiles[s.selectedX + s.selectedZ * s.width];
                selection.position = new Vector3(s.selectedX, Mathf.Max(cell.top, HabitatRules.Height(s, s.selectedX, s.selectedZ) * Step) + .055f, s.selectedZ);
                selection.localScale = Vector3.one * (1 + Mathf.Sin(time * 4f) * .012f);
            }
            for (int i = spinners.Count - 1; i >= 0; i--)
            {
                if (!spinners[i].t) { spinners.RemoveAt(i); continue; }
                spinners[i].t.Rotate(spinners[i].axis, spinners[i].speed * dt, Space.Self);
            }
            for (int i = 0; i < seaGlints.Count; i++)
            {
                Transform glint = seaGlints[i]; Vector3 sc = glint.localScale; sc.z = .019f + .012f * Mathf.Sin(time * 1.1f + i); glint.localScale = sc;
            }
            for (int i = artDecorations.Count - 1; i >= 0; i--)
            {
                ArtDecoration decoration = artDecorations[i];
                if (!decoration.t) { artDecorations.RemoveAt(i); continue; }
                decoration.t.rotation = GameCamera.transform.rotation;
                decoration.t.localPosition = decoration.localPosition + Vector3.up * (Mathf.Sin(time * 1.3f + i) * decoration.bob);
            }
            UpdateGridStepCompletion();
        }

        void UpdateOverlays(HabitatState s, Tuning tuning, bool show)
        {
            for (int z = 0; z < s.depth; z++) for (int x = 0; x < s.width; x++)
            {
                TileVisual v = tiles[x + z * s.width];
                bool protectedTile = show && HabitatRules.IsProtected(s, x, z, tuning.minHighland);
                overlayProbe.x = x; overlayProbe.z = z;
                bool danger = show && !protectedTile && HabitatRules.IsThreatened(s, overlayProbe, tuning);
                v.overlay.gameObject.SetActive(show && (protectedTile || danger));
                if (protectedTile || danger) for (int k = 0; k < v.overlayParts.Length; k++) v.overlayParts[k].sharedMaterial = materials[protectedTile ? "safe" : "danger"];
            }
        }

        Sprite[] SelectHero(HabitatState s)
        {
            if (artistKeeper)
            {
                // Only the supplied front and back poses are claimed: side movement mirrors the front.
                return s.facingX == 0 && s.facingZ > 0 ? heroBack : heroFront;
            }
            Sprite[] frames = s.facingZ > 0 ? heroBack : s.facingZ < 0 ? heroFront : s.facingX < 0 ? heroLeft : heroRight;
            if (frames.Length > 0) return frames;
            if (heroIdle.Length > 0) return heroIdle;
            return heroFront;
        }

        Vector3 PlayerPosition(HabitatState s)
        {
            if (s.onHarbor) return Harbor.WorldPosition(s);
            return new Vector3(s.playerX, Mathf.Max(HabitatRules.Height(s, s.playerX, s.playerZ) * Step + .055f, HabitatRules.WaterSurfaceY(s.waterLevel) + .025f), s.playerZ);
        }

        Vector3 PlayerFacing(HabitatState s)
            => s.onHarbor && Harbor.Ready ? Harbor.WorldFacing(s) : new Vector3(s.facingX, 0, s.facingZ);

        Vector3 AnimalPosition(HabitatState s, Animal a)
        {
            Tile t = HabitatRules.TileAt(s, a.x, a.z);
            float h = HabitatRules.Height(s, a.x, a.z);
            float y = t == null ? .05f : h <= s.waterLevel ? HabitatRules.WaterSurfaceY(s.waterLevel) + .027f : h * Step + .055f;
            return new Vector3(a.x, y, a.z);
        }

        static Quaternion DockRotation(HabitatState s)
        {
            // Design directions: 0 north/+Z, 1 east/+X, 2 south/-Z, 3 west/-X.
            // The dock model extends along its local -Z axis.
            int direction = s.dockRotation < 0 ? (s.dockX <= 1 ? 3 : 2) : s.dockRotation % 4;
            return Quaternion.Euler(0, (direction + 2) * 90, 0);
        }

        void ResetCameraForLevel()
        {
            cameraYaw = DefaultCameraYaw; cameraSize = LocalCameraSize;
            followPlayer = state.authoredLevel || state.width > 11 || state.depth > 11;
            overviewCamera = false; framingDirty = true;
            RecalculateFraming();
            cameraTarget = followPlayer ? PlayerCameraTarget() : new Vector3((state.width - 1) * .5f, .35f, (state.depth - 1) * .5f + .45f);
            cameraTargetSmooth = cameraTarget;
            nextVisibilityCheck = 0f;
        }

        Vector3 PlayerCameraTarget()
        {
            float yaw = cameraYaw * Mathf.Deg2Rad;
            Vector3 boardForward = new Vector3(-Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            return (player ? player.position : PlayerPosition(state)) + Vector3.up * .35f + boardForward * .45f;
        }

        void CameraAxes(out Vector3 right, out Vector3 up)
        {
            float yaw = cameraYaw * Mathf.Deg2Rad, pitch = DefaultCameraPitch * Mathf.Deg2Rad;
            right = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
            up = new Vector3(-Mathf.Sin(yaw) * Mathf.Sin(pitch), Mathf.Cos(pitch), Mathf.Cos(yaw) * Mathf.Sin(pitch));
        }

        static float ProjectedExtent(Vector3 axis, Vector3 extent)
        { return Mathf.Abs(axis.x) * extent.x + Mathf.Abs(axis.y) * extent.y + Mathf.Abs(axis.z) * extent.z; }

        void RecalculateFraming()
        {
            if (state == null || !GameCamera) return;
            float minY = HabitatRules.WaterSurfaceY(state.waterLevel), maxY = minY + 1.3f;
            for (int z = 0; z < state.depth; z++) for (int x = 0; x < state.width; x++)
                maxY = Mathf.Max(maxY, HabitatRules.Height(state, x, z) * Step + 1.2f);
            mapBounds = new Bounds(new Vector3((state.width - 1) * .5f, (minY + maxY) * .5f, (state.depth - 1) * .5f), new Vector3(state.width, maxY - minY, state.depth));
            // Frame the playable island, not the entire decorative harbor footprint.
            if (state.onHarbor) mapBounds.Encapsulate(PlayerWorldPosition);
            if(HabitatLevels.IsHomeHarbor(state)&&Harbor.ModelTransform)
                foreach(var renderer in Harbor.ModelTransform.GetComponentsInChildren<Renderer>())mapBounds.Encapsulate(renderer.bounds);
            CameraAxes(out Vector3 right, out Vector3 up);
            framingAspect = Mathf.Max(.1f, GameCamera.aspect);
            float horizontalFit = ProjectedExtent(right, mapBounds.extents) / (framingAspect * .82f);
            float verticalFit = ProjectedExtent(up, mapBounds.extents) / .80f;
            overviewSize = Mathf.Max(LocalCameraSize, Mathf.Max(horizontalFit, verticalFit) + .3f);
            framingDirty = false;
        }

        void RefreshDistantDecor()
        {
            bool show = !state.authoredLevel && state.width <= 11 && state.depth <= 11;
            foreach (Transform island in distantIslands) if (island) island.gameObject.SetActive(show);
        }

        void UpdateVisibleTerrain(bool force)
        {
            if (!GameCamera || tiles == null) return;
            bool transitioning = (cameraTarget - cameraTargetSmooth).sqrMagnitude > .0025f
                || Mathf.Abs(GameCamera.orthographicSize - cameraSize) > .025f
                || Mathf.Abs(Mathf.DeltaAngle(visibleCameraYaw,cameraYaw)) > .05f;
            if (!force && !transitioning && Time.unscaledTime < nextVisibilityCheck) return;
            nextVisibilityCheck = Time.unscaledTime + .12f;
            bool localCulling = state.authoredLevel || state.width > 11 || state.depth > 11;
            Vector3 right = GameCamera.transform.right, up = GameCamera.transform.up;
            Vector3 cameraPosition = GameCamera.transform.position;
            float horizontal = GameCamera.orthographicSize * GameCamera.aspect + 2f;
            float vertical = GameCamera.orthographicSize + 2f;
            for (int i = 0; i < tiles.Length; i++)
            {
                TileVisual cell = tiles[i];
                bool visible = true;
                if (localCulling)
                {
                    // Conservative terrain/prop bounds plus a two-unit margin keep
                    // fast drags seamless. Inactive roots are skipped by occlusion detection.
                    float bottom = Mathf.Min(-.7f, waterY), top = Mathf.Max(cell.top + 1.6f, waterY + .15f);
                    Vector3 center = cell.root.position + Vector3.up * ((bottom + top) * .5f);
                    Vector3 extent = new Vector3(.7f, (top - bottom) * .5f, .7f);
                    Vector3 offset = center - cameraPosition;
                    visible = Mathf.Abs(Vector3.Dot(offset, right)) <= horizontal + ProjectedExtent(right, extent)
                        && Mathf.Abs(Vector3.Dot(offset, up)) <= vertical + ProjectedExtent(up, extent);
                }
                if (cell.root.gameObject.activeSelf != visible)
                {
                    cell.root.gameObject.SetActive(visible);
                    // The occlusion component drops inactive roots from its registry.
                    // Re-register returning cells so click-through picking stays correct.
                    if (visible && occlusion) occlusion.RegisterCell(cell.root, state, i % state.width, i / state.width);
                }
            }
        }

        void UpdateCamera(bool immediate)
        {
            if (!GameCamera) return;
            if (framingDirty || Mathf.Abs(framingAspect - GameCamera.aspect) > .001f) RecalculateFraming();
            if (overviewCamera) { cameraTarget = mapBounds.center; cameraSize = overviewSize; }
            else if (followPlayer) cameraTarget = PlayerCameraTarget();
            cameraTargetSmooth = immediate ? cameraTarget : Vector3.Lerp(cameraTargetSmooth, cameraTarget, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
            if(immediate){visibleCameraYaw=cameraYaw;cameraYawVelocity=0;}
            else visibleCameraYaw=Mathf.SmoothDampAngle(visibleCameraYaw,cameraYaw,ref cameraYawVelocity,.18f,720f,Time.unscaledDeltaTime);
            float radians = visibleCameraYaw * Mathf.Deg2Rad;
            const float horizontalDistance = 12f;
            Vector3 orbit = new Vector3(Mathf.Sin(radians) * horizontalDistance, Mathf.Tan(DefaultCameraPitch * Mathf.Deg2Rad) * horizontalDistance, -Mathf.Cos(radians) * horizontalDistance);
            GameCamera.transform.position = cameraTargetSmooth + orbit;
            GameCamera.transform.LookAt(cameraTargetSmooth);
            GameCamera.orthographicSize = immediate ? cameraSize : Mathf.Lerp(GameCamera.orthographicSize, cameraSize, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
        }

        public bool Pick(Vector2 screen, out int x, out int z)
        {
            x = z = -1;
            if (!GameCamera) return false;
            if (occlusion && occlusion.isActiveAndEnabled)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                bool preferFront = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
                return occlusion.TryPick(GameCamera, screen, preferFront, out x, out z);
            }
            Ray ray = GameCamera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return false;
            GridHit tile = hit.collider.GetComponent<GridHit>();
            if (!tile) return false;
            x = tile.x; z = tile.z; return true;
        }
        public void FocusOverview()
        {
            if (state == null) return;
            followPlayer = false; overviewCamera = true; framingDirty = true;
            RecalculateFraming(); cameraTarget = mapBounds.center; cameraSize = overviewSize;
            nextVisibilityCheck = 0f;
        }

        public void FocusTutorialTarget(Vector3 goal)
        {
            if(state==null)return;
            followPlayer=false;overviewCamera=false;
            Vector3 origin=PlayerWorldPosition;
            cameraTarget=(origin+goal)*.5f;
            cameraTargetSmooth=cameraTarget;
            cameraSize=Mathf.Clamp(Vector3.Distance(origin,goal)*.42f+2.4f,3.1f,12f);
            nextVisibilityCheck=0f;
        }

        public void FocusPlayer()
        {
            if (state == null) return;
            followPlayer = true; overviewCamera = false; cameraSize = HabitatLevels.IsHomeHarbor(state) ? 4.8f : LocalCameraSize;
            cameraTarget = PlayerCameraTarget(); nextVisibilityCheck = 0f;
        }

        // delta is a mouse drag in screen pixels. The world follows the hand.
        public void Pan(Vector2 delta)
        {
            if (state == null || !GameCamera || delta.sqrMagnitude < .0001f) return;
            followPlayer = false; overviewCamera = false;
            float unitsPerPixel = 2f * GameCamera.orthographicSize / Mathf.Max(1, GameCamera.pixelHeight);
            float yaw = cameraYaw * Mathf.Deg2Rad;
            Vector3 right = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
            Vector3 forward = new Vector3(-Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            cameraTarget -= (right * delta.x + forward * (delta.y / Mathf.Sin(DefaultCameraPitch * Mathf.Deg2Rad))) * unitsPerPixel;
            if (framingDirty) RecalculateFraming();
            cameraTarget.x = Mathf.Clamp(cameraTarget.x, mapBounds.min.x - 1.5f, mapBounds.max.x + 1.5f);
            cameraTarget.z = Mathf.Clamp(cameraTarget.z, mapBounds.min.z - 1.5f, mapBounds.max.z + 1.5f);
            cameraTargetSmooth = cameraTarget; nextVisibilityCheck = 0f;
        }

        public void RotateCamera(float degrees) { cameraYaw = Mathf.Repeat(cameraYaw + degrees, 360f); framingDirty = true; nextVisibilityCheck = 0f; }
        public void Zoom(float amount)
        {
            if (framingDirty) RecalculateFraming();
            overviewCamera = false;
            cameraSize = Mathf.Clamp(cameraSize + amount, 4.5f, Mathf.Max(9f, overviewSize));
            nextVisibilityCheck = 0f;
        }

        static Transform NewRoot(string name, Transform parent)
        { Transform t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 p, Vector3 scale, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>(); if (collider) { collider.enabled = false; Destroy(collider); }
            Renderer renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = m;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            return go;
        }
        static GameObject Box(string name, Transform parent, Vector3 p, Vector3 scale, Material m) { return Primitive(name, PrimitiveType.Cube, parent, p, scale, m); }
        static GameObject Sphere(string name, Transform parent, Vector3 p, Vector3 scale, Material m) { return Primitive(name, PrimitiveType.Sphere, parent, p, scale, m); }
        static GameObject Capsule(string name, Transform parent, Vector3 p, Vector3 scale, Material m) { return Primitive(name, PrimitiveType.Capsule, parent, p, scale, m); }
        static GameObject Cylinder(string name, Transform parent, Vector3 p, Vector3 scale, Material m) { return Primitive(name, PrimitiveType.Cylinder, parent, p, scale, m); }
        static void Segment(string name, Transform parent, Vector3 a, Vector3 b, float width, Material m)
        {
            Transform t = Cylinder(name, parent, (a + b) * .5f, new Vector3(width, Vector3.Distance(a, b) * .5f, width), m).transform;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
        static void Triangle(string name, Transform parent, Vector3 a, Vector3 b, Vector3 c, Material m)
        {
            GameObject g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(parent, false);
            // Give each side its own vertices so opposite normals do not cancel to zero.
            Mesh mesh = new Mesh { vertices = new[] { a, b, c, c, b, a }, triangles = new[] { 0, 1, 2, 3, 4, 5 } }; mesh.RecalculateNormals();
            g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
        static float Hash(int a, int b) { uint n = (uint)(a * 374761393 + b * 668265263); n = (n ^ (n >> 13)) * 1274126177; return (n & 0xffff) / 65535f; }
        static void ClearChildren(Transform p) { for (int i = p.childCount - 1; i >= 0; i--) { GameObject child = p.GetChild(i).gameObject; child.SetActive(false); Destroy(child); } }
    }
}
