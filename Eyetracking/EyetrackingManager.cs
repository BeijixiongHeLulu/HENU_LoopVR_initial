using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
// 【修改 1】彻底移除 Tobii 和 Vive 的引用
// using Tobii.XR; 
// using ViveSR.anipal.Eye;

public class EyetrackingManager : MonoBehaviour
{
    public static EyetrackingManager Instance { get; private set; }

    public int SetSampleRate = 90;
    private Transform _hmdTransform;
    private List<EyeTrackingDataFrame> _eyeTrackingDataFrames;
    private EyeValidationData _eyeValidationData;
    private EyetrackingValidation _eyetrackingValidation;
    private bool _eyeValidationSucessful;
    private EyetrackingDataRecorder _eyeTrackingRecorder;
    private float _sampleRate;

    private bool _calibrationSuccess;

    private float eyeValidationDelay;

    private Vector3 _eyeValidationErrorAngles;

    public delegate void OnCompletedEyeValidation(bool wasSuccessful);
    public event OnCompletedEyeValidation NotifyEyeValidationCompletnessObservers;

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        _sampleRate = 1f / SetSampleRate;

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Camera.main != null)
        {
            _hmdTransform = Camera.main.transform;
        }
    }

    void Start()
    {
        _eyeTrackingRecorder = GetComponent<EyetrackingDataRecorder>();
        _eyetrackingValidation = GetComponentInChildren<EyetrackingValidation>();

        if (_eyetrackingValidation != null)
        {
            _eyetrackingValidation.NotifyEyeValidationObservers += SetEyeValidationStatus;
        }
    }

    public void StartValidation()
    {
        Debug.Log("validating...");
        if (_eyetrackingValidation != null)
            _eyetrackingValidation.StartValidation(eyeValidationDelay);
    }

    public void AbortValidation()
    {
        if (_eyetrackingValidation != null)
            _eyetrackingValidation.AbortValidation();
        NotifyEyeValidationCompletnessObservers?.Invoke(false);
    }

    public void StartValidation(float delay)
    {
        if (_eyetrackingValidation != null)
            _eyetrackingValidation.StartValidation(delay);
    }

    public void StartCalibration()
    {
        // 【修改 2】使用“虚拟校准成功”逻辑，绕过 Tobii/Vive
        Debug.Log("<color=green>Mock Calibration (Pico/VRCFT) successful :)</color>");

        if (CalibrationManager.Instance != null)
        {
            CalibrationManager.Instance.EyeCalibrationSuccessful();
        }
    }

    public void StartRecording()
    {
        Debug.Log("<color=green>Recording eye-tracking Data!</color>");
        if (_eyeTrackingRecorder != null)
            _eyeTrackingRecorder.StartRecording();
    }

    public void StopRecording()
    {
        if (_eyeTrackingRecorder != null)
            _eyeTrackingRecorder.StopRecording();
        StoreEyeTrackingData();
    }

    public Transform GetHmdTransform()
    {
        return _hmdTransform;
    }

    public float GetSampleRate()
    {
        return _sampleRate;
    }

    public void StoreEyeValidationData(EyeValidationData data)
    {
        _eyeValidationData = data;
    }

    public Vector3 GetEyeValidationErrorAngles()
    {
        return _eyeValidationErrorAngles;
    }

    private void StoreEyeTrackingData()
    {
        if (_eyeTrackingRecorder != null)
            _eyeTrackingDataFrames = _eyeTrackingRecorder.GetDataFrames();
    }

    public List<EyeTrackingDataFrame> GetEyeTrackingData()
    {
        if (_eyeTrackingDataFrames != null)
        {
            return _eyeTrackingDataFrames;
        }
        else
        {
            // 如果没有数据，返回空列表而不是报错，更安全
            return new List<EyeTrackingDataFrame>();
        }
    }

    private void SetEyeValidationStatus(bool eyeValidationWasSucessfull, Vector3 errorAngles)
    {
        Debug.Log("eyeValidation Status was called in EyeTrackingManager with " + eyeValidationWasSucessfull);
        _eyeValidationSucessful = eyeValidationWasSucessfull;
        _eyeValidationErrorAngles = errorAngles;
        NotifyEyeValidationCompletnessObservers?.Invoke(eyeValidationWasSucessfull);
    }

    public float GetAverageSceneFPS()
    {
        if (_eyeTrackingRecorder != null)
            return _eyeTrackingRecorder.GetAverageFrameRate();
        return 0f;
    }

    public bool GetEyeValidationStatus()
    {
        return _eyeValidationSucessful;
    }

    public double getCurrentTimestamp()
    {
        System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        return (System.DateTime.UtcNow - epochStart).TotalSeconds;
    }
}