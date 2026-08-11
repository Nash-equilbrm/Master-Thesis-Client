using DG.Tweening;
using UnityEngine;

namespace Thesis.UI
{
    public enum UIAnimType { Fade, Scale, Slide }

    [System.Serializable]
    public abstract class UIAnim
    {
        public float duration = 0.3f;
        public Ease  ease     = Ease.OutCubic;

        public abstract UIAnimType AnimType { get; }

        /// <summary>Sets the element to its pre-show start state before the tween plays.</summary>
        public abstract void SetShowStart(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos);

        public abstract Tween BuildShowTween(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos);
        public abstract Tween BuildHideTween(RectTransform rt, CanvasGroup cg, Vector3 restScale, Vector2 restPos);
    }
}
