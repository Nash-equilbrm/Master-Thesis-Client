using System;

namespace Thesis.UI
{
    public class BaseNotify : BaseUIElement
    {
        public override void Init()
        {
            base.Init();
            uiType = UIType.Notify;
        }

        public override void Show(object data) => base.Show(data);
        public override void Hide(Action onComplete = null) => base.Hide(onComplete);
    }
}
