using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class ICSimReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int port = 50000;

    [Header("Car State (ReadOnly)")]
    public float speedKmh;
    public string signalState = "Off";
    public bool flDoor, frDoor, rlDoor, rrDoor;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;

    // 定義 CAN IDs (與 Python 腳本一致)
    const uint DEFAULT_SPEED_ID = 0x244;
    const uint DEFAULT_SIGNAL_ID = 0x188;
    const uint DEFAULT_DOOR_ID = 0x19B;
    const uint BMW_SPEED_ID = 0x1B4;

    void Start()
    {
        // 初始化並啟動後台執行緒
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"UDP Receiver started on port {port}");
    }

    private void ReceiveData()
    {
        udpClient = new UdpClient(port);
        IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, port);

        while (isRunning)
        {
            try
            {
                // 接收原始位元組 (Python 的 sock.recvfrom)
                byte[] receivedBytes = udpClient.Receive(ref remoteIpEndPoint);

                if (receivedBytes.Length >= 12)
                {
                    // 解析 CAN ID (前 4 bytes, Little Endian)
                    uint canId = BitConverter.ToUInt32(receivedBytes, 0);

                    // 解析 CAN Data (後 8 bytes)
                    byte[] canData = new byte[8];
                    Array.Copy(receivedBytes, 4, canData, 0, 8);

                    ParseCanFrame(canId, canData);
                }
            }
            catch (Exception e)
            {
                if (isRunning) Debug.LogError(e.ToString());
            }
        }
    }

    private void ParseCanFrame(uint canId, byte[] data)
    {
        // 注意：這裡直接更新變數。
        // 在複雜專案中，建議使用鎖 (lock) 或線程安全隊列

        // --- 速度訊號 ---
        if (canId == DEFAULT_SPEED_ID || canId == BMW_SPEED_ID)
        {
            // ICSim: speed 放在 byte3, byte4
            int raw = (data[3] << 8) | data[4];
            speedKmh = raw / 100.0f;
        }
        // --- 方向燈訊號 ---
        else if (canId == DEFAULT_SIGNAL_ID)
        {
            byte sig = data[0];
            bool left = (sig & 0x01) != 0;
            bool right = (sig & 0x02) != 0;

            if (left && right) signalState = "Hazard";
            else if (left) signalState = "Left";
            else if (right) signalState = "Right";
            else signalState = "Off";
        }
        // --- 車門鎖訊號 ---
        else if (canId == DEFAULT_DOOR_ID)
        {
            byte doorsByte = data[2];
            flDoor = (doorsByte & 0x01) != 0;
            frDoor = (doorsByte & 0x02) != 0;
            rlDoor = (doorsByte & 0x04) != 0;
            rrDoor = (doorsByte & 0x08) != 0;
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null) receiveThread.Abort();
    }
}