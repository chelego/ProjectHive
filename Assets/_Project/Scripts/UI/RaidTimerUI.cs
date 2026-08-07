using UnityEngine;
using TMPro;
using ProjectHive.Core.Contracts;
using ProjectHive.Core.Runtime;
using System.Collections;

namespace ProjectHive.UI
{
    public class RaidTimerUI : MonoBehaviour
    {
        public TextMeshProUGUI timeText;
        public RaidClock raidClock;
        public GameObject sunLight; // New reference

        private void Start()
        {
            raidClock.OnTimeUpdated += UpdateTimeDisplay;
            raidClock.OnTimeExpired += ActivateSunlight;
        }

        private void UpdateTimeDisplay(float elapsedSeconds)
        {
            // Map 0-10s to 11pm (23:00) - 6am (06:00)
            float totalMinutes = (elapsedSeconds / 10f) * 7f * 60f;
            int hours = (23 + (int)(totalMinutes / 60f)) % 24;
            int minutes = (int)(totalMinutes % 60f);
            timeText.text = $"{hours:00}:{minutes:00}";
        }

        private void ActivateSunlight()
        {
            if (sunLight != null)
            {
                StartCoroutine(FadeInSunlight());
            }
        }

        private IEnumerator FadeInSunlight()
        {
            sunLight.SetActive(true);
            Light light = sunLight.GetComponent<Light>();
            light.intensity = 0;
            
            float duration = 5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                light.intensity = Mathf.Lerp(0, 10f, elapsed / duration);
                yield return null;
            }
            light.intensity = 10f;
        }
    }
}
