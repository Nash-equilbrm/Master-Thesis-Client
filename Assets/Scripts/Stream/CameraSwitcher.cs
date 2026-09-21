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
        [SerializeField] private float _dibrPrepareWaitMax = 8f;  // max seconds to wait for PrepareResult before giving up
        [SerializeField] private float _dibrFrameWaitMax = 3f;    // extra wait after prepare succeeds but no frame yet
        [SerializeField] private float _dibrStartupDelay = 1.5f;  // delay after prepare succeeds before showing overlay (let OpenDIBR fill pipeline)

        private readonly Dictionary<string, Button> _buttons = new();
        private string _activeCamera;

        private RawImage _dibrImage;
        private Coroutine _activeSwitch;

        void Start()
        {
            if (!LiveKitManager.HasInstance) return;

            LiveKitManager.Instance.OnConnected += OnConnected;
            LiveKitManager.Instance.OnVideoTrackAvailable += OnVideoTrackAvailable;
            LiveKitManager.Instance.OnVideoTrackRemoved += OnVideoTrackRemoved;
            LiveKitManager.Instance.OnParticipantNameChanged += OnParticipantNameChanged;

            if (OpenDibrSessionManager.HasInstance)
                OpenDibrSessionManager.Instance.OnSessionStarted += TryProactiveWarmUp;

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

            if (OpenDibrSessionManager.HasInstance)
                OpenDibrSessionManager.Instance.OnSessionStarted -= TryProactiveWarmUp;
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
            TryProactiveWarmUp();
        }

        private void TryProactiveWarmUp()
        {
            if (!DibrCapability.IsAvailable || !OpenDibrSessionManager.HasInstance) return;
            if (!OpenDibrSessionManager.Instance.IsSessionStarted) return;

            var ids = new System.Collections.Generic.List<string>(_buttons.Keys);
            if (ids.Count < 2) return;

            // Kick off a background prepare for every known pair so OpenDIBR has
            // cameras added before the user clicks. Fire-and-forget — failures are
            // logged inside PreparePairAsync and leave the session usable.
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                {
                    var a = ids[i]; var b = ids[j];
                    Debug.Log($"[CameraSwitcher] Proactive warm-up: {a}↔{b}");
                    _ = OpenDibrSessionManager.Instance.PreparePairAsync(a, b, AppConfig.ServerUrl);
                }
        }

        private void OnVideoTrackRemoved(string identity)
        {
            SetButtonInteractable(identity, false);

            if (_activeCamera == identity)
            {
                if (_activeSwitch != null) { StopCoroutine(_activeSwitch); _activeSwitch = null; }
                _streamPlayer?.Unsubscribe();
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
            Debug.Log($"[CameraSwitcher] Switch {fromIdentity ?? "none"} → {toIdentity} | " +
                      $"DibrAvailable={DibrCapability.IsAvailable} " +
                      $"SessionManagerPresent={OpenDibrSessionManager.HasInstance} " +
                      $"SessionStarted={( OpenDibrSessionManager.HasInstance ? OpenDibrSessionManager.Instance.IsSessionStarted.ToString() : "n/a" )}");

            EnsureDibrLayer();
            _dibrImage.gameObject.SetActive(false);
            SetAlpha(_dibrImage, 0f);

            bool haveFrom = fromIdentity != null;
            Task<PrepareResult> prepareTask = null;
            if (haveFrom && DibrCapability.IsAvailable && OpenDibrSessionManager.HasInstance)
            {
                Debug.Log($"[CameraSwitcher] Starting PreparePairAsync for {fromIdentity}→{toIdentity}");
                prepareTask = OpenDibrSessionManager.Instance.PreparePairAsync(fromIdentity, toIdentity, AppConfig.ServerUrl);
            }
            else
            {
                Debug.Log($"[CameraSwitcher] Skipping DIBR — haveFrom={haveFrom}, DibrAvailable={DibrCapability.IsAvailable}, HasInstance={OpenDibrSessionManager.HasInstance}");
            }

            PrepareResult prepareResult = default;
            bool dibrActive = false;
            bool feedSwitched = false;
            float fadeInT = 0f;
            float fadeOutT = 0f;
            float t = 0f;
            float dibrReadyWaitT = 0f;   // time waiting for first frame after prepare succeeded
            float dibrStartupWaitT = 0f; // time waiting for pipeline to fill after prepare succeeded

            // Loop exits when:
            //   - prepare timed out or failed (instant cut), OR
            //   - feed switched AND overlay fade-out done, OR
            //   - DIBR active but no frame within _dibrFrameWaitMax (instant cut)
            while ((prepareTask != null && !prepareTask.IsCompleted && t < _dibrPrepareWaitMax)
                   || (prepareTask != null && prepareTask.IsCompleted && !dibrActive)
                   || (dibrActive && !feedSwitched && dibrReadyWaitT < _dibrFrameWaitMax)
                   || (feedSwitched && fadeOutT < _dibrFadeInDuration))
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / _crossfadeDuration);

                if (!dibrActive && prepareTask != null && prepareTask.IsCompleted)
                {
                    prepareResult = prepareTask.Status == TaskStatus.RanToCompletion
                        ? prepareTask.Result
                        : PrepareResult.Failed(prepareTask.Exception?.InnerException?.Message ?? "unknown error");

                    if (prepareResult.Success)
                    {
                        Debug.Log($"[CameraSwitcher] DIBR ready for {fromIdentity}→{toIdentity} at {t:F2}s — waiting {_dibrStartupDelay}s for pipeline");
                        dibrActive = true;
                    }
                    else
                    {
                        Debug.Log($"[CameraSwitcher] DIBR unavailable for {fromIdentity}→{toIdentity}: {prepareResult.Reason}");
                        prepareTask = null;
                    }
                }

                if (dibrActive)
                {
                    OpenDibrPoseMath.Lerp(prepareResult.ExtrinsicsA, prepareResult.ExtrinsicsB, u, out var pos, out var rot);
                    OpenDibrSessionManager.Instance.StreamPose(pos, rot);

                    // Hold off showing the overlay until the startup delay has passed —
                    // OpenDIBR outputs green frames for ~1s after set_active_pair while
                    // its pipeline fills with real RTSP data.
                    if (dibrStartupWaitT < _dibrStartupDelay)
                    {
                        dibrStartupWaitT += Time.deltaTime;
                    }
                    else
                    {
                        if (!_dibrImage.gameObject.activeSelf)
                            _dibrImage.gameObject.SetActive(true);

                        var dibrTex = OpenDibrSessionManager.Instance.FrameReceiver.Texture;
                        if (dibrTex != null) _dibrImage.texture = dibrTex;

                        if (!feedSwitched)
                        {
                            if (_dibrImage.texture != null)
                                fadeInT = Mathf.Min(fadeInT + Time.deltaTime, _dibrFadeInDuration);
                            else
                                dibrReadyWaitT += Time.deltaTime;

                            float alpha = _dibrFadeInDuration > 0f ? fadeInT / _dibrFadeInDuration : 1f;
                            SetAlpha(_dibrImage, alpha);

                            if (alpha >= 1f)
                            {
                                Debug.Log($"[CameraSwitcher] DIBR opaque — swapping feed to {toIdentity}");
                                _streamPlayer.SubscribeTo(toPub);
                                feedSwitched = true;
                            }
                        }
                        else
                        {
                            fadeOutT += Time.deltaTime;
                            float alpha = _dibrFadeInDuration > 0f ? 1f - fadeOutT / _dibrFadeInDuration : 0f;
                            SetAlpha(_dibrImage, Mathf.Max(0f, alpha));
                        }
                    }
                }

                yield return null;
            }

            // DIBR timed out or never activated — instant cut to camera B.
            if (!feedSwitched)
            {
                Debug.Log($"[CameraSwitcher] DIBR deadline passed without activating — instant cut to {toIdentity}");
                _streamPlayer.SubscribeTo(toPub);
            }

            _dibrImage.gameObject.SetActive(false);
            SetAlpha(_dibrImage, 0f);
            _activeSwitch = null;
        }

        private void EnsureDibrLayer()
        {
            if (_dibrImage != null) return;
            var streamRect = _streamPlayer.GetComponent<RawImage>().rectTransform;
            _dibrImage = CreateOverlayRawImage("DibrSyntheticView", streamRect);
            _dibrImage.gameObject.SetActive(false);
        }

        private static RawImage CreateOverlayRawImage(string name, RectTransform matchRect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(matchRect.parent, false);
            go.transform.SetAsLastSibling();

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

        // ── Buttons ──────────────────────────────────────────────────────

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
