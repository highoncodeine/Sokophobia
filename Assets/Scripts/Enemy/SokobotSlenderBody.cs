using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sokophobia.Enemy
{
    /// <summary>
    /// Drives the Slenderman model with Idle/Walk/Run clips from the stock
    /// NVJOB controller, detaches that auto-cycling
    /// controller, and crossfades a Playables mixer from the AI blackboard
    /// (state + agent velocity). Add to the Sokobot root next to
    /// SokobotController; the Slender prefab instance must be a child.
    /// Follows game-ai: decision (SokobotController) stays separate from
    /// motion/appearance (here + NavMeshAgent).
    /// </summary>
    [RequireComponent(typeof(SokobotController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class SokobotSlenderBody : MonoBehaviour
    {
        [Header("Blend")]
        [SerializeField] private float crossfadeSpeed = 6f;
        [SerializeField] private float moveThreshold = 0.4f;
        [Header("Stride sync (anti-skate)")]
        [Tooltip("Fallback Walk speed in m/s when the clip has no root-motion speed.")]
        [SerializeField] private float walkReferenceSpeed = 1.2f;
        [Tooltip("Fallback Run speed in m/s when the clip has no root-motion speed.")]
        [SerializeField] private float runReferenceSpeed = 2.6f;

        // The stunned state has no dedicated clip and uses Idle. Attack.anim is
        // not wired into the stock controller, so there is no Attack slot.
        private static readonly string[] ClipNames = { "Idle", "Walk", "Run" };

        private SokobotController _brain;
        private NavMeshAgent _agent;
        private Animator _anim;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable[] _playables = new AnimationClipPlayable[0];
        private float _walkMotionSpeed;
        private float _runMotionSpeed;

        private void Awake()
        {
            _brain = GetComponent<SokobotController>();
            _agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            // Hide the legacy capsule visual anywhere under the bot root.
            // The Slender child uses SkinnedMeshRenderers, so it is untouched.
            foreach (var mr in GetComponentsInChildren<MeshRenderer>())
                mr.enabled = false;

            _anim = GetComponentInChildren<Animator>();
            if (_anim == null)
            {
                Debug.LogWarning("[SokobotSlenderBody] No Animator in children (Slender prefab missing?).");
                enabled = false;
                return;
            }

            // Silence the stock prefab's ambient loop; the dread director owns audio.
            foreach (var audio in GetComponentsInChildren<AudioSource>())
                Destroy(audio);

            if (!BuildGraph())
            {
                enabled = false;
                return;
            }

            FitAgentToModel();
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }

        private bool BuildGraph()
        {
            var controller = _anim.runtimeAnimatorController;
            if (controller == null)
            {
                Debug.LogWarning("[SokobotSlenderBody] Slender Animator has no controller to harvest clips from.");
                return false;
            }

            var byName = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (var clip in controller.animationClips)
            {
                if (clip != null && !byName.ContainsKey(clip.name))
                    byName.Add(clip.name, clip);
            }
            foreach (var n in ClipNames)
            {
                if (!byName.ContainsKey(n))
                    Debug.LogWarning($"[SokobotSlenderBody] Clip '{n}' not found in Slender controller.");
            }

            _walkMotionSpeed = GetMotionSpeed(byName, "Walk", walkReferenceSpeed);
            _runMotionSpeed = GetMotionSpeed(byName, "Run", runReferenceSpeed);

            // Detach the auto-cycling stock controller; Playables take over.
            // The Avatar stays on the Animator, so muscle clips keep retargeting.
            _anim.runtimeAnimatorController = null;
            _anim.applyRootMotion = false;
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("SokobotSlenderBody");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Body", _anim);
            _mixer = AnimationMixerPlayable.Create(_graph, ClipNames.Length);
            output.SetSourcePlayable(_mixer);

            _playables = new AnimationClipPlayable[ClipNames.Length];
            for (int i = 0; i < ClipNames.Length; i++)
            {
                AnimationClip clip = byName.TryGetValue(ClipNames[i], out var c) ? c : null;
                if (clip == null) continue;
                var p = AnimationClipPlayable.Create(_graph, clip);
                // NOTE: foot IK stays OFF (Playables foot-IK + scaled avatar root
                // slammed the whole body ~2.6m underground). Keep planted via stride sync.
                p.SetApplyFootIK(false);
                _playables[i] = p;
                _graph.Connect(p, 0, _mixer, i);
                _mixer.SetInputWeight(i, i == 0 ? 1f : 0f);
            }

            _graph.Play();
            return true;
        }

        private static float GetMotionSpeed(
            System.Collections.Generic.Dictionary<string, AnimationClip> clips,
            string clipName,
            float fallback)
        {
            if (clips.TryGetValue(clipName, out var clip))
            {
                Vector3 rootSpeed = clip.averageSpeed;
                rootSpeed.y = 0f;
                if (rootSpeed.magnitude > 0.05f)
                    return rootSpeed.magnitude;
            }

            return Mathf.Max(0.05f, fallback);
        }

        private void FitAgentToModel()
        {
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            float h = Mathf.Max(1.5f, bounds.size.y);
            _agent.height = h;
            _agent.radius = Mathf.Clamp(bounds.size.x * 0.25f, 0.25f, 0.6f);
            _agent.baseOffset = 0f;
        }

        private void Update()
        {
            if (!_graph.IsValid() || _brain == null) return;

            int target = 0; // Idle
            var state = _brain.DebugState;
            float moveThresholdSqr = moveThreshold * moveThreshold;
            bool moving = !_agent.isStopped &&
                (_agent.velocity.sqrMagnitude > moveThresholdSqr ||
                 _agent.desiredVelocity.sqrMagnitude > moveThresholdSqr);

            if (state == SokobotController.StateId.Stunned)
                target = 0; // No dedicated stun clip; keep the neutral Idle pose.
            else if (state == SokobotController.StateId.Chase)
                target = moving ? 2 : 0; // Run / Idle
            else
                target = moving ? 1 : 0; // Walk / Idle

            float k = 1f - Mathf.Exp(-crossfadeSpeed * Time.deltaTime);
            float agentSpeed = _agent.velocity.magnitude;
            for (int i = 0; i < ClipNames.Length; i++)
            {
                if (!_playables[i].IsValid()) continue;
                float w = _mixer.GetInputWeight(i);
                w = Mathf.Lerp(w, i == target ? 1f : 0f, k);
                if (i == target && w > 0.99f) w = 1f;
                if (i != target && w < 0.01f) w = 0f;
                _mixer.SetInputWeight(i, w);

                // Keep both locomotion clips in stride with the agent while they
                // crossfade, and derive the authored rate from the clip's root motion.
                if (i == 1 || i == 2)
                {
                    float refSpeed = i == 1 ? _walkMotionSpeed : _runMotionSpeed;
                    _playables[i].SetSpeed(agentSpeed / refSpeed);
                }
                else if (_playables[i].GetSpeed() != 1f)
                {
                    _playables[i].SetSpeed(1f);
                }
            }
        }
    }
}
