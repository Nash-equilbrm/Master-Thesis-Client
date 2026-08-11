using System;
using UnityEngine;

namespace Thesis.UI
{
    public class BaseScreen : BaseUIElement
    {
        public override void Init()
        {
            base.Init();
            uiType = UIType.Screen;
            var rt = GetComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public override void Show(object data) => base.Show(data);
        public override void Hide(Action onComplete = null) => base.Hide(onComplete);
    }
}
