using UnityEngine;

namespace Thesis.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class BaseUIElement : MonoBehaviour
    {
        protected CanvasGroup canvasGroup;
        protected UIType uiType = UIType.Unknow;
        protected bool isHide;
        private bool _isInited;

        public bool IsInited => _isInited;
        public bool IsHide => isHide;
        public CanvasGroup CanvasGroup => canvasGroup;
        public UIType UIType => uiType;

        public virtual void Init()
        {
            if (_isInited) return;
            _isInited = true;
            if (!gameObject.GetComponent<CanvasGroup>())
                gameObject.AddComponent<CanvasGroup>();
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            gameObject.SetActive(false);
        }

        public virtual void Show(object data)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            isHide = false;
            SetActiveCanvasGroup(true);
        }

        public virtual void Hide()
        {
            isHide = true;
            SetActiveCanvasGroup(false);
        }

        private void SetActiveCanvasGroup(bool isActive)
        {
            if (CanvasGroup == null) return;
            CanvasGroup.blocksRaycasts = isActive;
            CanvasGroup.alpha = isActive ? 1 : 0;
        }
    }
}
