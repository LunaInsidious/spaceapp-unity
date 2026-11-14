// AI-assisted code (generated with Claude Code) — see README.
using UnityEngine;
using UnityEngine.InputSystem;

namespace LunarOutpost.Game
{
    /// <summary>Astronaut with low-gravity hops and a third-person follow camera.</summary>
    public sealed class PlayerRig
    {
        readonly MoonWorld _world;
        readonly float _gravity, _walkSpeed, _jumpSpeed;
        public Transform Body { get; }
        public Camera Cam { get; }

        Vector3 _pos;
        float _vy;
        float _yaw = 180f, _pitch = 18f, _dist = 11f;  // start with the low Sun behind the camera
        float _hopPhase;

        public Vector3 Position => _pos;
        public Vector3 Forward => Quaternion.Euler(0, _yaw, 0) * Vector3.forward;

        public PlayerRig(MoonWorld world, float gravity, float walkSpeed, float jumpSpeed, Vector3 start)
        {
            _world = world;
            _gravity = gravity;
            _walkSpeed = walkSpeed;
            _jumpSpeed = jumpSpeed;

            var root = new GameObject("Astronaut").transform;
            var white = world.Mat(new Color(0.95f, 0.95f, 0.95f));
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0, 0.9f, 0);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = white;
            var visor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(visor.GetComponent<Collider>());
            visor.transform.SetParent(root, false);
            visor.transform.localPosition = new Vector3(0, 1.45f, 0.28f);
            visor.transform.localScale = new Vector3(0.5f, 0.35f, 0.3f);
            visor.GetComponent<Renderer>().sharedMaterial = world.Mat(new Color(0.9f, 0.7f, 0.2f));
            var pack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(pack.GetComponent<Collider>());
            pack.transform.SetParent(root, false);
            pack.transform.localPosition = new Vector3(0, 1.0f, -0.45f);
            pack.transform.localScale = new Vector3(0.7f, 0.8f, 0.35f);
            pack.GetComponent<Renderer>().sharedMaterial = white;
            Body = root;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.nearClipPlane = 0.2f;
            Cam.farClipPlane = 3000f;
            Cam.fieldOfView = 60f;

            Teleport(start);
        }

        public void Teleport(Vector3 p)
        {
            _pos = new Vector3(p.x, _world.HeightAt(p.x, p.z), p.z);
            _vy = 0;
            Body.position = _pos;
        }

        public void SetVisible(bool v) => Body.gameObject.SetActive(v);

        public void Tick(float dt, bool canMove)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                _yaw += d.x * 0.25f;
                _pitch = Mathf.Clamp(_pitch - d.y * 0.2f, 2f, 70f);
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) _dist = Mathf.Clamp(_dist - Mathf.Sign(scroll) * 1.2f, 4f, 40f);
            }

            Vector2 move = Vector2.zero;
            bool jump = false;
            if (canMove && kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
                jump = kb.spaceKey.wasPressedThisFrame;
            }
            if (move.sqrMagnitude > 1) move.Normalize();

            var camYaw = Quaternion.Euler(0, _yaw, 0);
            var wish = camYaw * new Vector3(move.x, 0, move.y) * _walkSpeed;
            _pos.x += wish.x * dt;
            _pos.z += wish.z * dt;
            var flat = new Vector2(_pos.x, _pos.z);
            if (flat.magnitude > MoonWorld.PlayRadius)
            {
                flat = flat.normalized * MoonWorld.PlayRadius;
                _pos.x = flat.x; _pos.z = flat.y;
            }

            float ground = _world.HeightAt(_pos.x, _pos.z);
            bool grounded = _pos.y <= ground + 0.01f;
            // Bunny-hop gait in low gravity while walking.
            if (grounded && (jump || move.sqrMagnitude > 0.01f))
            {
                _vy = jump ? _jumpSpeed : _jumpSpeed * 0.35f;
                grounded = false;
            }
            _vy -= _gravity * dt;
            _pos.y += _vy * dt;
            if (_pos.y < ground) { _pos.y = ground; _vy = 0; }

            Body.position = _pos;
            if (move.sqrMagnitude > 0.01f)
                Body.rotation = Quaternion.Slerp(Body.rotation, Quaternion.LookRotation(new Vector3(wish.x, 0, wish.z)), dt * 10f);
            _hopPhase += dt;

            // Follow camera that never dips into the ground.
            var offset = Quaternion.Euler(_pitch, _yaw, 0) * new Vector3(0, 0, -_dist);
            var target = _pos + Vector3.up * 1.5f;
            var camPos = target + offset;
            float camGround = _world.HeightAt(camPos.x, camPos.z) + 0.8f;
            if (camPos.y < camGround) camPos.y = camGround;
            Cam.transform.position = camPos;
            Cam.transform.LookAt(target);
        }
    }
}
