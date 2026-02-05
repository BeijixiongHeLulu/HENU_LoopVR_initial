using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Random = System.Random;

[DisallowMultipleComponent]
public class MainMenu : MonoBehaviour
{
    #region Fields

    public static MainMenu Instance { get; private set; }

    [SerializeField] private GameObject welcome;
    [SerializeField] private GameObject loading;
    [SerializeField] private GameObject thankYou;
    [SerializeField] private Canvas canvas;

    private bool _eyeCalibrationSelected;
    private bool _eyeValidationSelected;
    private bool _seatCalibrationSelected;
    bool _conditionSelected;

    // 【新增】用于输入被试编号和Session的变量
    private string _inputParticipantID = "P01";
    private string _inputSession = "1";

    private enum Section
    {
        ChooseVRState,
        ChooseSteeringInput,
        IDGeneration,
        EyeCalibration,
        EyeValidation,
        SeatCalibration,
        TrainingBlock,
        MainExperiment
    }

    private Section _section;

    #endregion

    #region PrivateMethods

    private void Awake()
    {
        // 【新增】强行查找并销毁可能存在的 Tobii 初始化器，防止它导致崩溃
        var tobiiObj = GameObject.Find("[TobiiXR Initializer]");
        if (tobiiObj != null)
        {
            DestroyImmediate(tobiiObj);
            Debug.LogWarning("Found and destroyed TobiiXR Initializer to prevent crash.");
        }

            if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        if (loading != null) loading.gameObject.SetActive(false);
        if (thankYou != null) thankYou.gameObject.SetActive(false);
        _section = Section.ChooseVRState;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (CalibrationManager.Instance.GetWasMainMenuLoaded())
        {
            _eyeCalibrationSelected = _eyeValidationSelected = true;

            if (_section == Section.TrainingBlock || _section == Section.MainExperiment) _seatCalibrationSelected = true;

            if (welcome != null)
            {
                Destroy(welcome);
            }
        }
    }

    #endregion

    #region PublicMethods

    public void ReStartMainMenu()
    {
        _section = Section.ChooseVRState;
    }

    public Canvas GetCanvas()
    {
        return canvas;
    }

    public void SetMenuSection(string section)
    {
        _section = (Section)Enum.Parse(typeof(Section), section, true);
    }

    #endregion

    #region GUI

    public void OnGUI()
    {
        #region LocalVariables

        float height = Screen.height;
        float width = Screen.width;

        float xB = width / 12f;
        float yB = height / 7f;

        float w = 200f;
        float h = 30f;

        #endregion

        // Quit
        GUI.backgroundColor = Color.red;
        GUI.color = Color.white;

        if (GUI.Button(new Rect(xB * 9, yB, w, h), "Quit"))
        {
            Application.Quit();
        }

        if (_eyeCalibrationSelected && !CalibrationManager.Instance.GetEndOfExperimentState())
        {
            if (welcome != null) Destroy(welcome);

            if (loading != null) loading.gameObject.SetActive(true);
            if (thankYou != null) thankYou.gameObject.SetActive(false);
        }
        else if (CalibrationManager.Instance.GetEndOfExperimentState())
        {
            if (welcome != null) Destroy(welcome);
            if (loading != null) Destroy(loading);

            if (thankYou != null) thankYou.gameObject.SetActive(true);
        }

        #region Table

        if (CalibrationManager.Instance.GetCameraModeSelectionState())
        {
            GUI.color = Color.green;
            GUI.skin.box.fontStyle = FontStyle.Bold;
            GUI.Box(new Rect(xB * 9, yB * 4.7f, w, h - 8), new GUIContent("Camera Mode"));
        }
        else
        {
            GUI.color = Color.yellow;
            GUI.skin.box.fontStyle = FontStyle.Italic;
            GUI.Box(new Rect(xB * 9, yB * 4.7f, w, h - 8), new GUIContent("Camera Mode"));
        }

        if (CalibrationManager.Instance.GetSteeringInputSelectedState())
        {
            GUI.color = Color.green;
            GUI.skin.box.fontStyle = FontStyle.Bold;
            GUI.Box(new Rect(xB * 9, yB * 5f, w, h - 8), new GUIContent("Control Input"));
        }
        else
        {
            GUI.color = Color.yellow;
            GUI.skin.box.fontStyle = FontStyle.Italic;
            GUI.Box(new Rect(xB * 9, yB * 5f, w, h - 8), new GUIContent("Control Input"));
        }

        if (CalibrationManager.Instance.GetParticipantUUIDState())
        {
            GUI.color = Color.green;
            GUI.skin.box.fontStyle = FontStyle.Bold;
            GUI.Box(new Rect(xB * 9, yB * 5.3f, w, h - 8), new GUIContent("Participant ID"));
        }
        else
        {
            GUI.color = Color.yellow;
            GUI.skin.box.fontStyle = FontStyle.Italic;
            GUI.Box(new Rect(xB * 9, yB * 5.3f, w, h - 8), new GUIContent("Participant ID"));
        }

        if (_conditionSelected)
        {
            GUI.color = Color.white;
            GUI.skin.box.fontStyle = FontStyle.Bold;
            GUI.Box(new Rect(xB * 5, yB * 5f, w, h - 8), new GUIContent("Condition: " + CalibrationManager.Instance.GetExperimentalCondition()));
        }

        if (CalibrationManager.Instance.GetVRActivationState())
        {
            if (!_eyeCalibrationSelected)
            {
                GUI.color = Color.yellow;
                GUI.skin.box.fontStyle = FontStyle.Italic;
                GUI.Box(new Rect(xB * 9, yB * 5.6f, w, h - 8), new GUIContent("Eye-tracker Calibration"));
            }
            else if (CalibrationManager.Instance.GetEyeTrackerCalibrationState())
            {
                GUI.color = Color.green;
                GUI.skin.box.fontStyle = FontStyle.Bold;
                GUI.Box(new Rect(xB * 9, yB * 5.6f, w, h - 8), new GUIContent("Eye-tracker Calibration"));
            }
            else
            {
                GUI.color = Color.red;
                GUI.skin.box.fontStyle = FontStyle.BoldAndItalic;
                GUI.Box(new Rect(xB * 9, yB * 5.6f, w, h - 8), new GUIContent("Eye-tracker Calibration"));
            }

            if (!_eyeValidationSelected)
            {
                GUI.color = Color.yellow;
                GUI.skin.box.fontStyle = FontStyle.Italic;
                GUI.Box(new Rect(xB * 9, yB * 5.9f, w, h - 8), new GUIContent("Eye-tracker Validation"));
            }
            else if (CalibrationManager.Instance.GetEyeTrackerValidationState())
            {
                GUI.color = Color.green;
                GUI.skin.box.fontStyle = FontStyle.Bold;
                GUI.Box(new Rect(xB * 9, yB * 5.9f, w, h - 8), new GUIContent("Eye-tracker Validation"));
            }
            else
            {
                GUI.color = Color.red;
                GUI.skin.box.fontStyle = FontStyle.BoldAndItalic;
                GUI.Box(new Rect(xB * 9, yB * 5.9f, w, h - 8), new GUIContent("Eye-tracker Validation"));
            }

            if (!_seatCalibrationSelected)
            {
                GUI.color = Color.yellow;
                GUI.skin.box.fontStyle = FontStyle.Italic;
                GUI.Box(new Rect(xB * 9, yB * 6.2f, w, h - 8), new GUIContent("Seat Calibration"));
            }
            else if (CalibrationManager.Instance.GetSeatCalibrationState())
            {
                GUI.color = Color.green;
                GUI.skin.box.fontStyle = FontStyle.Bold;
                GUI.Box(new Rect(xB * 9, yB * 6.2f, w, h - 8), new GUIContent("Seat Calibration"));
            }
            else
            {
                GUI.color = Color.red;
                GUI.skin.box.fontStyle = FontStyle.BoldAndItalic;
                GUI.Box(new Rect(xB * 9, yB * 6.2f, w, h - 8), new GUIContent("Seat Calibration"));
            }
        }

        #endregion

        #region States

        GUI.color = Color.white;

        // Choosing VR or non-VR mode
        if (_section == Section.ChooseVRState)
        {
            GUI.backgroundColor = Color.green;

            if (GUI.Button(new Rect(xB, yB, w, h), "VR Mode"))
            {
                CalibrationManager.Instance.StoreVRState(true);
                CalibrationManager.Instance.SetCameraMode(true);
                _section = Section.ChooseSteeringInput;
            }

            GUI.backgroundColor = Color.cyan;

            if (GUI.Button(new Rect(xB, yB * 2, w, h), "Non-VR Mode"))
            {
                CalibrationManager.Instance.StoreVRState(false);
                CalibrationManager.Instance.SetCameraMode(false);
                _section = Section.ChooseSteeringInput;
            }
        }

        // Choosing control input
        if (_section == Section.ChooseSteeringInput)
        {
            GUI.backgroundColor = Color.green;

            if (GUI.Button(new Rect(xB, yB, w, h), "Steering Wheel"))
            {
                CalibrationManager.Instance.StoreSteeringInputDevice("SteeringWheel");
                _section = Section.IDGeneration;
            }

            GUI.backgroundColor = Color.cyan;

            if (GUI.Button(new Rect(xB, yB * 1.5f, w, h), "Xbox One Controller"))
            {
                CalibrationManager.Instance.StoreSteeringInputDevice("XboxOneController");
                _section = Section.IDGeneration;
            }

            GUI.backgroundColor = Color.blue;

            if (GUI.Button(new Rect(xB, yB * 2f, w, h), "Keyboard"))
            {
                CalibrationManager.Instance.StoreSteeringInputDevice("Keyboard");
                _section = Section.IDGeneration;
            }
        }

        // 【修改部分】 Participant ID and Condition Generation
        if (_section == Section.IDGeneration)
        {
            // 绘制一个背景框
            GUI.color = Color.white;
            GUI.Box(new Rect(xB, yB, w, h * 5.5f), "Participant Info");

            // 1. ID 输入
            GUI.Label(new Rect(xB + 10, yB + 25, w - 20, h), "Participant ID:");
            _inputParticipantID = GUI.TextField(new Rect(xB + 10, yB + 45, w - 20, h), _inputParticipantID);

            // 2. Session 输入
            GUI.Label(new Rect(xB + 10, yB + 80, w - 20, h), "Session:");
            _inputSession = GUI.TextField(new Rect(xB + 10, yB + 100, w - 20, h), _inputSession);

            GUI.backgroundColor = Color.green;

            // 3. 确认按钮
            if (GUI.Button(new Rect(xB, yB + 4.5f * h, w, h * 2), "Confirm & Generate"))
            {
                _section = Section.IDGeneration;

                // 第一步：先运行原有的逻辑来生成Condition（这可能会生成一个随机ID）
                CalibrationManager.Instance.GenerateIDAndCondition();

                // 第二步：使用我们的手动ID覆盖掉随机生成的ID
                // 组合格式：ID_Session (例如: P01_1)
                string finalID = _inputParticipantID + "_" + _inputSession;
                CalibrationManager.Instance.OverwriteParticipantID(finalID);

                // 流程跳转
                _section = CalibrationManager.Instance.GetVRActivationState() ? Section.EyeCalibration : Section.TrainingBlock;
                _conditionSelected = true;
            }
        }

        // Eye calibration
        if (_section == Section.EyeCalibration)
        {
            GUI.backgroundColor = Color.green;
            if (GUI.Button(new Rect(xB, yB, w, h), "Eye Calibration"))
            {
                _eyeCalibrationSelected = true;
                CalibrationManager.Instance.EyeCalibration();

                if (CalibrationManager.Instance.GetEyeTrackerCalibrationState())
                {
                    _section = Section.EyeValidation;
                }
            }

            GUI.backgroundColor = Color.yellow;
            if (GUI.Button(new Rect(xB, yB * 2, w, h), "Skip Eye Calibration"))
            {
                CalibrationManager.Instance.AddSpecialNote("EyeCalibrationSkipped");
                _eyeCalibrationSelected = true;
                _section = Section.EyeValidation;
            }
        }

        // Eye validation
        if (_section == Section.EyeValidation)
        {
            ApplicationManager.Instance.StoreMainMenuLastState("SeatCalibration");

            GUI.backgroundColor = Color.green;
            if (GUI.Button(new Rect(xB, yB, w, h), "Eye Validation"))
            {
                _eyeValidationSelected = true;
                CalibrationManager.Instance.EyeValidation();
            }

            GUI.backgroundColor = Color.yellow;
            if (GUI.Button(new Rect(xB, yB * 2, w, h), "Skip Eye Validation"))
            {
                CalibrationManager.Instance.AddSpecialNote("EyeValidationSkipped");
                _eyeValidationSelected = true;
                _section = Section.SeatCalibration;
            }
        }

        // Seat calibration
        if (_section == Section.SeatCalibration)
        {
            ApplicationManager.Instance.StoreMainMenuLastState("TrainingBlock");

            GUI.backgroundColor = Color.green;
            if (GUI.Button(new Rect(xB, yB, w, h), "Seat Calibration"))
            {
                _seatCalibrationSelected = true;
                CalibrationManager.Instance.SeatCalibration();
            }

            GUI.backgroundColor = Color.yellow;
            if (GUI.Button(new Rect(xB, yB * 2, w, h), "Skip Seat Calibration"))
            {
                CalibrationManager.Instance.AddSpecialNote("SeatCalibrationSkipped");
                _seatCalibrationSelected = true;
                _section = Section.TrainingBlock;
            }
        }

        // Training scene
        if (_section == Section.TrainingBlock)
        {
            ApplicationManager.Instance.StoreMainMenuLastState("MainExperiment");

            GUI.backgroundColor = Color.green;
            if (GUI.Button(new Rect(xB, yB, w, h), "Training Block"))
            {
                CalibrationManager.Instance.StartTestDrive();
            }

            GUI.backgroundColor = Color.yellow;
            if (GUI.Button(new Rect(xB, yB * 2, w, h), "Skip Training Block"))
            {
                if (welcome != null)
                    Destroy(welcome);

                if (loading != null)
                    Destroy(loading);

                CalibrationManager.Instance.AddSpecialNote("TrainingSceneSkipped");
                _section = Section.MainExperiment;
                SceneLoadingHandler.Instance.LoadExperimentScenes();
            }
        }

        #endregion
    }

    #endregion
}