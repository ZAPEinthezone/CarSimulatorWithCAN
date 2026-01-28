using TMPro;
using UnityEngine;
using UnityEngine.UI; // 如果使用 Legacy Text
// using TMPro; // 如果使用 TextMeshPro，請取消註解此行

public class ICSimUIHandler : MonoBehaviour
{
    [Header("Reference to Data Source")]
    public ICSimReceiver receiver; // 拖入掛載了 ICSimReceiver 的物件

    [Header("UI Text Components")]
    public TMP_Text speedDisplayText;
    public TMP_Text signalDisplayText;
    public TMP_Text doorsDisplayText;

    // 如果你使用的是 TextMeshPro，請將上面的 Text 改為 TMP_Text

    void Update()
    {
        if (receiver == null) return;

        // 1. 更新時速 (保留一位小數)
        if (speedDisplayText != null)
        {
            speedDisplayText.text = $"Speed: {receiver.speedKmh:F1} km/h";
        }

        // 2. 更新方向燈狀態
        if (signalDisplayText != null)
        {
            signalDisplayText.text = $"Signal: {receiver.signalState}";

            // 進階：根據狀態改變文字顏色
            if (receiver.signalState == "Hazard") signalDisplayText.color = Color.red;
            else if (receiver.signalState != "Off") signalDisplayText.color = Color.yellow;
            else signalDisplayText.color = Color.white;
        }

        // 3. 更新車門狀態 (組合字串)
        if (doorsDisplayText != null)
        {
            string fl = receiver.flDoor ? "Locked" : "OPEN";
            string fr = receiver.frDoor ? "Locked" : "OPEN";
            string rl = receiver.rlDoor ? "Locked" : "OPEN";
            string rr = receiver.rrDoor ? "Locked" : "OPEN";

            doorsDisplayText.text = $"Doors:\nFL: {fl} | FR: {fr}\nRL: {rl} | RR: {rr}";
        }
    }
}