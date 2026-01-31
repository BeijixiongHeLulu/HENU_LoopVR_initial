using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.IO;
using System;

public class VrcftRawDebug : MonoBehaviour
{
    [Header("设置")]
    public int listenPort = 9000;

    // 内部变量
    private UdpClient _udpClient;
    private Thread _receiveThread;
    private bool _isRunning = false;
    private bool _hasNewData = false;
    private byte[] _latestData;
    private int _packetCount = 0;
    private string _logFilePath;

    private void Start()
    {
        // 设置日志路径：项目根目录/VRCFT_Raw_Log.txt
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        _logFilePath = Path.Combine(projectRoot, "VRCFT_Raw_Log.txt");

        // 清空旧日志
        try { File.WriteAllText(_logFilePath, "VRCFT Raw Data Log\n=================\n"); } catch { }

        StartListening();
    }

    private void OnDestroy()
    {
        StopListening();
    }

    private void Update()
    {
        // 在主线程打印，防止报错
        if (_hasNewData && _latestData != null)
        {
            _hasNewData = false;
            _packetCount++;

            // 只打印前 5 个包，防止刷屏卡死
            if (_packetCount <= 5)
            {
                string hex = BitConverter.ToString(_latestData); // 转成 FF-AC-01 这种格式
                string ascii = ToSafeAscii(_latestData);         // 转成文本，不可见字符显示为点

                string log = $"\n[数据包 #{_packetCount} | 长度: {_latestData.Length}字节]\n";
                log += $"HEX:   {hex}\n";
                log += $"ASCII: {ascii}\n";

                Debug.Log($"<color=yellow>{log}</color>");

                // 写入文件
                try { File.AppendAllText(_logFilePath, log + "\n"); } catch { }
            }
            else if (_packetCount == 6)
            {
                Debug.Log("<color=red>已捕获5个样本，停止打印，请将 Log 发给开发者。</color>");
            }
        }
    }

    private void StartListening()
    {
        try
        {
            _udpClient = new UdpClient(listenPort);
            _isRunning = true;
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
            Debug.Log($"<color=green>开始监听端口 {listenPort} (RAW 模式)</color>");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"端口占用或错误: {e.Message}");
        }
    }

    private void StopListening()
    {
        _isRunning = false;
        if (_udpClient != null) { _udpClient.Close(); _udpClient = null; }
        if (_receiveThread != null && _receiveThread.IsAlive) { _receiveThread.Abort(); }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, listenPort);
        while (_isRunning)
        {
            try
            {
                if (_udpClient != null && _udpClient.Available > 0)
                {
                    byte[] data = _udpClient.Receive(ref remoteEP);

                    // 只要没处理完上一帧，就不读新的，保证数据完整性
                    if (!_hasNewData)
                    {
                        _latestData = data;
                        _hasNewData = true;
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch { }
        }
    }

    // 将字节转为可显示的 ASCII 字符，不可见字符转为 '.'
    private string ToSafeAscii(byte[] data)
    {
        char[] chars = new char[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            if (b >= 32 && b <= 126) // 可打印字符范围
                chars[i] = (char)b;
            else
                chars[i] = '.';
        }
        return new string(chars);
    }
}