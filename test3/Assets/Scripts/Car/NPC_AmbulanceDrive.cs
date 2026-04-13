using UnityEngine;
using UnityEngine.AI;

public class NPC_AmbulanceDrive : NPC_WaypointDrive
{
    [Header("緊急狀態控制")]
    public bool isEmergency = false; 

    [Header("救護車特效組件")]
    public GameObject sirenLights;   
    public AudioSource sirenAudio;   

    private float normalSpeed;

    void Awake()
    {
        // 確保在 Start 之前抓到 agent
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) normalSpeed = agent.speed;

        // 初始設定：先關閉音效與燈光
        if (sirenLights != null) sirenLights.SetActive(false);
        if (sirenAudio != null) {
            sirenAudio.loop = true; // 救護車音效通常要循環
            sirenAudio.Stop();
        }
    }

    protected override void Update()
    {
        if (targetNode == null || agent == null) return;

        // 🚑 處理緊急狀態的邏輯切換
        HandleEmergencyEffects();

        if (isEmergency) 
        {
            // 🚨 強制加速
            agent.speed = normalSpeed * 2.0f; 

            // 🚨 執行避撞檢查
            CheckForwardCollision();

            // 🚨 重要：無視紅綠燈邏輯
            if (!IsPathBlockedByCar()) 
            {
                agent.isStopped = false; 
            }
        } 
        else 
        {
            // 🏠 一般模式：變回普通車，執行基類的紅綠燈判斷
            agent.speed = normalSpeed;
            base.Update(); 
            return; 
        }

        // --- 緊急模式下的尋路 ---
        if (!agent.isStopped && !agent.pathPending && agent.remainingDistance < 2f) 
        {
            TrafficNode nextNode = targetNode.GetNextNode(); 
            if (nextNode != null) 
            {
                targetNode = nextNode; 
                agent.SetDestination(targetNode.transform.position);
            }
            else 
            {
                Destroy(gameObject);
            }
        }
    }

    // 🔊 新增：獨立處理音效與燈光的邏輯
    private void HandleEmergencyEffects()
    {
        if (isEmergency)
        {
            // 開啟燈光
            if (sirenLights != null && !sirenLights.activeSelf) 
                sirenLights.SetActive(true);

            // 播放音效
            if (sirenAudio != null && !sirenAudio.isPlaying) 
                sirenAudio.Play();
        }
        else
        {
            // 關閉燈光
            if (sirenLights != null && sirenLights.activeSelf) 
                sirenLights.SetActive(false);

            // 停止音效
            if (sirenAudio != null && sirenAudio.isPlaying) 
                sirenAudio.Stop();
        }
    }

    private bool IsPathBlockedByCar() 
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, sensorLength)) 
        {
            if (hit.collider.CompareTag("Car")) return true;
        }
        return false;
    }
}