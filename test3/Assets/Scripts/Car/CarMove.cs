using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarMove : MonoBehaviour
{
    [Header("基礎移動參數")]
    public float maxSpeed = 100f;        // 配合你設定的 100 km/h
    public float baseAcceleration = 8f;  // 基礎加速度
    public float turnSpeed = 50f;        // 轉向速度
    public float brakePower = 20f;       // 建議設為 20 才有感

    [Header("現實加速曲線設定 (參考 RPM 圖表)")]
    [Tooltip("低速區間 (km/h)")] public float lowSpeedLimit = 8f;
    [Tooltip("穩定加速區間 (km/h)")] public float midSpeedLimit = 18f;
    [Tooltip("高速衰減倍率")] public float highSpeedMult = 0.4f;

    [Header("油門反應設定 (解決加速太快)")]
    [Tooltip("數值越小，油門反應越慢、越重。建議 0.5~1.5")]
    public float throttleResponse = 0.8f;
    private float smoothThrottle = 0f;

    [Header("自動對齊與偵測")]
    public LayerMask roadMask;           // 必須選取 "Road" 圖層
    public float rayStartHeight = 10f;

    private float currentSpeed = 0f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 1. 設定剛體屬性
        rb.mass = 1500;
        rb.drag = 1f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // 2. 遊戲開始自動對齊地面
        PlaceOnRoad();
    }

    void Update()
    {
        // 3. 讀取輸入與油門平滑化 (延長偵測感)
        float targetInput = 0f;
        if (Input.GetKey(KeyCode.W)) targetInput = 1f;
        if (Input.GetKey(KeyCode.S)) targetInput = -1f;

        // 模擬踏板踩下的過程，讓加速不會瞬間爆發
        smoothThrottle = Mathf.MoveTowards(smoothThrottle, targetInput, throttleResponse * Time.deltaTime);

        float turnInput = 0f;
        if (Input.GetKey(KeyCode.D)) turnInput = 1f;
        if (Input.GetKey(KeyCode.A)) turnInput = -1f;

        // 4. 計算擬真加速度 (動態扭力)
        if (smoothThrottle > 0)
        {
            float dynamicAccel = baseAcceleration;
            float absSpeed = Mathf.Abs(currentSpeed);

            if (absSpeed < lowSpeedLimit) dynamicAccel *= 1.6f;      // 起步大扭力
            else if (absSpeed < midSpeedLimit) dynamicAccel *= 0.9f; // 中速穩定
            else dynamicAccel *= highSpeedMult;                     // 高速衰減

            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, dynamicAccel * smoothThrottle * Time.deltaTime);
        }
        else if (smoothThrottle < 0)
        {
            // 煞車與倒車
            currentSpeed = Mathf.MoveTowards(currentSpeed, -maxSpeed * 0.3f, brakePower * Time.deltaTime);
        }
        else
        {
            // 自然滑行感
            currentSpeed = Mathf.Lerp(currentSpeed, 0, 1.2f * Time.deltaTime);
        }

        // 5. 處理轉向 (速度越快越沉穩)
        float speedFactor = Mathf.Clamp01(rb.velocity.magnitude / 5f);
        transform.Rotate(Vector3.up * turnInput * turnSpeed * speedFactor * Time.deltaTime);
    }

    void FixedUpdate()
    {
        // 6. 物理驅動位移
        Vector3 desiredVelocity = transform.forward * currentSpeed;
        desiredVelocity.y = rb.velocity.y; // 保留重力
        rb.velocity = desiredVelocity;
    }

    [ContextMenu("立即對齊路面")]
    public void PlaceOnRoad()
    {
        Ray ray = new Ray(transform.position + Vector3.up * rayStartHeight, Vector3.down);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayStartHeight * 2f, roadMask))
        {
            transform.position = hit.point + Vector3.up * 0.2f; // 避免埋入地板
            Debug.Log("CarMove: 已成功自動對齊路面");
        }
        else
        {
            Debug.LogWarning("CarMove: 找不到 Road 圖層，請確認 Road Mask 設定");
        }
    }
}