using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 4, -8);
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desiredPosition = target.position + target.rotation * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
