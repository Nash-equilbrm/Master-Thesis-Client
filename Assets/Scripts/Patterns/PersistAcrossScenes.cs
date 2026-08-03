using UnityEngine;

namespace Thesis.Patterns
{
    public class PersistAcrossScenes : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
