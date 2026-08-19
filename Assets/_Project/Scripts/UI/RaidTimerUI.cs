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
            raidClock.ClockChanged += OnClockChanged;
        }

        private void OnDestroy()
        {
            if (raidClock != null)
                raidClock.ClockChanged -= OnClockChanged;
        }

        private void OnClockChanged(RaidClockSnapshot snapshot)
        {
            timeText.text = $"{snapshot.WorldHour:00}:{snapshot.WorldMinute:00}";

            if (snapshot.IsExpired)
                ActivateSunlight();
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
