using System.Collections.Generic;
using UnityEngine;

namespace Thesis.UI
{
    [System.Serializable]
    public class UIAnimConfig
    {
        public bool enabled = true;

        [SerializeReference]
        public List<UIAnim> anims = new List<UIAnim> { new FadeAnim() };
    }
}
