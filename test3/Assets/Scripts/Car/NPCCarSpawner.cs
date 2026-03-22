using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPC_CarSpawner : MonoBehaviour
{
    [Header("生成設定")]
    public GameObject[] carPrefabs;
    public float spawnInterval = 3f; // ⚠️ 注意：因為一次生很多台，間隔建議拉長一點
    public int maxCars = 150;

    [Header("出生點的節點 (可放多個)")]
    public List<TrafficNode> startNodes;

    void Start()
    {
        StartCoroutine(SpawnCarRoutine());
    }

    IEnumerator SpawnCarRoutine()
    {
        while (true)
        {
            // 先數一下場上有幾台車
            GameObject[] currentCars = GameObject.FindGameObjectsWithTag("Car");
            int currentCount = currentCars.Length;

            // 如果還沒達到上限，就開始「同步」生車！
            if (carPrefabs.Length > 0 && startNodes.Count > 0 && currentCount < maxCars)
            {
                // 🚀 核心魔法：對著清單裡的「每一個」出生點發送生車指令！
                foreach (TrafficNode startNode in startNodes)
                {
                    // 保險機制：如果在迴圈中數量爆表了，就立刻煞車停止生成
                    if (currentCount >= maxCars) break;

                    // 隨機抽一台車款
                    GameObject randomCarPrefab = carPrefabs[Random.Range(0, carPrefabs.Length)];

                    // 在這個出生點生出車子
                    GameObject newCar = Instantiate(randomCarPrefab, startNode.transform.position, startNode.transform.rotation);

                    // 賦予大腦與目標
                    NPC_WaypointDrive driveScript = newCar.GetComponent<NPC_WaypointDrive>();
                    if (driveScript != null)
                    {
                        driveScript.targetNode = startNode.GetNextNode();
                    }

                    currentCount++; // 數量+1，換下一個出生點繼續生
                }
            }

            // 全部生完一輪後，休息 spawnInterval 秒，再重複動作
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}