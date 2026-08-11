using DG.Tweening;
using UnityEngine;

namespace Thesis.UI
{
    [System.Serializable]
    public class FadeAnim : UIAnim
    {
        public override UIAnimType AnimType => UIAnimType.Fade;

        public override void SetShowStart(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => cg.alpha = 0f;

        public override Tween BuildShowTween(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => cg.DOFade(1f, duration).SetEase(ease);

        public override Tween BuildHideTween(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => cg.DOFade(0f, duration).SetEase(ease);
    }
}
