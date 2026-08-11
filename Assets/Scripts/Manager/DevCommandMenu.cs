using Thesis.UI.Popups;
using UnityEngine;

namespace Thesis.Managers
{
    public class DevCommandMenu : MonoBehaviour
    {
        private Reporter _reporter;

        private void Start()
        {
            _reporter = FindObjectOfType<Reporter>();
            if (_reporter != null)
                _reporter.OnGestureDetected = OnGestureDetected;
        }

        private void OnGestureDetected()
        {
            UIManager.Instance.ShowPopup<DevCommandPopup>(forceShow: true);
        }

        public void OpenReporter()
        {
            if (_reporter != null)
                _reporter.doShow();
        }
    }
}
