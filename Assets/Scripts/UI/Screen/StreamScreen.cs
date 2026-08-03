using Thesis.Stream;
using Thesis.UI;
using UnityEngine;

namespace Thesis.UI.Screens
{
    public class StreamScreen : BaseScreen
    {
        [Header("References")]
        [SerializeField] private CameraStreamPlayer _streamPlayer;
        [SerializeField] private CameraSwitcher _cameraSwitcher;

        public override void Hide()
        {
            _streamPlayer?.Unsubscribe();
            base.Hide();
        }
    }
}
