using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPCCarSpawner : MonoBehaviour
{
    [Header("生成設定")]
    public GameObject carPrefab;
    public float spawnInterval = 4f;
    public int maxCars = 10;

    [Header("出生點的節點 (可放多個)")]
    public List<TrafficNode> startNodes; // 把地圖上隨便幾個 Node 丟進來當出生點

    private int currentCarCount = 0;

    void Start()
    {
        StartCoroutine(SpawnCarRoutine());
    }

    IEnumerator SpawnCarRoutine()
    {
        while (currentCarCount < maxCars)
        {
            if (startNodes.Count > 0)
            {
                // 隨機挑選一個出生點
                TrafficNode startNode = startNodes[Random.Range(0, startNodes.Count)];

                GameObject newCar = Instantiate(carPrefab, startNode.transform.position, startNode.transform.rotation);

                NPC_WaypointDrive driveScript = newCar.GetComponent<NPC_WaypointDrive>();
                if (driveScript != null)
                {
                    // 告訴新車子：你的第一站，就是出生點的「下一站」
                    driveScript.targetNode = startNode.GetNextNode();
                }
                currentCarCount++;
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}