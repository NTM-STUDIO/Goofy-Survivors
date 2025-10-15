using UnityEngine;

public class DayNightController : MonoBehaviour
{
    public Light sun;
    public float dayLengthInMinutes = 1.0f;
    [Range(0, 1)]
    public float timeOfDay;
    public Gradient skyColor;
    public Gradient ambientColor;

    private void Update()
    {
        timeOfDay += Time.deltaTime / (dayLengthInMinutes * 60);
        if (timeOfDay >= 1)
        {
            timeOfDay = 0;
        }

        sun.transform.localRotation = Quaternion.Euler(new Vector3((timeOfDay * 360f) - 90f, 170f, 0));
        RenderSettings.skybox.SetColor("_Tint", skyColor.Evaluate(timeOfDay));
        RenderSettings.ambientIntensity = ambientColor.Evaluate(timeOfDay).a;
        RenderSettings.ambientLight = ambientColor.Evaluate(timeOfDay);
    }
}