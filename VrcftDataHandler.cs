using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System;
using System.Text;
using System.Diagnostics; // 用于高精度计时
using Debug = UnityEngine.Debug;

public class VrcftDataHandler : MonoBehaviour
{
    public static VrcftDataHandler Instance { get; private set; }

    [Header("核心设置")]
    public int listenPort = 9000;
    public string fileNamePrefix = "VRCFT_ExpSession";

    [Header("调试开关")]
    [Tooltip("是否在游戏屏幕左上角显示诊断信息")]
    public bool showOnScreenDebug = true;

    // --- 实时数据 (供 EyeTrackingConnector 调用) ---
    public float LeftEyeX { get; private set; }
    public float LeftEyeY { get; private set; }
    public float RightEyeX { get; private set; }
    public float RightEyeY { get; private set; }
    public float LeftOpenness { get; private set; } = 1.0f;
    public float RightOpenness { get; private set; } = 1.0f;

    // --- 内部变量 ---
    private UdpClient _udpClient;
    private Thread _receiveThread;
    private bool _isRunning = false;
    private StreamWriter _writer;
    private object _lock = new object();

    // --- 新增：跨线程安全的同步变量 ---
    private volatile int _threadSafeMarker = 0;
    private long _threadSafeUnixTime = 0;

    // 线程安全计时器
    private Stopwatch _stopwatch = new Stopwatch();

    // 诊断变量
    private int _packetCount = 0;
    private string _debugStatus = "等待启动...";
    private string _lastHex = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _stopwatch.Start(); // 启动计时
        SetupFile();
        StartListening();
    }

    // --- 新增 Update 方法：只有主线程能安全地找 SyncManager 拿数据 ---
    private void Update()
    {
        if (SyncManager.Instance != null)
        {
            _threadSafeMarker = SyncManager.Instance.CurrentFrameMarker;
        }
        // 使用纯 C# 获取绝对时间，精度极高
        _threadSafeUnixTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void OnDestroy()
    {
        StopListening();
        CloseFile();
    }

    // --- GUI 可视化逻辑 ---
    private void OnGUI()
    {
        // 如果开关没开，直接返回，不画任何东西
        if (!showOnScreenDebug) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 25;
        style.normal.textColor = Color.yellow;
        style.fontStyle = FontStyle.Bold;

        // 背景框
        GUI.Box(new Rect(10, 10, 800, 250), "");

        GUI.Label(new Rect(20, 20, 780, 40), $"VRCFT 监控面板 (端口 {listenPort})", style);

        style.normal.textColor = _packetCount > 0 ? Color.green : Color.red;
        GUI.Label(new Rect(20, 60, 780, 30), $"接收包数: {_packetCount}", style);

        style.normal.textColor = Color.white;
        style.fontSize = 20;
        GUI.Label(new Rect(20, 100, 780, 30), $"眼动: L({LeftEyeX:F2}, {LeftEyeY:F2}) R({RightEyeX:F2}, {RightEyeY:F2})", style);
        GUI.Label(new Rect(20, 130, 780, 30), $"开合: L:{LeftOpenness:F2} / R:{RightOpenness:F2}", style);

        style.fontSize = 16;
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(20, 170, 780, 30), $"状态: {_debugStatus}", style);
        // 如果数据太长，截断显示
        string hexShow = _lastHex.Length > 50 ? _lastHex.Substring(0, 50) + "..." : _lastHex;
        GUI.Label(new Rect(20, 200, 780, 30), $"HEX: {hexShow}", style);
    }

    private void SetupFile()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string saveFolder = Path.Combine(projectRoot, "EyeTracking_Data");
        if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);

        string timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fullPath = Path.Combine(saveFolder, $"{fileNamePrefix}_{timeStamp}.csv");

        try
        {
            var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(fs, Encoding.UTF8);
            _writer.AutoFlush = true; // 保持开启，防丢失
            // --- [修改] 表头加入 Marker ---
            _writer.WriteLine("Time,LeftEyeX,LeftEyeY,LeftOpen,RightEyeX,RightEyeY,RightOpen,AbsoluteUnixTime,EventMarker");
            _debugStatus = "CSV 记录中...";
        }
        catch (Exception e)
        {
            _debugStatus = $"文件错误: {e.Message}";
        }
    }

    private void StartListening()
    {
        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // 保持 IPv4 强制绑定，这是成功的关键
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), listenPort));

            _isRunning = true;
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
        }
        catch (Exception e)
        {
            _debugStatus = $"监听失败: {e.Message}";
        }
    }

    private void StopListening()
    {
        _isRunning = false;
        if (_udpClient != null) { _udpClient.Close(); _udpClient = null; }
        if (_receiveThread != null && _receiveThread.IsAlive) { try { _receiveThread.Abort(); } catch { } }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (_isRunning)
        {
            try
            {
                if (_udpClient != null && _udpClient.Available > 0)
                {
                    byte[] data = _udpClient.Receive(ref remoteEP);
                    _packetCount++;

                    // 仅在开启调试时更新 HEX 字符串，节省性能
                    if (showOnScreenDebug && _packetCount % 10 == 0)
                        _lastHex = BitConverter.ToString(data).Replace("-", " ");

                    ParseAndWrite(data);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (Exception e)
            {
                _debugStatus = $"接收错误: {e.Message}";
            }
        }
    }

    private void ParseAndWrite(byte[] data)
    {
        string ascii = Encoding.ASCII.GetString(data);
        bool updated = false;

        // 1. PitchYaw 解析
        int tagIndex = ascii.IndexOf("/tracking/eye/LeftRightPitchYaw");
        if (tagIndex != -1)
        {
            int typeIndex = ascii.IndexOf(",ffff", tagIndex);
            if (typeIndex != -1)
            {
                int dataStart = typeIndex + 8;
                if (dataStart + 16 <= data.Length)
                {
                    float v1 = ReadFloat(data, dataStart);
                    float v2 = ReadFloat(data, dataStart + 4);
                    float v3 = ReadFloat(data, dataStart + 8);
                    float v4 = ReadFloat(data, dataStart + 12);

                    LeftEyeY = v1 / 45.0f;
                    LeftEyeX = v2 / 45.0f;
                    RightEyeY = v3 / 45.0f;
                    RightEyeX = v4 / 45.0f;
                    updated = true;
                }
            }
        }

        // 2. ClosedAmount 解析
        int closeIndex = ascii.IndexOf("EyesClosedAmount");
        if (closeIndex != -1)
        {
            int typeIndex = ascii.IndexOf(",f", closeIndex);
            if (typeIndex != -1)
            {
                int dataStart = typeIndex + 4;
                if (dataStart + 4 <= data.Length)
                {
                    float v = ReadFloat(data, dataStart);
                    LeftOpenness = 1.0f - v;
                    RightOpenness = 1.0f - v;
                    updated = true;
                }
            }
        }

        if (updated)
        {
            // 使用线程安全的 Stopwatch 获取时间
            double currentTime = _stopwatch.Elapsed.TotalSeconds;

            // --- [新增] 把跨线程安全的时间和 Marker 拼接到这行数据的最后 ---
            string line = $"{currentTime:F4},{LeftEyeX:F4},{LeftEyeY:F4},{LeftOpenness:F3},{RightEyeX:F4},{RightEyeY:F4},{RightOpenness:F3},{_threadSafeUnixTime},{_threadSafeMarker}";

            lock (_lock)
            {
                if (_writer != null) _writer.WriteLine(line);
            }
        }
    }

    private float ReadFloat(byte[] data, int index)
    {
        byte[] bytes = { data[index + 3], data[index + 2], data[index + 1], data[index] };
        return BitConverter.ToSingle(bytes, 0);
    }

    private void CloseFile()
    {
        lock (_lock)
        {
            if (_writer != null) { _writer.Flush(); _writer.Close(); _writer = null; }
        }
    }
}