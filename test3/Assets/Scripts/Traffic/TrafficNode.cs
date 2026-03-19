using UnityEngine;
using System.Collections.Generic;

public class TrafficNode : MonoBehaviour
{
    [Header("下一站的節點 (可放多個)")]
    public List<TrafficNode> nextNodes;

    // 🌟 這個魔法會在 Scene 視窗畫出黃色的連線，讓你看清楚路網！
    private void OnDrawGizmos()
    {
        if (nextNodes == null) return;
        Gizmos.color = Color.yellow;
        foreach (TrafficNode node in nextNodes)
        {
            if (node != null)
            {
                // 畫一條線連接到下一個點
                Gizmos.DrawLine(transform.position, node.transform.position);
                // 在自己身上畫一個小圓圈代表節點
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
    }

    // 讓車子呼叫這個函數來「問路」
    public TrafficNode GetNextNode()
    {
        if (nextNodes == null || nextNodes.Count == 0) return null; // 沒路了(死胡同)

        // 如果只有一條路，就走那一條
        if (nextNodes.Count == 1) return nextNodes[0];

        // 🎲 如果是十字路口 (有多條路)，就隨機選一條！
        int randomIndex = Random.Range(0, nextNodes.Count);
        return nextNodes[randomIndex];
    }
}