using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public class SceneLoadingHandler : MonoBehaviour
{
    public static SceneLoadingHandler Instance { get; private set; }

    private GameObject _participantsCar;
    private GameObject _seatPosition;
    private bool _isLoadAdditiveModeRunning;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AssignParticipantsCarAndSeatPosition();

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetObjectToFollow(_participantsCar);
            CameraManager.Instance.SetSeatPosition(_seatPosition);
            SavingManager.Instance.SetParticipantCar(_participantsCar);
        }
    }

    private void Start()
    {
        AssignParticipantsCarAndSeatPosition();

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetObjectToFollow(_participantsCar);
            CameraManager.Instance.SetSeatPosition(_seatPosition);
        }
    }

    public void LoadExperimentScenes()
    {
        AssignParticipantsCarAndSeatPosition();

        CameraManager.Instance.FadeOut();
        StartCoroutine(LoadExperimentScenesAsyncAdditive());
    }

    public void SceneChange(string targetScene)
    {
        CameraManager.Instance.FadeOut();
        StartCoroutine(LoadScenesAsync(targetScene));
    }

    IEnumerator LoadScenesAsync(string targetScene)
    {
        yield return new WaitForSeconds(2);
        Debug.Log("Loading...");

        AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene);

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / .9f);

            if (progress >= .9f)
            {
                CameraManager.Instance.FadeOut();
            }

            yield return null;
        }

        AssignParticipantsCarAndSeatPosition();
        CameraManager.Instance.OnSceneLoaded(true);
    }

    public IEnumerator LoadExperimentScenesAsyncAdditive()
    {
        _isLoadAdditiveModeRunning = true;
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        // 【修改 1】首先加载 Autobahn 作为主场景（Active Scene）
        AsyncOperation opAutobahn = SceneManager.LoadSceneAsync("Autobahn");

        while (!opAutobahn.isDone)
        {
            yield return null;
        }

        // 加载其他场景（保持 Westbrueck 和 CountryRoad 不变）
        AsyncOperation op2 = SceneManager.LoadSceneAsync("Westbrueck", LoadSceneMode.Additive);

        while (!op2.isDone)
        {
            yield return null;
        }

        AsyncOperation op3 = SceneManager.LoadSceneAsync("CountryRoad", LoadSceneMode.Additive);

        while (!op3.isDone)
        {
            yield return null;
        }

        // 【修改 2】最后加载 MountainRoad 作为附加场景（原先它是主场景）
        AsyncOperation op4 = SceneManager.LoadSceneAsync("MountainRoad", LoadSceneMode.Additive);

        while (!op4.isDone)
        {
            yield return null;
        }

        // 【修改 3】确保获取的是 Autobahn 场景里的车和座位
        // 之前这里写死的是 MountainRoadManager
        _participantsCar = AutobahnManager.Instance.GetParticipantsCar();
        _seatPosition = AutobahnManager.Instance.GetSeatPosition();

        _isLoadAdditiveModeRunning = false;
        CameraManager.Instance.OnSceneLoaded(false);
    }

    private void AssignParticipantsCarAndSeatPosition()
    {
        switch (SceneManager.GetActiveScene().name)
        {
            case "SceneLoader":
                _participantsCar = SceneLoadingSceneManager.Instance.GetParticipantsCar();
                break;
            case "SeatCalibrationScene":
                _participantsCar = SeatCalibrationManager.Instance.GetParticipantsCar();
                _seatPosition = SeatCalibrationManager.Instance.GetSeatPosition();
                break;
            case "TrainingScene":
                _participantsCar = TrainingHandler.Instance.testEventManager.GetParticipantCar();
                _seatPosition = TrainingHandler.Instance.GetSeatPosition();
                break;
            case "MountainRoad":
                _participantsCar = MountainRoadManager.Instance.GetParticipantsCar();
                _seatPosition = MountainRoadManager.Instance.GetSeatPosition();
                break;
            case "Westbrueck":
                _participantsCar = WestbrueckManager.Instance.GetParticipantsCar();
                _seatPosition = WestbrueckManager.Instance.GetSeatPosition();
                break;
            case "CountryRoad":
                _participantsCar = CountryRoadManager.Instance.GetParticipantsCar();
                _seatPosition = CountryRoadManager.Instance.GetSeatPosition();
                break;
            case "Autobahn":
                _participantsCar = AutobahnManager.Instance.GetParticipantsCar();
                _seatPosition = AutobahnManager.Instance.GetSeatPosition();
                break;
        }
    }

    public GameObject GetParticipantsCar()
    {
        AssignParticipantsCarAndSeatPosition();
        return _participantsCar;
    }

    public GameObject GetSeatPosition()
    {
        AssignParticipantsCarAndSeatPosition();
        return _seatPosition;
    }

    public bool GetAdditiveLoadingState()
    {
        return _isLoadAdditiveModeRunning;
    }
}