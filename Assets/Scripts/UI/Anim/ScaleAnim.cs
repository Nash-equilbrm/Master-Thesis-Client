using DG.Tweening;
using UnityEngine;

namespace Thesis.UI
{
    [System.Serializable]
    public class ScaleAnim : UIAnim
    {
        [Range(0f, 2f)] public float from = 0.9f;  // start scale for show
        [Range(0f, 2f)] public float to   = 0.9f;  // end scale for hide

        public override UIAnimType AnimType => UIAnimType.Scale;

        public override void SetShowStart(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => rt.localScale = restScale * from;

        public override Tween BuildShowTween(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => rt.DOScale(restScale, duration).SetEase(ease);

        public override Tween BuildHideTween(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos)
            => rt.DOScale(restScale * to, duration).SetEase(ease);
    }
}
