using UnityEngine;

public class CarSpeedDetector : MonoBehaviour
{
    private Rigidbody rb;
    public float currentSpeedKmh;

    void Start()
    {
        // 取得車子的 Rigidbody 組件
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (rb != null)
        {
            // rb.velocity.magnitude 是公尺/秒 (m/s)
            // 轉換為 km/h: (m/s) * 3.6
            float speedMps = rb.velocity.magnitude;
            currentSpeedKmh = speedMps * 3.6f;
        }
    }
}