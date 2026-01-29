using UnityEngine;
using UnityEngine.AI;

public class NavMeshGuard : MonoBehaviour
{
    private Vector3 lastValidPos;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        lastValidPos = transform.position;
    }

    void LateUpdate()
    {
        NavMeshHit hit;
        // 檢查車子目前的座標下方是否有 NavMesh (藍色區域)
        // 1.0f 是偵測半徑
        if (NavMesh.SamplePosition(transform.position, out hit, 1.0f, NavMesh.AllAreas))
        {
            // 在路上，更新最後安全點
            lastValidPos = transform.position;
        }
        else
        {
            // 不在路上，強制拉回
            transform.position = lastValidPos;
            if (rb != null) rb.velocity = Vector3.zero;
        }
    }
}