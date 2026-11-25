using DuckKing.Gameplay;
using UnityEditor;
using UnityEngine;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Builds a ready-to-use see-saw plank.
    ///
    /// The plank is a real physics body: a Dynamic Rigidbody2D pinned in place by a
    /// HingeJoint2D, so the solver handles tipping and a duck standing on it rotates
    /// with it. Getting that setup right by hand is fiddly (the hinge has to be
    /// connected to the world, not to another body, and the collider has to cover the
    /// plank), which is what this tool automates.
    /// </summary>
    public static class BalanceBeamTool
    {
        private const float HalfLength = 1.9f;
        private const float Thickness = 0.4f;
        private const float MaxAngle = 18f;

        [MenuItem("DuckKing/Balance Beam/1. Create Balance Beam Here", false, 40)]
        public static void CreateBalanceBeam()
        {
            Create(FindPlaceholder());
        }

        [MenuItem("DuckKing/Balance Beam/2. Create Balance Beam From Selected Sprite", false, 41)]
        public static void CreateFromSelection()
        {
            Sprite sprite = null;
            foreach (var obj in Selection.objects)
            {
                if (obj is Sprite s)
                {
                    sprite = s;
                    break;
                }
            }

            if (sprite == null)
            {
                EditorUtility.DisplayDialog(
                    "Balance Beam",
                    "在 Project 窗口里选中一张 Sprite 再点这个菜单。",
                    "OK");
                return;
            }

            Create(sprite);
        }

        private static void Create(Sprite sprite)
        {
            GameObject sceneParent = FindSceneRoot();

            var plankGo = new GameObject("BalanceBeam");
            GameObjectUtility.SetParentAndAlign(plankGo, sceneParent);

            Vector3 pivot = ScenePivot();
            plankGo.transform.position = pivot;

            var sr = plankGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.72f, 0.55f, 0.34f);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(HalfLength * 2f, Thickness);
            sr.sortingOrder = 178;

            // The collider has to match the drawn plank exactly. A collider smaller than
            // the sprite means a duck can stand visually on the plank while actually
            // standing on nothing, which reads as the plank randomly dropping its rider.
            var col = plankGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(HalfLength * 2f, Thickness);
            col.offset = Vector2.zero;
            col.edgeRadius = 0.02f;

            // RequireComponent adds these automatically, but they are configured
            // explicitly so the created plank is correct before the first play.
            var rb = plankGo.GetComponent<Rigidbody2D>();
            if (rb == null) rb = plankGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 3f;
            rb.mass = 6f;
            rb.drag = 0.5f;
            rb.angularDrag = 2f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var hinge = plankGo.GetComponent<HingeJoint2D>();
            if (hinge == null) hinge = plankGo.AddComponent<HingeJoint2D>();

            // connectedBody left null on purpose: a hinge connected to nothing is pinned
            // to a fixed point in world space, which is exactly what a see-saw is.
            hinge.connectedBody = null;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = pivot;
            hinge.useLimits = true;
            hinge.limits = new JointAngleLimits2D { min = -MaxAngle, max = MaxAngle };

            var beam = plankGo.AddComponent<BalanceBeam>();
            var so = new SerializedObject(beam);
            SetFloat(so, "halfLength", HalfLength);
            SetFloat(so, "plankThickness", Thickness);
            SetFloat(so, "boardTolerance", 0.25f);
            SetFloat(so, "maxAngle", MaxAngle);
            SetFloat(so, "plankMass", 6f);
            SetFloat(so, "plankGravityScale", 3f);
            so.ApplyModifiedPropertiesWithoutUndo();

            CreateFulcrum(plankGo.transform, sr.color, pivot);

            Undo.RegisterCreatedObjectUndo(plankGo, "Create Balance Beam");
            Selection.activeGameObject = plankGo;

            Debug.Log("[DuckKing] Balance beam created with a Dynamic Rigidbody2D and a " +
                      "HingeJoint2D pinned to the world. Drag your art sprite onto its " +
                      "Sprite Renderer.", plankGo);
        }

        private static Vector3 ScenePivot()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) return sv.pivot;

            return new Vector3(0f, 2f, 0f);
        }

        private static void CreateFulcrum(Transform plank, Color tint, Vector3 pivot)
        {
            var fulcrumGo = new GameObject("Fulcrum");
            GameObjectUtility.SetParentAndAlign(fulcrumGo, plank.parent != null ? plank.parent.gameObject : null);

            var sr = fulcrumGo.AddComponent<SpriteRenderer>();
            sr.sprite = FindPlaceholder();
            sr.sortingOrder = 177;
            sr.color = new Color(tint.r * 0.6f, tint.g * 0.6f, tint.b * 0.6f);

            fulcrumGo.transform.position = pivot + new Vector3(0f, -Thickness * 0.5f, 0f);
            fulcrumGo.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
        }

        private static GameObject FindSceneRoot()
        {
            var level = GameObject.Find("Level");
            if (level != null) return level;

            var platforms = GameObject.Find("Platforms");
            if (platforms != null) return platforms;

            return null;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.floatValue = value;
        }

        private static Sprite FindPlaceholder()
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Placeholder/WhiteSquare.png");
        }
    }
}