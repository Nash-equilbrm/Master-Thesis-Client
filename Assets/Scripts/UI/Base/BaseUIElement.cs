using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Thesis.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class BaseUIElement : MonoBehaviour
    {
        [Header("Animations")]
        [SerializeField] private UIAnimConfig _showAnim = new UIAnimConfig();
        [SerializeField] private UIAnimConfig _hideAnim = new UIAnimConfig();

        protected CanvasGroup canvasGroup;
        protected UIType uiType = UIType.Unknow;
        protected bool isHide;
        private bool _isInited;

        private RectTransform _rt;
        private Vector2 _restPosition;
        private Vector3 _restScale;
        private Sequence _tweenSeq;
        private readonly List<Action> _pendingHideCallbacks = new List<Action>();

        public bool IsInited => _isInited;
        public bool IsHide => isHide;
        public CanvasGroup CanvasGroup => canvasGroup;
        public UIType UIType => uiType;

        public virtual void Init()
        {
            if (_isInited) return;
            _isInited = true;

            canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _rt = GetComponent<RectTransform>();
            _restPosition = _rt.anchoredPosition;
            _restScale    = transform.localScale;

            canvasGroup.alpha          = 0f;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public virtual void Show(object data)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            isHide = false;
            canvasGroup.blocksRaycasts = true;
            AnimateShow(_showAnim);
        }

        public virtual void Hide(Action onComplete = null)
        {
            if (isHide)
            {
                // Already hidden or hiding — don't restart the animation (which would
                // Kill() the in-flight tween and silently drop its onComplete). Just
                // queue this callback to run alongside/after the existing one.
                if (onComplete == null) return;
                if (_tweenSeq != null && _tweenSeq.IsActive())
                    _pendingHideCallbacks.Add(onComplete);
                else
                    onComplete.Invoke();
                return;
            }

            isHide = true;
            canvasGroup.blocksRaycasts = false;
            AnimateHide(_hideAnim, onComplete);
        }

        // ── Core animation ───────────────────────────────────────────────────

        protected virtual void OnDestroy()
        {
            _tweenSeq?.Kill(false);
        }

        private void AnimateShow(UIAnimConfig cfg)
        {
            _tweenSeq?.Kill(false);
            _tweenSeq = null;
            _pendingHideCallbacks.Clear();

            canvasGroup.alpha    = 1f;
            transform.localScale = _restScale;
            _rt.anchoredPosition = _restPosition;

            if (!HasAnims(cfg)) return;

            foreach (var anim in cfg.anims)
                anim?.SetShowStart(_rt, canvasGroup, _restScale, _restPosition);

            _tweenSeq = DOTween.Sequence().SetUpdate(true);
            foreach (var anim in cfg.anims)
                if (anim != null)
                    _tweenSeq.Join(anim.BuildShowTween(_rt, canvasGroup, _restScale, _restPosition));

            _tweenSeq.Play();
        }

        private void AnimateHide(UIAnimConfig cfg, Action onComplete)
        {
            _tweenSeq?.Kill(false);
            _tweenSeq = null;

            if (!HasAnims(cfg))
            {
                canvasGroup.alpha    = 0f;
                transform.localScale = _restScale;
                _rt.anchoredPosition = _restPosition;
                gameObject.SetActive(false);
                onComplete?.Invoke();
                FlushPendingHideCallbacks();
                return;
            }

            _tweenSeq = DOTween.Sequence().SetUpdate(true);
            foreach (var anim in cfg.anims)
                if (anim != null)
                    _tweenSeq.Join(anim.BuildHideTween(_rt, canvasGroup, _restScale, _restPosition));

            _tweenSeq.OnComplete(() =>
            {
                if (this == null) return;
                transform.localScale = _restScale;
                _rt.anchoredPosition = _restPosition;
                gameObject.SetActive(false);
                onComplete?.Invoke();
                FlushPendingHideCallbacks();
            }).Play();
        }

        private void FlushPendingHideCallbacks()
        {
            if (_pendingHideCallbacks.Count == 0) return;
            var callbacks = _pendingHideCallbacks.ToArray();
            _pendingHideCallbacks.Clear();
            foreach (var cb in callbacks)
                cb?.Invoke();
        }

        private static bool HasAnims(UIAnimConfig cfg)
        {
            if (cfg == null || !cfg.enabled || cfg.anims == null) return false;
            foreach (var a in cfg.anims)
                if (a != null) return true;
            return false;
        }
    }
}
