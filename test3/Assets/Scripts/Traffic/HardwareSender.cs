using UnityEngine;
using System.IO.Ports;

public class HardwareSender : MonoBehaviour
{
    public static HardwareSender Instance;

    [Header("連線設定")]
    public string portName = "COM5"; 
    public int baudRate = 9600;

    private SerialPort serialPort;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.Open();
            Debug.Log($"✅ 成功連接實體硬體: {portName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 硬體連線失敗: {e.Message}");
        }
    }

    // 📦 升級版：直接接收顏色代碼 (R, Y, G)
    public void SendLightPacket(string nodeID, string colorCode)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            string packet = $"{nodeID}:{colorCode}\n"; 
            serialPort.Write(packet);
            Debug.Log($"📤 傳出封包: {packet}");
        }
    }

    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log("🛑 已安全關閉實體硬體連線");
        }
    }
}