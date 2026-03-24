using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Network_Game.ThirdPersonController.InputSystem;

namespace Network_Game.ThirdPersonController
{
    /// <summary>Temporary runtime diagnostic — remove after investigation.</summary>
    public class PlayerDiagnostic : MonoBehaviour
    {
        private CharacterController _cc;
        private Animator _anim;
        private StarterAssetsInputs _input;
        private ThirdPersonController _tpc;
        private NetworkObject _net;
        private PlayerInput _pi;
        private float _timer;

        private void Start()
        {
            _cc    = GetComponent<CharacterController>();
            _anim  = GetComponent<Animator>();
            _input = GetComponent<StarterAssetsInputs>();
            _tpc   = GetComponent<ThirdPersonController>();
            _net   = GetComponent<NetworkObject>();
            _pi    = GetComponent<PlayerInput>();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < 1.5f) return;
            _timer = 0f;

            // --- identity ---
            bool isOwner    = _net == null || _net.IsOwner;
            bool tpcOn      = _tpc != null && _tpc.enabled;
            bool ccOn       = _cc  != null && _cc.enabled;
            bool piOn       = _pi  != null && _pi.enabled;
            bool inputBlock = _input != null && _input.inputBlocked;
            string amap     = _tpc != null ? _tpc.ActiveInputActionMap : "n/a";

            // --- physics ---
            Vector3 pos    = transform.position;
            bool ccGround  = _cc != null && _cc.isGrounded;
            Vector3 ccVel  = _cc != null ? _cc.velocity : Vector3.zero;
            Vector2 moveIn = _input != null ? _input.move : Vector2.zero;
            bool useRb     = _tpc != null && _tpc.UseRigidbody;

            // --- animation ---
            float spd     = _anim != null && _anim.isActiveAndEnabled ? _anim.GetFloat("Speed")   : -1f;
            bool gnd      = _anim != null && _anim.isActiveAndEnabled && _anim.GetBool("IsGrounded");
            bool rootMot  = _anim != null && _anim.applyRootMotion;

            // --- ground layer check ---
            int mask = _tpc != null ? _tpc.GroundLayers.value : 0;
            Vector3 sp = new Vector3(pos.x, pos.y + 0.14f, pos.z);
            int gHits  = Physics.OverlapSphere(sp, 0.4f, mask, QueryTriggerInteraction.Ignore).Length;
            Collider[] all = Physics.OverlapSphere(sp, 0.4f, ~0, QueryTriggerInteraction.Ignore);

            string layers = "";
            foreach (var c in all)
                layers += LayerMask.LayerToName(c.gameObject.layer) + "(" + c.gameObject.layer + ") ";

            Debug.Log("[DIAG A] pos=" + pos.ToString("F2")
                + " isOwner=" + isOwner + " tpc=" + tpcOn + " cc=" + ccOn + " pi=" + piOn
                + " inputBlocked=" + inputBlock + " actionMap=" + amap + " useRb=" + useRb);
            Debug.Log("[DIAG B] move=" + moveIn + " ccVel=" + ccVel.ToString("F2") + " ccGrounded=" + ccGround);
            Debug.Log("[DIAG C] anim.Speed=" + spd.ToString("F2") + " anim.Grounded=" + gnd + " rootMotion=" + rootMot);
            Debug.Log("[DIAG D] groundSphere hits (mask " + mask + ")=" + gHits + " | allHits=" + all.Length + " layers: " + layers);
        }
    }
}
