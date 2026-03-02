using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class SyncManager : MonoBehaviour
{
    public static SyncManager Instance { get; private set; }

    [Header("TCP Connection")]
    public string host = "127.0.0.1";
    public int port = 9001;

    // 暴露给 InputRecorder 读取的公共变量
    public int CurrentFrameMarker { get; private set; } = 0;
    private int _framesToKeepMarker = 0;

    private TcpClient _tcpClient;
    private NetworkStream _networkStream;
    private StreamWriter _csvWriter;
    private string _logFilePath;
    private Coroutine _markerResetCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeTCP();
            InitializeCSV();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeTCP()
    {
        try
        {
            _tcpClient = new TcpClient();
            // 设置 50 毫秒超时，防止 Unity 卡死
            _tcpClient.ConnectAsync(host, port).Wait(50);
            _networkStream = _tcpClient.GetStream();
            Debug.Log($"【SyncManager】成功连接到 Python MarkerServer {host}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"【SyncManager】无法连接到生理数据端，请确认 Python 脚本已启动！错误: {e.Message}");
        }
    }

    private void InitializeCSV()
    {
        string dir = Path.Combine(Application.dataPath, "../Data_Log");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        _logFilePath = Path.Combine(dir, $"Event_Condition_Log_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        _csvWriter = new StreamWriter(_logFilePath, true);
        _csvWriter.WriteLine("AbsoluteUnixTime,Marker,Speed_KMH,Distance_To_Event,Generated_Interval");
        _csvWriter.Flush();
    }

    /// <summary>
    /// 核心触发接口：向 Python 发送指令，并写入本地 CSV
    /// </summary>
    public void TriggerEvent(int markerValue, float speed = 0f, float distance = 0f, float interval = 0f)
    {
        CurrentFrameMarker = markerValue;

        long unixTime = GetAbsoluteUnixTime();

        // 写入独立的纯 Marker CSV
        if (_csvWriter != null)
        {
            _csvWriter.WriteLine($"{unixTime},{markerValue},{speed:F2},{distance:F2},{interval:F2}");
        }

        // 异步向 Python 发送单字节指令
        SendByteToPython((byte)markerValue);

        Debug.Log($"【Marker 触发】发送 {markerValue} | 时间戳: {unixTime}");

        // ==========================================
        // --- 绝对时间展宽：强制保持 0.3 秒，无视掉帧！ ---
        // ==========================================
        if (_markerResetCoroutine != null)
        {
            StopCoroutine(_markerResetCoroutine);
        }
        _markerResetCoroutine = StartCoroutine(ResetMarkerAfterTime(0.3f));
    }

    // --- 新增：使用绝对真实时间来倒数，彻底免疫卡顿 ---
    private System.Collections.IEnumerator ResetMarkerAfterTime(float timeInSeconds)
    {
        // WaitForSecondsRealtime 不受游戏卡顿和 Time.timeScale 的影响
        yield return new WaitForSecondsRealtime(timeInSeconds);
        CurrentFrameMarker = 0;
    }

    private async void SendByteToPython(byte markerByte)
    {
        if (_networkStream != null && _tcpClient.Connected)
        {
            try
            {
                byte[] data = new byte[] { markerByte };
                await _networkStream.WriteAsync(data, 0, 1);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"【SyncManager】Marker 发送失败: {e.Message}");
            }
        }
    }

    public long GetAbsoluteUnixTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }


    private void OnApplicationQuit()
    {
        // 发送结束信号 7 给 Python
        SendByteToPython(7);

        if (_networkStream != null) _networkStream.Close();
        if (_tcpClient != null) _tcpClient.Close();
        if (_csvWriter != null)
        {
            _csvWriter.Flush();
            _csvWriter.Close();
        }
    }
}