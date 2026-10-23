using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Tactics camera over the board. It frames the whole room by default; the player can rotate (right drag, Z / X),
    /// pan (middle drag, WASD or arrows), zoom (scroll) and re-center (F). On touch screens two fingers pan, pinch to
    /// zoom and twist to rotate, so one-finger taps stay free for commands. Works while paused.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public BoardView Board;
        public BattleHud Hud;

        public const float Pitch = 58f, MinZoom = 0.4f, MaxZoom = 1.5f;
        const float RotateKeySpeed = 90f, PanKeySpeed = 9f, DragRotateSpeed = 0.3f;

        public float Yaw { get; private set; }
        public float Zoom { get; private set; } = 1f;
        public Vector3 Pan { get; private set; }

        Camera cam;
        Battle framed;
        Vector2 lastMouse;
        bool dragging;
        float lastPinch, lastTwist;
        Vector2 lastTouchMid;

        void Awake() => cam = GetComponent<Camera>();

        /// <summary>Back to the default view: no rotation, no pan, the whole room in view.</summary>
        public void ResetView()
        {
            Yaw = 0;
            Zoom = 1;
            Pan = Vector3.zero;
        }

        void LateUpdate()
        {
            var battle = Board != null ? Board.Battle : null;
            if (battle == null) return;
            if (battle != framed)
            {
                framed = battle;
                ResetView();
            }
            HandleInput();
            Apply(battle);
        }

        Vector3 BoardCenter(Battle b) => new Vector3((b.Width - 1) / 2f, 0, -(b.Height - 1) / 2f);

        /// <summary>Distance at which the whole room fits the viewport at the default angle.</summary>
        float FitDistance(Battle b)
        {
            cam.fieldOfView = 35f;
            float halfW = b.Width / 2f + 0.3f;
            float halfH = (b.Height / 2f + 0.3f) * Mathf.Sin(Pitch * Mathf.Deg2Rad) + 0.6f;
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(0.1f, cam.aspect);
            return Mathf.Max(halfW / tanH, halfH / tanV) + 1f;
        }

        void Apply(Battle b)
        {
            var rotation = Quaternion.Euler(Pitch, Yaw, 0);
            // Keep the focus point over the board.
            var p = Pan;
            p.x = Mathf.Clamp(p.x, -b.Width / 2f, b.Width / 2f);
            p.z = Mathf.Clamp(p.z, -b.Height / 2f, b.Height / 2f);
            p.y = 0;
            Pan = p;
            var focus = BoardCenter(b) + Pan;
            cam.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * FitDistance(b) * Zoom, rotation);
        }

        void HandleInput()
        {
            float dt = Time.unscaledDeltaTime;
            Vector2 mouse = Input.mousePosition;
            bool overHud = Hud != null && Hud.IsOverHud(mouse);

            // Keyboard
            float rotate = (Input.GetKey(KeyCode.X) ? 1 : 0) - (Input.GetKey(KeyCode.Z) ? 1 : 0);
            Yaw += rotate * RotateKeySpeed * dt;
            var move = new Vector2(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
            if (move != Vector2.zero) PanBy(GroundRight * move.x + GroundForward * move.y, PanKeySpeed * Zoom * dt);
            if (Input.GetKeyDown(KeyCode.F)) ResetView();

            // Mouse
            if (!overHud && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
                Zoom = Mathf.Clamp(Zoom * (1 - Input.mouseScrollDelta.y * 0.1f), MinZoom, MaxZoom);
            // Drags only start off the UI, so scrolling a panel or pressing a button never moves the camera.
            if ((Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) && !overHud)
            {
                dragging = true;
                lastMouse = mouse;
            }
            if (!Input.GetMouseButton(1) && !Input.GetMouseButton(2)) dragging = false;
            if (dragging)
            {
                if (Input.GetMouseButton(1)) Yaw += (mouse.x - lastMouse.x) * DragRotateSpeed;
                else if (Input.GetMouseButton(2)) DragPan(lastMouse, mouse);
                lastMouse = mouse;
            }

            // Two-finger touch
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                var mid = (a.position + b.position) / 2;
                float pinch = Vector2.Distance(a.position, b.position);
                float twist = Mathf.Atan2(b.position.y - a.position.y, b.position.x - a.position.x) * Mathf.Rad2Deg;
                if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
                {
                    lastPinch = pinch;
                    lastTwist = twist;
                    lastTouchMid = mid;
                }
                else
                {
                    if (lastPinch > 1 && pinch > 1) Zoom = Mathf.Clamp(Zoom * lastPinch / pinch, MinZoom, MaxZoom);
                    Yaw -= Mathf.DeltaAngle(lastTwist, twist);
                    DragPan(lastTouchMid, mid);
                    lastPinch = pinch;
                    lastTwist = twist;
                    lastTouchMid = mid;
                }
            }
        }

        Vector3 GroundRight => Quaternion.Euler(0, Yaw, 0) * Vector3.right;
        Vector3 GroundForward => Quaternion.Euler(0, Yaw, 0) * Vector3.forward;

        void PanBy(Vector3 direction, float amount) => Pan += direction.normalized * amount;

        /// <summary>Moves the view so the ground point under <paramref name="from"/> ends up under <paramref name="to"/>.</summary>
        void DragPan(Vector2 from, Vector2 to)
        {
            if (Ground(from, out var a) && Ground(to, out var b)) Pan += a - b;
        }

        bool Ground(Vector2 screen, out Vector3 point)
        {
            var ray = cam.ScreenPointToRay(screen);
            point = default;
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }
    }
}
