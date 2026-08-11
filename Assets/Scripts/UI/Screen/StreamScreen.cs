using System;
using Thesis.Stream;
using Thesis.UI;

namespace Thesis.UI.Screens
{
    public class StreamScreen : BaseScreen
    {
        [UnityEngine.Header("References")]
        [UnityEngine.SerializeField] private CameraStreamPlayer _streamPlayer;
        [UnityEngine.SerializeField] private CameraSwitcher _cameraSwitcher;

        public override void Hide(Action onComplete = null)
        {
            _streamPlayer?.Unsubscribe();
            base.Hide(onComplete);
        }
    }
}
