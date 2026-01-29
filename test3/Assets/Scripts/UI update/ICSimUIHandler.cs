using UnityEngine;
using TMPro; // 務必確保使用的是 TextMeshPro

public class ICSimUIHandler : MonoBehaviour
{
    [Header("資料來源")]
    [Tooltip("請將 Hierarchy 中的 car 5 拖入此欄位")]
    public CarSpeedDetector speedSource;

    [Header("UI 元件")]
    [Tooltip("請將 StatusPanel 下的 SpeedText 拖入此欄位")]
    public TMP_Text speedDisplayText;

    void Update()
    {
        // 檢查來源與 UI 是否都已連接
        if (speedSource != null && speedDisplayText != null)
        {
            // 直接讀取 CarSpeedDetector 腳本中的速度數值
            float speed = speedSource.currentSpeedKmh;

            // 更新 UI 文字，:F1 代表保留一位小數
            speedDisplayText.text = $"Speed: {speed:F1} km/h";
        }
    }
}