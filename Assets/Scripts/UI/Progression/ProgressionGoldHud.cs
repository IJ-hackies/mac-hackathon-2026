using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    [DisallowMultipleComponent]
    public sealed class ProgressionGoldHud : MonoBehaviour
    {
        [SerializeField] private ProgressionDataAdapter progression;
        [SerializeField] private Text valueText;
        [SerializeField] private string prefix = "G ";

        public void Configure(Text target) => valueText = target;
        public void Bind(MonoBehaviour source)
        {
            if (progression == null) progression = GetComponent<ProgressionDataAdapter>();
            if (progression != null) progression.Bind(source);
        }

        private void OnEnable()
        {
            if (progression == null) progression = GetComponent<ProgressionDataAdapter>();
            if (progression != null) progression.Refreshed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (progression != null) progression.Refreshed -= Refresh;
        }

        public void Refresh()
        {
            if (valueText != null) valueText.text = prefix + (progression != null ? progression.Gold : 0);
        }
    }
}
