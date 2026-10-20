using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sokophobia.Enemy;
using Sokophobia.Grid;

namespace Sokophobia.UI
{
    /// <summary>
    /// Enemy-AI HUD (game-ai verify-by-observation, in-game edition): alert
    /// glyph + suspicion bar, off-screen chase arrow, flashlight charge bar,
    /// plate progress, and a damage flash. Builds its own Canvas at runtime,
    /// so it only needs this component on any GameObject in the scene.
    /// </summary>
    public class EnemyAIHUD : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float edgeMargin = 70f;
        [SerializeField] private float flashPeak = 0.55f;
        [SerializeField] private float flashFade = 1.8f;

        private Canvas _canvas;
        private Text _alertText;
        private RectTransform _suspFill;
        private Image _suspFillImg;
        private GameObject _suspBar;
        private float _suspShown;
        private readonly List<RectTransform> _suspTicks = new();
        private RectTransform _chargeFill;
        private Image _chargeFillImg;
        private GameObject _chargeBar;
        private Text _platesText;
        private Image _flash;
        private RectTransform _arrow;
        private Image _arrowImg;

        private SokobotController _bot;
        private FlashlightStunner _stunner;
        private PuzzleManager _puzzle;
        private Camera _cam;
        private float _flashAlpha;

        private void Awake()
        {
            BuildCanvas();
        }

        private void Start()
        {
            // The enemy starts inactive and is spawned by EnemySpawner on the
            // first plate press, so cache it even while the object is disabled.
            _bot = FindAnyObjectByType<SokobotController>(FindObjectsInactive.Include);
            _stunner = FindAnyObjectByType<FlashlightStunner>();
            _puzzle = FindAnyObjectByType<PuzzleManager>();
            _cam = Camera.main;
            SubscribeToPuzzle();
            BuildSuspTicks();
            _suspShown = _bot != null ? _bot.DebugSuspicion : 0f;
        }

        // Threshold ticks (? becomes !): positioned once from the bot's tuning.
        private void BuildSuspTicks()
        {
            if (_bot == null) return;
            const float w = 300f;
            AddSuspTick(_bot.InvestigateThreshold, w, Color.white);
            AddSuspTick(_bot.ChaseThreshold, w, Color.red);
        }

        private void AddSuspTick(float t, float barW, Color c)
        {
            var go = new GameObject("Tick");
            go.transform.SetParent(_suspBar.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Mathf.Clamp01(t) * barW, 0f);
            rt.sizeDelta = new Vector2(2f, 20f);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            _suspTicks.Add(rt);
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerDamaged += HandleDamaged;
            SubscribeToPuzzle();
        }

        private void SubscribeToPuzzle()
        {
            // On the first enable, Start has not found the puzzle yet.
            if (_puzzle == null) return;

            _puzzle.OnProgressChanged += HandleProgress;
            _puzzle.OnPuzzleSolved += HandlePuzzleSolved;
            HandleProgress(_puzzle.PlacedCount, _puzzle.TotalPlates);
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerDamaged -= HandleDamaged;
            if (_puzzle != null)
            {
                _puzzle.OnProgressChanged -= HandleProgress;
                _puzzle.OnPuzzleSolved -= HandlePuzzleSolved;
            }
        }

        private void HandleDamaged() => _flashAlpha = flashPeak;
        private void HandleProgress(int placed, int total)
        {
            // Keep the escape instruction once solved, just like the open door.
            if (_puzzle != null && _puzzle.IsSolved)
            {
                HandlePuzzleSolved();
                return;
            }

            _platesText.text = total > 0 ? $"PLATES  {placed}/{total}" : "PLATES  --";
            _platesText.color = Color.white;
        }

        private void HandlePuzzleSolved()
        {
            _platesText.text = "ESCAPE!";
            _platesText.color = Color.red;
        }

        private void Update()
        {
            // Damage flash decay.
            if (_flashAlpha > 0f)
            {
                _flashAlpha = Mathf.Max(0f, _flashAlpha - flashFade * Time.deltaTime);
                _flash.color = new Color(0.7f, 0f, 0f, _flashAlpha);
            }

            if (_bot == null) { SetAlertVisible(false); SetArrowVisible(false); return; }

            var state = _bot.DebugState;
            float susp = _bot.DebugSuspicion;

            // Alert glyph + suspicion bar.
            bool showAlert = state == SokobotController.StateId.Chase
                || state == SokobotController.StateId.Investigate
                || state == SokobotController.StateId.Stunned
                || susp > 0.02f;
            SetAlertVisible(showAlert);
            if (showAlert)
            {
                if (state == SokobotController.StateId.Chase)
                {
                    _alertText.text = "!";
                    _alertText.color = Color.red;
                }
                else if (state == SokobotController.StateId.Stunned)
                {
                    _alertText.text = "STUNNED";
                    _alertText.color = Color.cyan;
                }
                else
                {
                    _alertText.text = "?";
                    _alertText.color = Color.yellow;
                }
                // Smooth the stepped 0.1s sense updates + ramp yellow->red.
                float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
                _suspShown = Mathf.Lerp(_suspShown, Mathf.Clamp01(susp), k);
                _suspFill.sizeDelta = new Vector2(300f * _suspShown, _suspFill.sizeDelta.y);
                if (_suspFillImg != null)
                {
                    _suspFillImg.color = Color.Lerp(Color.yellow, Color.red, _suspShown);
                    // Blink at max: the ? is about to become !.
                    if (_suspShown >= _bot.ChaseThreshold - 0.001f
                        && state != SokobotController.StateId.Chase)
                        _suspFillImg.color = Color.Lerp(Color.red, Color.white,
                            0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
                }
            }

            // High-beam charge bar. While discharging, the bar refills to show
            // when the next high beam is ready.
            float charge = _stunner != null ? _stunner.Charge01 : 0f;
            float cooldown = _stunner != null ? _stunner.Cooldown01 : 0f;
            bool charging = charge > 0.01f;
            bool recharging = !charging && cooldown > 0.01f;

            _chargeBar.SetActive(charging || recharging);

            if (charging)
            {
                _chargeFill.sizeDelta = new Vector2(220f * charge, _chargeFill.sizeDelta.y);
                if (_chargeFillImg != null) _chargeFillImg.color = Color.cyan;
            }
            else if (recharging)
            {
                _chargeFill.sizeDelta = new Vector2(
                    220f * (1f - cooldown), _chargeFill.sizeDelta.y);
                if (_chargeFillImg != null)
                    _chargeFillImg.color = new Color(0.25f, 0.5f, 0.6f, 0.9f);
            }

            UpdateArrow(state);
        }

        private void UpdateArrow(SokobotController.StateId state)
        {
            if (_cam == null) _cam = Camera.main;
            bool want = state == SokobotController.StateId.Chase && _cam != null;
            if (!want) { SetArrowVisible(false); return; }

            Vector3 botPos = _bot.transform.position;
            Vector3 toBot = botPos - _cam.transform.position;
            bool behind = Vector3.Dot(_cam.transform.forward, toBot) <= 0f;
            Vector3 sp = _cam.WorldToScreenPoint(botPos);
            Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = new Vector2(sp.x - center.x, sp.y - center.y);
            if (behind) dir = -dir;

            bool onScreen = !behind
                && sp.x > edgeMargin && sp.x < Screen.width - edgeMargin
                && sp.y > edgeMargin && sp.y < Screen.height - edgeMargin;
            if (onScreen || dir.sqrMagnitude < 1f) { SetArrowVisible(false); return; }

            SetArrowVisible(true);
            dir.Normalize();
            // Clamp to an inset rect.
            float tx = (Screen.width * 0.5f - edgeMargin) / Mathf.Max(1f, Mathf.Abs(dir.x));
            float ty = (Screen.height * 0.5f - edgeMargin) / Mathf.Max(1f, Mathf.Abs(dir.y));
            Vector2 pos = center + dir * Mathf.Min(tx, ty);
            _arrow.position = new Vector3(pos.x, pos.y, 0f);
            _arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            _arrowImg.color = Color.red;
        }

        private void SetAlertVisible(bool v)
        {
            _alertText.gameObject.SetActive(v);
            _suspBar.SetActive(v);
        }

        private void SetArrowVisible(bool v) => _arrow.gameObject.SetActive(v);

        // ---------- Runtime UI construction ----------
        private static Font Arial() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void BuildCanvas()
        {
            var cv = new GameObject("EnemyAIHUD_Canvas");
            cv.transform.SetParent(transform, false);
            _canvas = cv.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = cv.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // Damage flash (bottom of draw order).
            _flash = FullScreenImage(new Color(0.7f, 0f, 0f, 0f));

            // Alert glyph, top-center (clear of the bar below it).
            _alertText = MakeText("Alert", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), 44, TextAnchor.MiddleCenter);
            _alertText.gameObject.SetActive(false);

            // Suspicion bar under the glyph.
            _suspBar = Bar(new Vector2(0.5f, 1f), new Vector2(0f, -104f), 300f, 14f,
                new Color(0f, 0f, 0f, 0.6f), Color.yellow, out _suspFill);
            _suspFillImg = _suspFill.GetComponent<Image>();
            _suspBar.SetActive(false);

            // Plates progress, top-left.
            _platesText = MakeText("Plates", Vector2.up, Vector2.up,
                new Vector2(130f, -34f), 22, TextAnchor.MiddleLeft);
            _platesText.text = "PLATES  --";

            // Flashlight charge, bottom-center.
            _chargeBar = Bar(new Vector2(0.5f, 0f), new Vector2(0f, 44f), 220f, 10f,
                new Color(0f, 0f, 0f, 0.6f), Color.cyan, out _chargeFill);
            _chargeFillImg = _chargeFill.GetComponent<Image>();
            _chargeBar.SetActive(false);

            // Chase arrow (hidden until needed).
            var arrowGo = new GameObject("ChaseArrow");
            arrowGo.transform.SetParent(_canvas.transform, false);
            _arrow = arrowGo.AddComponent<RectTransform>();
            _arrow.sizeDelta = new Vector2(48f, 48f);
            _arrowImg = arrowGo.AddComponent<Image>();
            _arrowImg.sprite = TriangleSprite();
            _arrowImg.color = Color.red;
            arrowGo.SetActive(false);
        }

        private Image FullScreenImage(Color c)
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private Text MakeText(string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, int size, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400f, 60f);
            var t = go.AddComponent<Text>();
            t.font = Arial();
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private GameObject Bar(Vector2 anchor, Vector2 anchoredPos, float w, float h, Color bg, Color fill, out RectTransform fillRt)
        {
            var go = new GameObject("Bar");
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(w, h);
            var bgImg = go.AddComponent<Image>();
            bgImg.color = bg;
            bgImg.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            fillRt = fillGo.AddComponent<RectTransform>();
            // Anchor to the bar's LEFT edge so the fill grows rightward inside it.
            fillRt.anchorMin = new Vector2(0f, 0.5f);
            fillRt.anchorMax = new Vector2(0f, 0.5f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, h);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fill;
            fillImg.raycastTarget = false;
            return go;
        }

        private static Sprite TriangleSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.ARGB32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var pix = new Color[s * s];
            for (int i = 0; i < pix.Length; i++) pix[i] = clear;
            for (int y = 0; y < s; y++)
            {
                float row = 1f - (float)y / s; // wide at bottom, apex at top
                int half = Mathf.RoundToInt(row * (s * 0.45f));
                for (int x = s / 2 - half; x <= s / 2 + half; x++)
                    pix[y * s + x] = Color.white;
            }
            tex.SetPixels(pix);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, s, s), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
