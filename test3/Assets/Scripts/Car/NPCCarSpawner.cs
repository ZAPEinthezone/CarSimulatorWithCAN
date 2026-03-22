using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPC_CarSpawner : MonoBehaviour
{
    [Header("生成設定")]
    // 🌟 這裡升級成陣列了！可以塞無限多種車
    public GameObject[] carPrefabs;

    public float spawnInterval = 1.5f;
    public int maxCars = 50;

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
            GameObject[] currentCars = GameObject.FindGameObjectsWithTag("Car");

            // 確保有設定車款、有出生點，且還沒達上限
            if (carPrefabs.Length > 0 && startNodes.Count > 0 && currentCars.Length < maxCars)
            {
                TrafficNode startNode = startNodes[Random.Range(0, startNodes.Count)];

                // 🎲 核心魔法：從你給的車款清單裡，隨機抽一台出來！
                GameObject randomCarPrefab = carPrefabs[Random.Range(0, carPrefabs.Length)];

                GameObject newCar = Instantiate(randomCarPrefab, startNode.transform.position, startNode.transform.rotation);

                NPC_WaypointDrive driveScript = newCar.GetComponent<NPC_WaypointDrive>();
                if (driveScript != null)
                {
                    driveScript.targetNode = startNode.GetNextNode();
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }
}