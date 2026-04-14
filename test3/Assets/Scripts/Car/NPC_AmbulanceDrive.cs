using UnityEngine;
using UnityEngine.AI;
using System.Reflection;

public class NPC_AmbulanceDrive : NPC_WaypointDrive
{
    [Header("緊急狀態控制")]
    public bool isEmergency = false; 
    public float emergencySpeed = 15.0f;
    public float detectRadius = 25f;   // 周圍偵測半徑
    public float intersectionDist = 50f; // 前方路口偵測距離

    [Header("特效組件")]
    public GameObject sirenLights;   
    public AudioSource sirenAudio;   

    private float normalSpeed;

    void Awake() {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) normalSpeed = agent.speed;
    }

    protected override void Update() {
        if (targetNode == null || agent == null) return;
        HandleEffects();

        if (isEmergency) {
            agent.speed = emergencySpeed;
            agent.angularSpeed = 1000f; // 轉向極高，確保走在軌道上
            agent.acceleration = 30f;  

            // 執行避讓廣播
            NotifyNearbyCars();
            
            // 防撞偵測：緊急模式下距離調短，避免誤停
            CheckForwardCollisionCustom(3.5f); 
        } else {
            agent.speed = normalSpeed;
            base.Update(); 
            return;
        }

        // 導航邏輯
        if (!agent.isStopped && !agent.pathPending && agent.remainingDistance < 2f) {
            TrafficNode nextNode = targetNode.GetNextNode(); 
            if (nextNode != null) {
                targetNode = nextNode;
                agent.SetDestination(targetNode.transform.position);
            } else {
                Destroy(gameObject);
            }
        }
    }

    private void NotifyNearbyCars() {
        // 1. 周圍同向車輛靠右避讓
        Collider[] nearby = Physics.OverlapSphere(transform.position, detectRadius);
        foreach (var col in nearby) {
            if (col.CompareTag("Car") && col.gameObject != gameObject) {
                float directionMatch = Vector3.Dot(transform.forward, col.transform.forward);
                if (directionMatch > 0.5f) { // 確保是順向車
                    col.SendMessage("YieldForAmbulance", transform.position, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        // 2. 前方遠處路口車輛原地煞停 (清空路口)
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, 6f, transform.forward, intersectionDist);
        foreach (var hit in hits) {
            if (hit.collider.CompareTag("Car") && hit.collider.gameObject != gameObject) {
                hit.collider.SendMessage("IntersectionYield", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    protected void CheckForwardCollisionCustom(float dist) {
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, dist)) {
            if (hit.collider.CompareTag("Car")) {
                agent.isStopped = true;
                return;
            }
        }
        agent.isStopped = false;
    }

    private void HandleEffects() {
        if (sirenLights != null && sirenLights.activeSelf != isEmergency)
            sirenLights.SetActive(isEmergency);
        
        if (isEmergency && sirenLights != null) {
            LightManager manager = sirenLights.GetComponent<LightManager>();
            if (manager != null) {
                FieldInfo field = typeof(LightManager).GetField("sirenMode", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(manager, 2); 
                Light[] lights = sirenLights.GetComponentsInChildren<Light>(true);
                foreach (Light l in lights) l.enabled = true;
            }
        }
        if (sirenAudio != null) {
            if (isEmergency && !sirenAudio.isPlaying) sirenAudio.Play();
            else if (!isEmergency && sirenAudio.isPlaying) sirenAudio.Stop();
        }
    }
}