using UnityEngine;
using System.Collections.Generic;

public class IntersectionV2X : MonoBehaviour
{
    [Header("🚥 直向燈組 (1, 2, 5, 6) - 救護車通行的方向")]
    public List<GameObject> verticalRed; 
    public List<GameObject> verticalYellow; // 👈 新增黃燈
    public List<GameObject> verticalGreen;

    [Header("🚥 橫向燈組 (3, 4, 7, 8) - 要擋住的方向")]
    public List<GameObject> horizontalRed;
    public List<GameObject> horizontalYellow; // 👈 新增黃燈
    public List<GameObject> horizontalGreen;

    private float resetTimer = 0f;

    public void AmbulanceApproach()
    {
        // --- 🚑 直向：強制變綠 ---
        // 熄滅紅燈與黃燈，只點亮綠燈
        foreach(var r in verticalRed) if(r != null) r.SetActive(false);
        foreach(var y in verticalYellow) if(y != null) y.SetActive(false);
        foreach(var g in verticalGreen) if(g != null) g.SetActive(true);

        // --- 🛑 橫向：強制變紅 ---
        // 點亮紅燈，熄滅黃燈與綠燈
        foreach(var r in horizontalRed) if(r != null) r.SetActive(true);
        foreach(var y in horizontalYellow) if(y != null) y.SetActive(false);
        foreach(var g in horizontalGreen) if(g != null) g.SetActive(false);

        NotifyNearbyNPCs();
        resetTimer = 5.0f; // 5 秒沒感應到救護車就交還控制權
    }

    void Update() 
    {
        if (resetTimer > 0) 
        {
            resetTimer -= Time.deltaTime;
            if (resetTimer <= 0) 
            {
                ResetNPCs();
                // 燈號會由你原本的「紅綠燈控制套件」在它下一個 Update 週期自動刷回正確顏色
            }
        }
    }

    void NotifyNearbyNPCs()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 40f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Car"))
            {
                var npc = hit.GetComponent<NPC_WaypointDrive>();
                if (npc == null) continue;

                // 判斷是否為同向車道 (1, 2, 5, 6 方向)
                float dot = Vector3.Dot(npc.transform.forward, transform.up);
                if (Mathf.Abs(dot) > 0.7f) 
                {
                    if (dot > 0) npc.V2X_Accelerate(); // 1 號向前衝
                    else npc.V2X_ForceStop();        // 6 號對向停下
                }
            }
        }
    }

    void ResetNPCs() 
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 45f);
        foreach (var h in hits) 
        {
            var npc = h.GetComponent<NPC_WaypointDrive>();
            if (npc != null) npc.V2X_Reset();
        }
    }
}