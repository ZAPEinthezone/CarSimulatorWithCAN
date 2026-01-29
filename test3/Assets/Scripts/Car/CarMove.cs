using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarMove : MonoBehaviour
{
    [Header("移動參數")]
    public float maxSpeed = 25f;       // 稍微下修極速，增加操控性
    public float acceleration = 8f;    // 調低加速度，解決加速太快的問題
    public float turnSpeed = 70f;
    public float brakePower = 20f;

    [Header("自動對齊設定")]
    public LayerMask roadMask;         // 務必在 Inspector 選擇 "Road" 圖層
    public float rayStartHeight = 10f;

    private float currentSpeed = 0f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 防止碰撞時車子像不倒翁一樣亂翻
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // 遊戲開始時自動對齊路面
        PlaceOnRoad();
    }

    void Update()
    {
        // 1. 讀取輸入
        float moveInput = 0f;
        float turnInput = 0f;

        if (Input.GetKey(KeyCode.W)) moveInput += 1f;
        if (Input.GetKey(KeyCode.S)) moveInput -= 1f;
        if (Input.GetKey(KeyCode.D)) turnInput += 1f;
        if (Input.GetKey(KeyCode.A)) turnInput -= 1f;

        // 2. 處理加速與減速 (使用 MoveTowards 讓數值增加更平穩)
        if (moveInput > 0)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
        }
        else if (moveInput < 0)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, -maxSpeed * 0.4f, brakePower * Time.deltaTime);
        }
        else
        {
            // 放開按鍵時的自然滑行感
            currentSpeed = Mathf.Lerp(currentSpeed, 0, 1.5f * Time.deltaTime);
        }

        // 3. 處理轉向 (在速度極低時降低轉向力，模擬真實感)
        float speedFactor = Mathf.Clamp01(rb.velocity.magnitude / 2f);
        transform.Rotate(Vector3.up * turnInput * turnSpeed * speedFactor * Time.deltaTime);
    }

    void FixedUpdate()
    {
        // 4. 使用物理力移動車子，這能讓 CarSpeedDetector 正確抓到速度
        // 並且能與 NavMeshGuard 的位置修正邏輯完美相容
        Vector3 desiredVelocity = transform.forward * currentSpeed;

        // 保留垂直方向的速度（例如重力造成的下墜或跳躍）
        desiredVelocity.y = rb.velocity.y;

        rb.velocity = desiredVelocity;
    }

    // 自動將車子放置於路面上
    [ContextMenu("立即對齊路面")]
    public void PlaceOnRoad()
    {
        Ray ray = new Ray(transform.position + Vector3.up * rayStartHeight, Vector3.down);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayStartHeight * 2f, roadMask))
        {
            transform.position = hit.point + Vector3.up * 0.2f; // 稍微懸空防止卡進地板
            Debug.Log("CarMove: 已自動對齊路面");
        }
        else
        {
            Debug.LogWarning("CarMove: 找不到路面，請確認 Road Mask 是否勾選了 map 所在的圖層");
        }
    }
}