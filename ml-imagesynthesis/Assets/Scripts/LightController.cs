using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightController : MonoBehaviour
{
    public SampleCountController count;
    private int trainingImages;
    private int testImages;
    private int frameCount = 0;
    private float duration = 0.5f;

    private Light lt;
    // Start is called before the first frame update
    void Start()
    {
        trainingImages = count.trainingImages;
        testImages = count.testImages;

        lt = GetComponent<Light>();
    }

    // Update is called once per frame
    void Update()
    {
        if (frameCount < trainingImages + testImages)
        {
            // Rotation
            lt.transform.rotation = Random.rotation;

            // Color change
            float t = Mathf.PingPong(Time.time, duration) / duration;
            Color color0 = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
            Color color1 = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
            lt.color = Color.Lerp(color0, color1, t);
            
            frameCount++;
        }
    }
}
