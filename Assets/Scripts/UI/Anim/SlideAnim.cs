using DG.Tweening;
using UnityEngine;

namespace Thesis.UI
{
    public enum SlideDirection { FromBottom, FromTop, FromLeft, FromRight }

    [System.Serializable]
    public class SlideAnim : UIAnim
    {
        public SlideDirection direction = SlideDirection.FromBottom;
        public float          offset    = 150f;

        public override UIAnimType AnimType => UIAnimType.Slide;

        private Vector2 GetOffset() => direction switch
        {
            SlideDirection.FromBottom => Vector2.down  * offset,
            SlideDirection.FromTop    => Vector2.up    * offset,
            SlideDirection.FromLeft   => Vector2.left  * offset,
            SlideDirection.FromRight  => Vector2.right * offset,
            _                         => Vector2.down  * offset,
        };

        public override void SetShowStart(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => rt.anchoredPosition = restPos + GetOffset();

        public override Tween BuildShowTween(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => rt.DOAnchorPos(restPos, duration).SetEase(ease);

        public override Tween BuildHideTween(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => rt.DOAnchorPos(restPos + GetOffset(), duration).SetEase(ease);
    }
}
