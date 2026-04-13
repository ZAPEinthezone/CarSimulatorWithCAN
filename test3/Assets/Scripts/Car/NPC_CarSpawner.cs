using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPC_CarSpawner : MonoBehaviour
{
    [Header("生成設定")]
    public GameObject ambulancePrefab; // 救護車 Prefab
    public GameObject[] carPrefabs;    // 普通車 Prefab
    public float spawnInterval = 3f;
    public int maxCars = 150;

    [Header("出生點的節點")]
    public List<TrafficNode> startNodes;

    [Header("攝影機跟隨設定")]
    // 💡 這裡改成對應新的類別名稱 AmbulanceCameraFollow
    public AmbulanceCameraFollow cameraFollowScript; 

    private bool hasAmbulanceSpawned = false;

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            GameObject[] currentCars = GameObject.FindGameObjectsWithTag("Car");
            int currentCount = currentCars.Length;

            if (currentCount < maxCars && startNodes.Count > 0)
            {
                foreach (TrafficNode startNode in startNodes)
                {
                    if (currentCount >= maxCars) break;

                    GameObject prefabToSpawn;
                    
                    // ✨ 邏輯：如果還沒生過救護車，第一台就生救護車
                    if (!hasAmbulanceSpawned && ambulancePrefab != null)
                    {
                        prefabToSpawn = ambulancePrefab;
                        GameObject newAmb = Instantiate(prefabToSpawn, startNode.transform.position, startNode.transform.rotation);
                        
                        // 設定導航目標
                        NPC_AmbulanceDrive drive = newAmb.GetComponent<NPC_AmbulanceDrive>();
                        if (drive != null) drive.targetNode = startNode;

                        // 🎥 關鍵：呼叫新的攝影機腳本鎖定這台救護車
                        if (cameraFollowScript != null)
                        {
                            cameraFollowScript.SetTarget(newAmb.transform);
                        }

                        hasAmbulanceSpawned = true;
                    }
                    else
                    {
                        // 生普通車
                        prefabToSpawn = carPrefabs[Random.Range(0, carPrefabs.Length)];
                        GameObject newCar = Instantiate(prefabToSpawn, startNode.transform.position, startNode.transform.rotation);
                        
                        NPC_WaypointDrive drive = newCar.GetComponent<NPC_WaypointDrive>();
                        if (drive != null) drive.targetNode = startNode;
                    }

                    currentCount++;
                }
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}