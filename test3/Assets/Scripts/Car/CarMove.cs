using UnityEngine;

public class CarMove : MonoBehaviour
{
    public float maxSpeed = 30f;
    public float acceleration = 12f;
    public float turnSpeed = 70f;
    public float brakePower = 20f;

    [Header("Road Limit")]
    public LayerMask roadMask;
    public float rayHeight = 2f;
    public float rayDist = 8f;

    [Tooltip("離開路面時回彈強度(越大彈越多)")]
    public float bounceStrength = 6f;

    [Tooltip("回彈時速度衰減(0.6=剩60%)")]
    public float bounceSpeedDamp = 0.6f;

    float currentSpeed = 0f;

    Vector3 lastOnRoadPos;
    Quaternion lastOnRoadRot;
    bool hasLastOnRoad = false;

    bool IsOnRoad()
    {
        Vector3 origin = transform.position + Vector3.up * rayHeight;
        return Physics.Raycast(origin, Vector3.down, rayDist, roadMask);
    }

    void Update()
    {
        float moveInput = 0f;
        float turnInput = 0f;

        if (Input.GetKey(KeyCode.W)) moveInput += 1f;
        if (Input.GetKey(KeyCode.S)) moveInput -= 1f;
        if (Input.GetKey(KeyCode.D)) turnInput += 1f;
        if (Input.GetKey(KeyCode.A)) turnInput -= 1f;

        bool onRoad = IsOnRoad();

        if (onRoad)
        {
            lastOnRoadPos = transform.position;
            lastOnRoadRot = transform.rotation;
            hasLastOnRoad = true;
        }
        else if (hasLastOnRoad)
        {
            Vector3 toSafe = (lastOnRoadPos - transform.position);
            toSafe.y = 0f;

            // 1) 位置往安全點推回（推回更明顯一點）
            if (toSafe.sqrMagnitude > 0.0001f)
                transform.position += toSafe.normalized * (bounceStrength * 1.5f) * Time.deltaTime;

            // 2) 仍然允許轉向（讓你可以自己轉回路上）
            transform.Rotate(Vector3.up * turnInput * turnSpeed * Time.deltaTime);

            // 3) 反彈感：把速度往反方向推一點點，並衰減
            currentSpeed = Mathf.Lerp(currentSpeed, -Mathf.Abs(currentSpeed) * 0.3f, 8f * Time.deltaTime);

            // 4) 讓車慢慢回正（不要太強，不然會像被鎖方向）
            transform.rotation = Quaternion.Slerp(transform.rotation, lastOnRoadRot, 2f * Time.deltaTime);

            // 不 return：讓下面的加減速邏輯仍然可以跑（你按 W 可以脫困）
        }

        else
        {
            // 還沒記到任何合法點之前，先不要擋你操作
            // 讓車可以先動一下，直到第一次在路上為止
        }

        if (moveInput > 0)
            currentSpeed += acceleration * Time.deltaTime;
        else if (moveInput < 0)
            currentSpeed -= brakePower * Time.deltaTime;
        else
            currentSpeed = Mathf.Lerp(currentSpeed, 0, 2f * Time.deltaTime);

        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed * 0.4f, maxSpeed);

        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
        transform.Rotate(Vector3.up * turnInput * turnSpeed * Time.deltaTime);
    }
}
