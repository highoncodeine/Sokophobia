using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sokophobia.Enemy
{
    [RequireComponent(typeof(SokobotController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class SokobotBodyDriver : MonoBehaviour
    {
        [Header("Blend")]
        [SerializeField] private float crossfadeSpeed = 6f;
        [SerializeField] private float moveThreshold = 0.4f;
        [Header("Stride sync (anti-skate)")]
        [Tooltip("Fallback Walk speed in m/s when the clip has no root-motion speed.")]
        [SerializeField] private float walkReferenceSpeed = 1.2f;
        [Tooltip("Fallback Run speed in m/s when the clip has no root-motion speed.")]
        [SerializeField] private float runReferenceSpeed = 2.6f;

        private static readonly string[][] ClipCandidates =
        {
            new[] { "StaticIdle", "Idle" },
            new[] { "Walk" },
            new[] { "Run" },
            new[] { "CombatIdle", "Idle" },
            new[] { "GetHit" },
        };

        public const int SlotIdle = 0;
        public const int SlotWalk = 1;
        public const int SlotRun = 2;
        public const int SlotAlert = 3;
        public const int SlotStun = 4;
        public const int SlotCount = 5;

        private SokobotController _brain;
        private NavMeshAgent _agent;
        private Animator _anim;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly AnimationClipPlayable[] _playables = new AnimationClipPlayable[SlotCount];
        private float _walkMotionSpeed;
        private float _runMotionSpeed;
        private bool _stunPlayed;

        private void Awake()
        {
            _brain = GetComponent<SokobotController>();
            _agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            _anim = GetComponentInChildren<Animator>();
            if (_anim == null)
            {
                Debug.LogWarning("[SokobotBodyDriver] No Animator in children (body model missing?).");
                enabled = false;
                return;
            }

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
                Debug.LogWarning("[SokobotBodyDriver] Body Animator has no controller to harvest clips from.");
                return false;
            }

            var byName = new Dictionary<string, AnimationClip>();
            foreach (var clip in controller.animationClips)
            {
                if (clip != null && !byName.ContainsKey(clip.name))
                    byName.Add(clip.name, clip);
            }

            var resolved = new AnimationClip[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                resolved[i] = FirstClip(byName, ClipCandidates[i]);

            if (resolved[SlotIdle] == null)
            {
                Debug.LogWarning("[SokobotBodyDriver] No idle clip (StaticIdle/Idle) in the body controller.");
                return false;
            }
            if (resolved[SlotWalk] == null)
                Debug.LogWarning("[SokobotBodyDriver] No 'Walk' clip; travel will reuse idle.");
            if (resolved[SlotRun] == null)
                Debug.LogWarning("[SokobotBodyDriver] No 'Run' clip; chase will reuse walk/idle.");
            if (resolved[SlotWalk] == null) resolved[SlotWalk] = resolved[SlotIdle];
            if (resolved[SlotRun] == null) resolved[SlotRun] = resolved[SlotWalk];
            if (resolved[SlotAlert] == null) resolved[SlotAlert] = resolved[SlotIdle];
            if (resolved[SlotStun] == null) resolved[SlotStun] = resolved[SlotIdle];

            _walkMotionSpeed = GetMotionSpeed(byName, "Walk", walkReferenceSpeed);
            _runMotionSpeed = GetMotionSpeed(byName, "Run", runReferenceSpeed);

            _anim.applyRootMotion = false;
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("SokobotBodyDriver");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Body", _anim);
            _mixer = AnimationMixerPlayable.Create(_graph, SlotCount);
            output.SetSourcePlayable(_mixer);

            for (int i = 0; i < SlotCount; i++)
            {
                var p = AnimationClipPlayable.Create(_graph, resolved[i]);
                p.SetApplyFootIK(false);
                _playables[i] = p;
                _graph.Connect(p, 0, _mixer, i);
                _mixer.SetInputWeight(i, i == SlotIdle ? 1f : 0f);
            }

            _graph.Play();
            return true;
        }

        private static AnimationClip FirstClip(
            Dictionary<string, AnimationClip> clips, string[] names)
        {
            foreach (var n in names)
                if (clips.TryGetValue(n, out var c))
                    return c;
            return null;
        }

        private static float GetMotionSpeed(
            Dictionary<string, AnimationClip> clips, string clipName, float fallback)
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
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            _agent.height = Mathf.Max(1.5f, bounds.size.y);
            _agent.radius = Mathf.Clamp(bounds.size.x * 0.25f, 0.25f, 0.6f);
            _agent.baseOffset = 0f;
        }

        private void Update()
        {
            if (!_graph.IsValid() || _brain == null) return;

            var state = _brain.DebugState;
            bool stunned = state == SokobotController.StateId.Stunned;
            float moveThresholdSqr = moveThreshold * moveThreshold;
            bool moving = !_agent.isStopped &&
                (_agent.velocity.sqrMagnitude > moveThresholdSqr ||
                 _agent.desiredVelocity.sqrMagnitude > moveThresholdSqr);

            int target;
            if (stunned)
            {
                target = SlotStun;
                if (!_stunPlayed)
                {
                    _playables[SlotStun].SetTime(0);
                    _playables[SlotStun].SetSpeed(1);
                    _stunPlayed = true;
                }
            }
            else
            {
                _stunPlayed = false;

                if (moving)
                    target = state == SokobotController.StateId.Chase ? SlotRun : SlotWalk;
                else
                    target = state == SokobotController.StateId.Patrol ||
                             state == SokobotController.StateId.Inactive
                        ? SlotIdle
                        : SlotAlert;
            }

            float k = 1f - Mathf.Exp(-crossfadeSpeed * Time.deltaTime);
            float agentSpeed = _agent.velocity.magnitude;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_playables[i].IsValid()) continue;
                float w = _mixer.GetInputWeight(i);
                w = Mathf.Lerp(w, i == target ? 1f : 0f, k);
                if (i == target && w > 0.99f) w = 1f;
                if (i != target && w < 0.01f) w = 0f;
                _mixer.SetInputWeight(i, w);

                if (i == SlotWalk || i == SlotRun)
                {
                    float refSpeed = i == SlotWalk ? _walkMotionSpeed : _runMotionSpeed;
                    _playables[i].SetSpeed(agentSpeed / refSpeed);
                }
                else if (_playables[i].GetSpeed() != 1f)
                {
                    _playables[i].SetSpeed(1f);
                }
            }
        }

        public float DebugWeight(int slot) =>
            _graph.IsValid() && slot >= 0 && slot < SlotCount ? _mixer.GetInputWeight(slot) : 0f;
    }
}
