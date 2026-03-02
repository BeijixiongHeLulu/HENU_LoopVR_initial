using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO; // 新增这一行，用于文件写入
// 【修改 1】移除 Tobii 引用，防止编译错误
// using Tobii.XR;
// using ViveSR.anipal.Eye;
using LoopAr.Connector; // 引入你的 Connector 命名空间

public class EyetrackingDataRecorder : MonoBehaviour
{
    private float _sampleRate;
    private List<EyeTrackingDataFrame> _recordedEyeTrackingData;
    private List<float> _frameRates;
    private EyetrackingManager _eyetrackingManager;
    private Transform _hmdTransform;
    private bool recordingEnded;
    // --- 新增：实时文件流 ---
    private StreamWriter _writer;
    private string _csvPath;


    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        _recordedEyeTrackingData = new List<EyeTrackingDataFrame>();

        _eyetrackingManager = EyetrackingManager.Instance;

        if (_eyetrackingManager != null)
        {
            _sampleRate = _eyetrackingManager.GetSampleRate();
            _hmdTransform = _eyetrackingManager.GetHmdTransform();
        }
    }

    void Update() { }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_eyetrackingManager != null)
            _hmdTransform = _eyetrackingManager.GetHmdTransform();
    }

    public void StartRecording()
    {
        recordingEnded = false;

        // --- 新增：初始化实时 CSV 写入 ---
        string folderPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "WestdriveLoopARData", "EyeTracking_Realtime");
        if (!System.IO.Directory.Exists(folderPath)) System.IO.Directory.Exists(folderPath); // 修复笔误，应为 CreateDirectory
        if (!System.IO.Directory.Exists(folderPath)) System.IO.Directory.CreateDirectory(folderPath);

        _csvPath = System.IO.Path.Combine(folderPath, $"EyeTracking_Realtime_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        _writer = new StreamWriter(_csvPath, true);
        _writer.AutoFlush = true; // 开启实时刷写防崩溃！

        // 写入极其清晰的表头
        _writer.WriteLine("UnixTimeStamp,AbsoluteUnixTime,EventMarker,FPS,GazePosX,GazePosY,GazePosZ,GazeDirX,GazeDirY,GazeDirZ,LeftBlink,RightBlink");

        StartCoroutine(RecordEyeTrackingData());
    }

    public void StopRecording()
    {
        recordingEnded = true;

        // --- 新增：安全关闭文件流 ---
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Close();
            _writer = null;
            Debug.Log($"<color=green>[眼动记录] 实时 CSV 已安全保存至: {_csvPath}</color>");
        }
    }

    public void ClearEyeTrackingDataRecordings()
    {
        if (_recordedEyeTrackingData != null)
            _recordedEyeTrackingData.Clear();
    }

    private IEnumerator RecordEyeTrackingData()
    {
        int frameCounter = 0;
        Debug.Log("<color=green>Start recording (Pico Mode)...</color>");

        _frameRates = new List<float>();

        while (!recordingEnded)
        {
            EyeTrackingDataFrame dataFrame = new EyeTrackingDataFrame();

            // 【修改 2】核心替换：用 EyeTrackingConnector 替换 TobiiXR
            // 原代码：var eyeTrackingDataWorld = TobiiXR.GetEyeTrackingData(...)

            // 获取注视射线
            Ray gazeRay = EyeTrackingConnector.RequestCombinedGazeRay();

            // 获取睁眼/闭眼数据 (用于判断眨眼)
            float leftOpen, rightOpen;
            EyeTrackingConnector.ShowEyeOpenness(out leftOpen, out rightOpen);
            // 简单阈值判断眨眼 (如果开度小于 0.1 则认为闭眼)
            bool leftBlink = leftOpen < 0.1f;
            bool rightBlink = rightOpen < 0.1f;

            // 填充数据 (World Space)
            // 既然 Ray.origin 通常就是 EyePosition，我们直接用
            dataFrame.EyePosWorldCombined = gazeRay.origin;
            dataFrame.EyeDirWorldCombined = gazeRay.direction;
            dataFrame.LeftEyeIsBlinkingWorld = leftBlink;
            dataFrame.RightEyeIsBlinkingWorld = rightBlink;

            // 获取物体碰撞信息
            dataFrame.hitObjects = GetHitObjectsFromGaze(gazeRay.origin, gazeRay.direction);

            // Local Space 数据填充 (将 World 转回 Camera Local)
            if (Camera.main != null)
            {
                dataFrame.EyePosLocalCombined = Camera.main.transform.InverseTransformPoint(gazeRay.origin);
                dataFrame.EyeDirLocalCombined = Camera.main.transform.InverseTransformDirection(gazeRay.direction);
                dataFrame.LeftEyeIsBlinkingLocal = leftBlink;
                dataFrame.RightEyeIsBlinkingLocal = rightBlink;
            }

            // 通用数据填充
            dataFrame.TobiiTimeStamp = Time.time; // 用 Unity 时间代替 Tobii 时间戳
            dataFrame.UnixTimeStamp = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentUnixTimeStamp() : DateTime.Now.Ticks;

            if (SavingManager.Instance != null)
                dataFrame.FPS = SavingManager.Instance.GetCurrentFPS();

            if (EyetrackingManager.Instance != null && EyetrackingManager.Instance.GetHmdTransform() != null)
            {
                dataFrame.HmdPosition = EyetrackingManager.Instance.GetHmdTransform().position;
                dataFrame.NoseVector = EyetrackingManager.Instance.GetHmdTransform().forward;
            }

            // ==========================================================
            // --- [新增] 注入全局绝对时间和当前帧的脉冲 Marker ---
            // ==========================================================
            if (SyncManager.Instance != null)
            {
                dataFrame.AbsoluteUnixTime = SyncManager.Instance.GetAbsoluteUnixTime();
                dataFrame.EventMarker = SyncManager.Instance.CurrentFrameMarker;
            }

            _frameRates.Add(dataFrame.FPS);
            _recordedEyeTrackingData.Add(dataFrame);
            frameCounter++;

            if (_writer != null)
            {
                string line = $"{dataFrame.UnixTimeStamp},{dataFrame.AbsoluteUnixTime},{dataFrame.EventMarker},{dataFrame.FPS}," +
                              $"{dataFrame.EyePosWorldCombined.x:F4},{dataFrame.EyePosWorldCombined.y:F4},{dataFrame.EyePosWorldCombined.z:F4}," +
                              $"{dataFrame.EyeDirWorldCombined.x:F4},{dataFrame.EyeDirWorldCombined.y:F4},{dataFrame.EyeDirWorldCombined.z:F4}," +
                              $"{dataFrame.LeftEyeIsBlinkingWorld},{dataFrame.RightEyeIsBlinkingWorld}";
                _writer.WriteLine(line);
            }

            yield return new WaitForSeconds(_sampleRate);
        }
    }

    private List<HitObjectInfo> GetHitObjectsFromGaze(Vector3 gazeOrigin, Vector3 gazeDirection)
    {
        // 增加层级遮罩或忽略 Trigger，防止射线打到不该打的东西 (可选优化)
        RaycastHit[] hitColliders = Physics.RaycastAll(gazeOrigin, gazeDirection);

        List<HitObjectInfo> hitObjectInfoList = new List<HitObjectInfo>();

        foreach (var colliderhit in hitColliders)
        {
            HitObjectInfo hitInfo = new HitObjectInfo();
            hitInfo.ObjectName = colliderhit.collider.gameObject.name;
            hitInfo.HitObjectPosition = colliderhit.collider.transform.position;
            hitInfo.HitPointOnObject = colliderhit.point;
            hitObjectInfoList.Add(hitInfo);
        }

        return hitObjectInfoList;
    }

    public List<EyeTrackingDataFrame> GetDataFrames()
    {
        return _recordedEyeTrackingData;
    }

    public float GetAverageFrameRate()
    {
        if (_frameRates != null && _frameRates.Count > 0)
            return _frameRates.Average();
        return 0f;
    }
}