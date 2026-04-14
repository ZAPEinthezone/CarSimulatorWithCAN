using UnityEngine;
using System.Collections.Generic;

public class TrafficNode : MonoBehaviour
{
    public List<TrafficNode> nextNodes;

    [Header("🚦 紅綠燈系統設定")]
    public bool isStopLine = false;   
    public bool currentIsRed = false; // 給 AI 車子看的煞車訊號

    [Header("🔌 連接紅綠燈套件 (實體燈號用)")]
    public GameObject redLightModel;    // 拖入紅燈模型
    public GameObject yellowLightModel; // 拖入黃燈模型
    public GameObject greenLightModel;  // 拖入綠燈模型

    [Header("📡 實體硬體傳輸設定")]
    public string hardwareID = "Jianguo_Zhongxiao_1"; 
    
    // 記憶體：用來偵測有沒有變燈
    private string previousColorCode = ""; 

    void Update()
    {
        if (isStopLine)
        {
            // 1. 維持 AI 車子的煞車系統
            if (redLightModel != null)
            {
                currentIsRed = redLightModel.activeInHierarchy;
            }

            // 2. 判斷現在套件亮什麼燈
            string currentColorCode = "";
            if (redLightModel != null && redLightModel.activeInHierarchy) currentColorCode = "R";
            else if (yellowLightModel != null && yellowLightModel.activeInHierarchy) currentColorCode = "Y";
            else if (greenLightModel != null && greenLightModel.activeInHierarchy) currentColorCode = "G";

            // 3. 如果燈號有改變，就呼叫總機傳送封包
            if (currentColorCode != previousColorCode && currentColorCode != "")
            {
                if (HardwareSender.Instance != null)
                {
                    HardwareSender.Instance.SendLightPacket(hardwareID, currentColorCode);
                }
                previousColorCode = currentColorCode; 
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (nextNodes == null) return;
        Gizmos.color = Color.yellow;
        foreach (TrafficNode node in nextNodes)
        {
            if (node != null)
            {
                Gizmos.DrawLine(transform.position, node.transform.position);
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
    }

    public TrafficNode GetNextNode()
    {
        if (nextNodes == null || nextNodes.Count == 0) return null; 
        if (nextNodes.Count == 1) return nextNodes[0];

        int randomIndex = Random.Range(0, nextNodes.Count);
        return nextNodes[randomIndex];
    }
}