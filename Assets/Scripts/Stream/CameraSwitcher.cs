using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Thesis.Dibr;
using Thesis.Managers;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Thesis.Stream
{
    public class CameraSwitcher : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CameraStreamPlayer _streamPlayer;
        [SerializeField] private Transform _buttonContainer;

        [Header("Transition")]
        [SerializeField] private float _crossfadeDuration = 1.5f;
        [SerializeField] private float _dibrFadeInDuration = 0.3f;

        private readonly Dictionary<string, Button> _buttons = new();
        private string _activeCamera;

        // Second live track + a synthetic-view overlay, created at runtime as
        // siblings of _streamPlayer's RawImage (matching this class's
        // existing convention of building UI programmatically rather than
        // via prefab authoring — see CreateButton below). See
        // Master-Thesis-Reports/handoff_spec_Sep_20th_opendibr_live_control_and_export.md
        // and the plan (tranquil-seeking-clarke.md, Phase D) for the full
        // switch sequence this implements: fallback-first 2D crossfade,
        // swapped for the OpenDIBR synthetic view if it becomes ready in
        // time, settling on a plain single-camera view either way.
        private CameraStreamPlayer _transitionPlayer;
        private RawImage _transitionImage;
        private RawImage _dibrImage;
        private Coroutine _activeSwitch;

        void Start()
        {
            if (!LiveKitManager.HasInstance) return;

            LiveKitManager.Instance.OnConnected += OnConnected;
            LiveKitManager.Instance.OnVideoTrackAvailable += OnVideoTrackAvailable;
            LiveKitManager.Instance.OnVideoTrackRemoved += OnVideoTrackRemoved;
            LiveKitManager.Instance.OnParticipantNameChanged += OnParticipantNameChanged;

            if (LiveKitManager.Instance.IsConnected)
                OnConnected(LiveKitManager.Instance.Room);
        }

        void OnDestroy()
        {
            if (!LiveKitManager.HasInstance) return;
            LiveKitManager.Instance.OnConnected -= OnConnected;
            LiveKitManager.Instance.OnVideoTrackAvailable -= OnVideoTrackAvailable;
            LiveKitManager.Instance.OnVideoTrackRemoved -= OnVideoTrackRemoved;
            LiveKitManager.Instance.OnParticipantNameChanged -= OnParticipantNameChanged;
        }

        private void OnConnected(LiveKit.Room room)
        {
            foreach (var identity in LiveKitManager.Instance.VideoTracks.Keys)
                OnVideoTrackAvailable(identity);
        }

        private void OnVideoTrackAvailable(string identity)
        {
            if (!_buttons.ContainsKey(identity))
                CreateButton(identity);
            SetButtonInteractable(identity, true);
        }

        private void OnVideoTrackRemoved(string identity)
        {
            SetButtonInteractable(identity, false);

            if (_activeCamera == identity)
            {
                if (_activeSwitch != null) { StopCoroutine(_activeSwitch); _activeSwitch = null; }
                _streamPlayer?.Unsubscribe();
                _transitionPlayer?.Unsubscribe();
                _activeCamera = null;
            }
        }

        private void OnParticipantNameChanged(string identity)
        {
            if (!_buttons.TryGetValue(identity, out var btn)) return;
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = ResolveLabel(identity);
        }

        // ── Switching ────────────────────────────────────────────────────

        private void SwitchCamera(string identity)
        {
            if (_activeCamera == identity) return;
            if (!LiveKitManager.Instance.VideoTracks.TryGetValue(identity, out var pub)) return;

            string fromIdentity = _activeCamera;
            _activeCamera = identity;
            HighlightActiveButton(identity);

            if (_activeSwitch != null) StopCoroutine(_activeSwitch);
            _activeSwitch = StartCoroutine(RunSwitch(fromIdentity, identity, pub));
        }

        private IEnumerator RunSwitch(string fromIdentity, string toIdentity, LiveKit.RemoteTrackPublication toPub)
        {
            EnsureTransitionLayers();

            // _transitionPlayer must render above _streamPlayer for the fade-in to be visible.
            // After each swap the sibling order inverts, so re-establish it every switch.
            _transitionPlayer.transform.SetAsLastSibling();
            _dibrImage.transform.SetAsLastSibling(); // keep DIBR overlay on top of both

            bool haveFrom = fromIdentity != null;
            _dibrImage.gameObject.SetActive(false);
            SetAlpha(_dibrImage, 0f);

            _transitionPlayer.SubscribeTo(toPub);
            SetAlpha(_transitionImage, haveFrom ? 0f : 1f);

            Task<PrepareResult> prepareTask = null;
            if (haveFrom && DibrCapability.IsAvailable && OpenDibrSessionManager.HasInstance)
                prepareTask = OpenDibrSessionManager.Instance.PreparePairAsync(fromIdentity, toIdentity, Thesis.AppConfig.ServerUrl);

            PrepareResult prepareResult = default;
            bool haveResult = false;
            bool dibrActive = false;
            float dibrFadeT = 0f;
            float t = 0f;

            while (t < _crossfadeDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / _crossfadeDuration);

                if (haveFrom && !dibrActive)
                    SetAlpha(_transitionImage, u);

                if (!haveResult && prepareTask != null && prepareTask.IsCompleted)
                {
                    haveResult = true;
                    prepareResult = prepareTask.Status == TaskStatus.RanToCompletion
                        ? prepareTask.Result
                        : PrepareResult.Failed(prepareTask.Exception?.InnerException?.Message ?? "unknown error");

                    if (prepareResult.Success)
                    {
                        dibrActive = true;
                        _dibrImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        Debug.Log($"[CameraSwitcher] Falling back to plain crossfade for {fromIdentity}->{toIdentity}: {prepareResult.Reason}");
                    }
                }

                if (dibrActive)
                {
                    OpenDibrPoseMath.Lerp(prepareResult.ExtrinsicsA, prepareResult.ExtrinsicsB, u, out var pos, out var rot);
                    OpenDibrSessionManager.Instance.StreamPose(pos, rot);

                    var dibrTex = OpenDibrSessionManager.Instance.FrameReceiver.Texture;
                    if (dibrTex != null) _dibrImage.texture = dibrTex;

                    dibrFadeT = Mathf.Min(dibrFadeT + Time.deltaTime, _dibrFadeInDuration);
                    float fadeIn = _dibrFadeInDuration > 0f ? dibrFadeT / _dibrFadeInDuration : 1f;
                    SetAlpha(_dibrImage, fadeIn);
                    // Never let the flat crossfade dip below where a plain,
                    // non-DIBR switch would already be — DIBR only adds on
                    // top, never reveals a flash of the old camera under it.
                    SetAlpha(_transitionImage, haveFrom ? Mathf.Max(1f - fadeIn, u) : (1f - fadeIn));
                }

                yield return null;
            }

            // Settle: plain single-camera view of toIdentity, DIBR overlay hidden.
            SetAlpha(_transitionImage, 1f);
            _dibrImage.gameObject.SetActive(false);
            SetAlpha(_dibrImage, 0f);

            if (haveFrom) _streamPlayer.Unsubscribe();
            (_streamPlayer, _transitionPlayer) = (_transitionPlayer, _streamPlayer);
            var streamImage = _streamPlayer.GetComponent<RawImage>();
            _transitionImage = _transitionPlayer.GetComponent<RawImage>();
            SetAlpha(streamImage, 1f);
            SetAlpha(_transitionImage, 0f); // hide the retired display so it doesn't bleed through next switch

            _activeSwitch = null;
        }

        private void EnsureTransitionLayers()
        {
            if (_transitionPlayer != null) return;

            var streamImage = _streamPlayer.GetComponent<RawImage>();
            var streamRect = streamImage.rectTransform;

            _transitionImage = CreateOverlayRawImage("TransitionDisplay", streamRect);
            _transitionPlayer = _transitionImage.gameObject.AddComponent<CameraStreamPlayer>();

            _dibrImage = CreateOverlayRawImage("DibrSyntheticView", streamRect);
            _dibrImage.gameObject.SetActive(false);
        }

        private static RawImage CreateOverlayRawImage(string name, RectTransform matchRect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(matchRect.parent, false);
            go.transform.SetAsLastSibling(); // render on top of _streamPlayer's image

            var rt = (RectTransform)go.transform;
            rt.anchorMin = matchRect.anchorMin;
            rt.anchorMax = matchRect.anchorMax;
            rt.offsetMin = matchRect.offsetMin;
            rt.offsetMax = matchRect.offsetMax;
            rt.pivot = matchRect.pivot;

            return go.GetComponent<RawImage>();
        }

        private static void SetAlpha(RawImage image, float alpha)
        {
            if (image == null) return;
            var c = image.color;
            c.a = Mathf.Clamp01(alpha);
            image.color = c;
        }

        // ── Buttons (unchanged) ─────────────────────────────────────────

        private void CreateButton(string identity)
        {
            var go = new GameObject(identity, typeof(RectTransform));
            go.transform.SetParent(_buttonContainer, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 50);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.05f, 0.05f, 0.05f, 1f);
            colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.4f);
            btn.colors = colors;
            btn.targetGraphic = bg;
            btn.interactable = false;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.offsetMin = new Vector2(8, 0);
            textRt.offsetMax = new Vector2(-8, 0);

            var label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = ResolveLabel(identity);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 16;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;

            var captured = identity;
            btn.onClick.AddListener(() => SwitchCamera(captured));
            _buttons[identity] = btn;
        }

        private string ResolveLabel(string identity)
        {
            if (LiveKitManager.HasInstance &&
                LiveKitManager.Instance.Room != null &&
                LiveKitManager.Instance.Room.RemoteParticipants.TryGetValue(identity, out var participant) &&
                !string.IsNullOrEmpty(participant.Name))
                return participant.Name;

            return identity.ToUpper();
        }

        private void SetButtonInteractable(string identity, bool interactable)
        {
            if (_buttons.TryGetValue(identity, out var btn))
                btn.interactable = interactable;
        }

        private void HighlightActiveButton(string activeIdentity)
        {
            foreach (var kv in _buttons)
            {
                var img = kv.Value.GetComponent<Image>();
                if (img == null) continue;
                img.color = kv.Key == activeIdentity
                    ? new Color(0.2f, 0.5f, 0.9f, 1f)
                    : new Color(0.15f, 0.15f, 0.15f, 0.9f);
            }
        }
    }
}
