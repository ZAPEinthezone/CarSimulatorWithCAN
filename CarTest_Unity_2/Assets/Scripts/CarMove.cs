using UnityEngine;
using UnityEngine.InputSystem;

public class CarMove : MonoBehaviour
{
    public float maxSpeed = 30f;      // 最高速度
    public float acceleration = 12f;  // 加速度
    public float turnSpeed = 70f;     // 轉向速度
    public float brakePower = 20f;    // 煞車力

    float currentSpeed = 0f;

    void Update()
    {
        float moveInput = 0f;
        float turnInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput -= 1f;
            if (Keyboard.current.dKey.isPressed) turnInput += 1f;
            if (Keyboard.current.aKey.isPressed) turnInput -= 1f;
        }

        // 加速 / 減速
        if (moveInput > 0)
            currentSpeed += acceleration * Time.deltaTime;
        else if (moveInput < 0)
            currentSpeed -= brakePower * Time.deltaTime;
        else
            currentSpeed = Mathf.Lerp(currentSpeed, 0, 2f * Time.deltaTime);

        // 限制最大速度
        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed * 0.4f, maxSpeed);

        // 移動與轉向
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
        transform.Rotate(Vector3.up * turnInput * turnSpeed * Time.deltaTime);
    }
}

