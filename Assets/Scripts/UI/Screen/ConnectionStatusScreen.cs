using System;
using Thesis.Managers;
using Thesis.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thesis.UI.Screens
{
    public class ConnectionStatusScreen : BaseScreen
    {
        [Header("References")]
        [SerializeField] private RawImage _cameraPreview;
        [SerializeField] private AspectRatioFitter _aspectFitter;
        [SerializeField] private TMP_Text _statusText;

        public override void Init() => base.Init();

        public override void Show(object data = null)
        {
            base.Show(data);

            if (_statusText != null && RegistrationClient.HasInstance)
                _statusText.text = $"Streaming as {RegistrationClient.Instance.Identity}";

            if (LiveKitCameraPublisher.HasInstance)
                ApplyPreviewTexture(LiveKitCameraPublisher.Instance.Texture);
        }

        public override void Hide(Action onComplete = null)
        {
            if (_cameraPreview != null) _cameraPreview.texture = null;
            base.Hide(onComplete);
        }

        private void ApplyPreviewTexture(WebCamTexture texture)
        {
            if (_cameraPreview == null || texture == null) return;

            _cameraPreview.texture = texture;

            var rt = _cameraPreview.rectTransform;
            rt.localEulerAngles = new Vector3(0f, 0f, -texture.videoRotationAngle);
            rt.localScale = new Vector3(rt.localScale.x, texture.videoVerticallyMirrored ? -1f : 1f, 1f);

            if (_aspectFitter != null && texture.height > 0)
            {
                bool rotated = texture.videoRotationAngle == 90 || texture.videoRotationAngle == 270;
                float width  = rotated ? texture.height : texture.width;
                float height = rotated ? texture.width  : texture.height;
                _aspectFitter.aspectRatio = width / height;
            }
        }
    }
}
