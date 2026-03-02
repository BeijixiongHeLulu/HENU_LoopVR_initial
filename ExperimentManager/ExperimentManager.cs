using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PathCreation;
using UnityEngine;
using Valve.VR;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Utility;

[DisallowMultipleComponent]
public class ExperimentManager : MonoBehaviour
{
    #region Fields

    public static ExperimentManager Instance { get; private set; }

    [Space]
    [Header("Necessary Elements")]
    private GameObject _participantsCar;
    [Tooltip("0 to 10 seconds")][Range(0, 10)][SerializeField] private float startExperimentDelay = 3f;
    [Tooltip("0 to 10 seconds")][Range(0, 10)][SerializeField] private float respawnDelay = 5f;

    private enum Scene
    {
        MainMenu,
        Experiment
    }

    private List<ActivationTrigger> _activationTriggers;
    private CriticalEventController _criticalEventController;
    private Vector3 _respawnPosition;
    private Quaternion _respawnRotation;
    private Scene _scene;
    private bool _activatedEvent;
    private bool _vRScene;
    private bool _isStartPressed;

    // 【关键修改】状态锁：一旦为 true，彻底屏蔽 OnGUI
    private bool _isAborting = false;

    #endregion

    #region Private Methods

    private void Awake()
    {
        _activationTriggers = new List<ActivationTrigger>();

        //singleton pattern a la Unity
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        if (SavingManager.Instance != null)
        {
            SavingManager.Instance.SetParticipantCar(_participantsCar);
        }
    }

    public void OnSceneLoaded()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            AssignParticipantsCar();
            RunMainMenu();
        }
    }


    private void Start()
    {
        _vRScene = CalibrationManager.Instance.GetVRActivationState();

        if (_activationTriggers.Count == 0)
        {
            Debug.Log("<color=red>Error: </color>Please ensure that ActivationTrigger is being executed before ExperimentManager if there are triggers present in the scene.");
        }

        if (EyetrackingManager.Instance == null)
        {
            Debug.Log("<color=red>Error: </color>EyetrackingManager should be present in the scene.");
        }

        if (CalibrationManager.Instance == null)
        {
            Debug.Log("<color=red>Error: </color>CalibrationManager should be present in the scene.");
        }

        if (SavingManager.Instance == null)
        {
            Debug.Log("<color=red>Error: </color>SavingManager should be present in the scene.");
        }

        if (CameraManager.Instance == null)
        {
            Debug.Log("<color=red>Error: </color>CameraManager should be present in the scene.");
        }

        try
        {
            InformTriggers();
            AssignParticipantsCar();
            RunMainMenu();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e);
            throw;
        }
    }

    // 【新增】安全中止协程
    private IEnumerator AbortSequence()
    {
        _isAborting = true; // 1. 立即上锁，OnGUI 下一帧起将不再执行

        // 2. 降低负载：先关掉车辆物理和引擎
        if (_participantsCar != null)
        {
            var rb = _participantsCar.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            var ctrl = _participantsCar.GetComponent<CarController>();
            if (ctrl != null) ctrl.TurnOffEngine();
        }

        // 3. 保存数据
        if (SavingManager.Instance != null)
        {
            SavingManager.Instance.StopAndSaveData(SceneManager.GetActiveScene().name);
        }

        Debug.Log("Aborting... Waiting for data save (3s)...");

        // 4. 长等待：给 OpenXR 3秒钟时间来处理后台事务，防止 Device Lost
        yield return new WaitForSeconds(3.0f);

        // 5. 执行跳转
        if (CalibrationManager.Instance != null)
        {
            CalibrationManager.Instance.AbortExperiment();
        }

        // 注意：这里不需要把 _isAborting 设回 false，因为场景即将卸载/重载
    }

    private void RunMainMenu()
    {
        _scene = Scene.MainMenu;
        if (_participantsCar != null)
        {
            if (_participantsCar.GetComponent<Rigidbody>() != null)
                _participantsCar.GetComponent<Rigidbody>().isKinematic = true;

            if (_participantsCar.GetComponent<CarController>() != null)
                _participantsCar.GetComponent<CarController>().TurnOffEngine();
        }
    }

    // inform all triggers to disable their game objects at the beginning of the experiment
    private void InformTriggers()
    {
        foreach (var trigger in _activationTriggers)
        {
            trigger.DeactivateTheGameObjects();
        }
    }

    // starting the experiment
    private IEnumerator StartExperiment()
    {
        string condition = ConditionManager.Instance.GetExperimentalCondition();

        if (condition == "BaseCondition" || condition == "AudioOnly")
        {
            var hud = _participantsCar.GetComponentInChildren<HUD_Advance>();
            if (hud != null) hud.ShutDownAllVisualsPermanently();
        }

        TimeManager.Instance.SetExperimentStartTime();
        _isStartPressed = true;
        while (SceneLoadingHandler.Instance.GetAdditiveLoadingState()) yield return null;

        _scene = Scene.Experiment;

        SavingManager.Instance.StartRecordingData();
        CameraManager.Instance.FadeIn();
        yield return new WaitForSeconds(startExperimentDelay);
        _participantsCar.GetComponent<Rigidbody>().isKinematic = false;
        // ================== 【开始修改】请在此处插入以下代码 ==================
        // 【Plan A 修复】实验正式开始瞬间，立刻记录当前位置为“第一复活点”
        // 这样即使出门就撞车（还没碰到任何 Checkpoint），也会回到起跑线，而不是飞到 (0,0,0)
        _respawnPosition = _participantsCar.transform.position;
        _respawnRotation = _participantsCar.transform.rotation;
        // ================== 【修改结束】 ====================================

        _participantsCar.GetComponent<CarController>().TurnOnEngine();
    }

    private IEnumerator ReSpawnParticipant(float seconds)
    {
        _participantsCar.GetComponent<Rigidbody>().velocity = Vector3.zero;
        _participantsCar.GetComponent<Rigidbody>().isKinematic = true;
        yield return new WaitForSeconds(seconds);
        _participantsCar.GetComponent<Rigidbody>().isKinematic = false;

        // ConditionManager.Instance.EndEvent(false);

        CameraManager.Instance.AlphaFadeIn();
        _participantsCar.GetComponent<CarController>().TurnOnEngine();
    }

    private void AssignParticipantsCar()
    {
        switch (SceneManager.GetActiveScene().name)
        {
            case "SceneLoader":
                _participantsCar = SceneLoadingSceneManager.Instance.GetParticipantsCar();
                break;
            case "MountainRoad":
                _participantsCar = MountainRoadManager.Instance.GetParticipantsCar();
                break;
            case "Westbrueck":
                _participantsCar = WestbrueckManager.Instance.GetParticipantsCar();
                break;
            case "CountryRoad":
                _participantsCar = CountryRoadManager.Instance.GetParticipantsCar();
                break;
            case "Autobahn":
                _participantsCar = AutobahnManager.Instance.GetParticipantsCar();
                break;
        }

        PersistentTrafficEventManager.Instance.SetParticipantsCar(_participantsCar);
        // 【新增修复代码】：必须同时更新 SavingManager 的车！
        // 防止 SavingManager 操作已销毁的旧车导致崩溃
        if (SavingManager.Instance != null)
        {
            SavingManager.Instance.SetParticipantCar(_participantsCar);
        }
    }

    #endregion

    #region Public Methods

    public void ParticipantFailed()
    {
        _activatedEvent = false;

        CameraManager.Instance.AlphaFadeOut();

        ConditionManager.Instance.EndEvent(false); // todo check

        PersistentTrafficEventManager.Instance.FinalizeEvent();
        _participantsCar.GetComponent<CarController>().TurnOffEngine();
        _participantsCar.GetComponent<Rigidbody>().isKinematic = true;
        _participantsCar.GetComponent<Rigidbody>().velocity = Vector3.zero;
        _participantsCar.transform.SetPositionAndRotation(_respawnPosition, _respawnRotation);
        CameraManager.Instance.RespawnBehavior();
        _participantsCar.GetComponent<Rigidbody>().isKinematic = false;
        _participantsCar.GetComponent<AIController>().SetLocalTargetAndCurveDetection();
        StartCoroutine(ReSpawnParticipant(respawnDelay));
    }

    // ending the experiment
    public void EndOfExperiment()
    {
        CameraManager.Instance.FadeOut();

        _participantsCar.transform.parent.gameObject.SetActive(false);

        CalibrationManager.Instance.URIRequest();
        CalibrationManager.Instance.ExperimentEnded();
        SceneManager.LoadSceneAsync("MainMenu");
    }

    // Reception desk for ActivationTriggers to register themselves
    public void RegisterToExperimentManager(ActivationTrigger listener)
    {
        _activationTriggers.Add(listener);
    }

    #endregion

    #region Setters

    public void SetRespawnPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        _respawnPosition = position;
        _respawnRotation = rotation;
    }

    public void SetInitialTransform(Vector3 position, Quaternion rotation)
    {
        _participantsCar.transform.SetPositionAndRotation(position, rotation);
    }

    public void SetInitialTransform(Vector3 position)
    {
        _participantsCar.transform.SetPositionAndRotation(position, _participantsCar.transform.rotation);
    }

    public void SetCarPath(PathCreator newPath)
    {
        _participantsCar.GetComponent<AIController>().SetNewPath(newPath);
    }

    public void SetEventActivationState(bool activationState)
    {
        _activatedEvent = activationState;
    }

    public void SetParticipantsCar(GameObject car)
    {
        _participantsCar = car;
    }

    public void SetController(CriticalEventController criticalEventController)
    {
        _criticalEventController = criticalEventController;
    }

    #endregion

    #region Getters

    public bool GetEventActivationState()
    {
        return _activatedEvent;
    }

    public GameObject GetSeatPosition()
    {
        if (_participantsCar != null && _participantsCar.GetComponent<CarController>() != null)
            return _participantsCar.GetComponent<CarController>().GetSeatPosition();
        return null;
    }

    public GameObject GetParticipantsCar()
    {
        return _participantsCar;
    }

    #endregion

    #region GUI

    public void OnGUI()
    {
        // 【关键修复】如果正在中止程序，立刻停止绘制 GUI。
        // 这能防止 OpenXR 在场景卸载期间因渲染 GUI 而崩溃。
        if (_isAborting) return;

        float height = Screen.height;
        float width = Screen.width;

        float xForButtons = width / 12f;
        float yForButtons = height / 7f;

        float xForLable = (width / 12f);
        float yForLable = height / 1.35f;

        float buttonWidth = 200f;
        float buttonHeight = 30f;

        int labelFontSize = 33;


        // Lable
        GUI.color = Color.white;
        GUI.skin.label.fontSize = labelFontSize;
        GUI.skin.label.fontStyle = FontStyle.Bold;

        // Buttons
        GUI.backgroundColor = Color.cyan;
        GUI.color = Color.white;

        if (_scene == Scene.MainMenu)
        {
            if (!_isStartPressed)
            {
                GUI.Label(new Rect(xForLable, yForLable, 500, 100), "Main Experiment");

                if (GUI.Button(new Rect(xForButtons, yForButtons, buttonWidth, buttonHeight), "Start"))
                {
                    StartCoroutine(StartExperiment());
                }
            }

            if (_isStartPressed && _scene != Scene.Experiment)
            {
                GUI.Label(new Rect(width / 4f, height / 8f, 500, 100), "Main Experiment is Loading...");
            }

            // Reset Button
            GUI.backgroundColor = Color.red;
            GUI.color = Color.white;

            if (GUI.Button(new Rect(xForButtons * 9, yForButtons, buttonWidth, buttonHeight), "Abort"))
            {
                // 启动安全中止协程
                StartCoroutine(AbortSequence());
            }
        }
        else if (_scene == Scene.Experiment)
        {
            GUI.color = Color.white;

            if (_activatedEvent)
            {
                GUI.backgroundColor = Color.magenta;

                if (GUI.Button(new Rect(xForButtons, yForButtons, buttonWidth, buttonHeight), "Respawn Manually"))
                {
                    ParticipantFailed();
                }
            }
        }
    }

    #endregion
}