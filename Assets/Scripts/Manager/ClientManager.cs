using Thesis.Patterns;
using Thesis.UI.Screens;
using UnityEngine;

namespace Thesis.Managers
{
    public class ClientManager : Singleton<ClientManager>
    {
        protected override void Awake()
        {
            base.Awake();
        }

        void Start()
        {
            if (LiveKitManager.HasInstance)
                LiveKitManager.Instance.OnDisconnected += OnDisconnected;

            UIManager.Instance.ShowScreen<ConnectionScreen>(forceShow: true);
        }

        void OnDestroy()
        {
            if (LiveKitManager.HasInstance)
                LiveKitManager.Instance.OnDisconnected -= OnDisconnected;
        }

        private void OnDisconnected()
        {
            UIManager.Instance.ShowScreen<ConnectionScreen>("Disconnected. Enter server URL to reconnect.", forceShow: true);
        }
    }
}
