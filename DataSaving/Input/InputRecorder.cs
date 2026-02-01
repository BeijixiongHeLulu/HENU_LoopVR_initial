using System;
using System.Collections;
using System.Collections.Generic;
using System.IO; // 必须引入 IO
using UnityEngine;
using UnityEngine.UI;

public class InputRecorder : MonoBehaviour
{

    private float _sampleRate;
    private GameObject _participantCar;
    private bool _recordingEnded;

    private bool receivedInput;
    private float _steeringInput;
    private float _accelerationInput;
    private float _brakeInput;

    private List<InputDataFrame> InputDataFrames;

    // --- 新增：实时文件流 ---
    private StreamWriter _writer;
    private string _csvPath;

    void Start()
    {
        _sampleRate = SavingManager.Instance.GetSampleRate();
        InputDataFrames = new List<InputDataFrame>();
    }

    private void ReceiveInput(float steeringInput, float accelerationInput, float brakeInput)
    {
        _steeringInput = steeringInput;
        _accelerationInput = accelerationInput;
        _brakeInput = brakeInput;
    }

    private IEnumerator RecordInputData()
    {
        while (!_recordingEnded)
        {
            double timestamp = TimeManager.Instance.GetCurrentUnixTimeStamp();

            // --- 1. 原有逻辑 (保留以防 SavingManager 报错) ---
            InputDataFrame inputDataFrame = new InputDataFrame();
            inputDataFrame.TimeStamp = timestamp;

            if (Math.Abs(_steeringInput) > 0 || Math.Abs(_accelerationInput) > 0 || Math.Abs(_brakeInput) > 0)
            {
                inputDataFrame.ReceivedInput = true;
                inputDataFrame.SteeringInput = _steeringInput;
                inputDataFrame.AcellerationInput = _accelerationInput;
                inputDataFrame.BrakeInput = _brakeInput;
            }
            else
            {
                inputDataFrame.ReceivedInput = false;
                inputDataFrame.SteeringInput = 0f;
                inputDataFrame.AcellerationInput = 0f;
                inputDataFrame.BrakeInput = 0f;
            }
            InputDataFrames.Add(inputDataFrame);

            // --- 2. 新增：实时写入 CSV ---
            if (_writer != null)
            {
                // 格式: UnixTimestamp, Steering, Acceleration, Brake
                string line = $"{timestamp:F6},{_steeringInput:F4},{_accelerationInput:F4},{_brakeInput:F4}";
                _writer.WriteLine(line);
            }

            yield return new WaitForSeconds(_sampleRate);
        }
    }

    public void StartInputRecording()
    {
        Debug.Log("<color=green>Found input!</color>");
        if (_participantCar != null)
        {
            _participantCar.GetComponent<ManualController>().NotifyInputObservers += ReceiveInput;
        }

        Debug.Log("<color=green>Recording Input started!</color>");

        // --- 初始化 CSV 文件 ---
        SetupCSV();

        _recordingEnded = false;
        StartCoroutine(RecordInputData());
    }

    private void SetupCSV()
    {
        // 获取被试 ID (如果 CalibrationManager 里没有公开方法，这里可能需要调整)
        string participantID = "Unknown";
        if (CalibrationManager.Instance != null)
        {
            // 尝试获取 ID，这里假设 CalibrationData 是公开的或者通过 Getter 获取
            // participantID = CalibrationManager.Instance.GetCalibrationData().ParticipantUuid; 
            // 简单起见，我们也可以生成一个时间戳ID，或者你确保CalibrationManager有ID
            var data = CalibrationManager.Instance.GetCalibrationData();
            if (data != null && !string.IsNullOrEmpty(data.ParticipantUuid))
            {
                participantID = data.ParticipantUuid;
            }
        }

        string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "WestdriveLoopARData");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string fileName = $"{participantID}_Input_Realtime.csv";
        _csvPath = Path.Combine(folderPath, fileName);

        try
        {
            _writer = new StreamWriter(_csvPath, true); // 追加模式
            _writer.AutoFlush = true; // 关键：实时刷写到硬盘
            // 写表头
            _writer.WriteLine("UnixTimeStamp,Steering,Acceleration,Brake");
            Debug.Log($"[InputRecorder] 实时 CSV 已创建: {_csvPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[InputRecorder] 创建 CSV 失败: {e.Message}");
        }
    }

    public void StopRecording()
    {
        _recordingEnded = true;

        // 关闭文件流
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Close();
            _writer = null;
            Debug.Log($"[InputRecorder] 录制结束，文件已保存: {_csvPath}");
        }
    }

    public void SetParticipantCar(GameObject participantCar)
    {
        _participantCar = participantCar;
    }

    public List<InputDataFrame> GetDataFrames()
    {
        // 依然返回 List 以兼容 SavingManager
        if (_recordingEnded || InputDataFrames != null)
        {
            return InputDataFrames;
        }
        else
        {
            return new List<InputDataFrame>(); // 返回空列表防止报错
        }
    }
}