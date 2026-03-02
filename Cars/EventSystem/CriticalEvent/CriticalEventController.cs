using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[DisallowMultipleComponent]
public class CriticalEventController : MonoBehaviour
{
    #region Fields

    [Space]
    [Header("Consistent Event Objects")]
    [SerializeField] private TrafficEventTrigger startTrigger;
    [SerializeField] private TrafficEventTrigger endTrigger;
    [SerializeField] private GameObject consistentEventObjects;

    [Space]
    [Header("Two-Stage Takeover Settings (MR -> ToR)")]
    [Tooltip("预期的自动驾驶车速（km/h），用于自动计算MR的触发距离")]
    public float expectedCarSpeedKmh = 100f;

    [Tooltip("MR与ToR之间的时间间隔（自变量：3, 5, 7, 9秒）")]
    public float mrToTorInterval = 5f;

    [Tooltip("挂载播放 MR_voice.mp3 的 AudioSource")]
    public AudioSource mrAudioSource;

    // --- 新增：专门用于播放 ToR 接管警报的音频源 ---
    [Tooltip("挂载播放 ToR 接管提示音的 AudioSource")]
    public AudioSource torAudioSource;

    // 用于记录双阶段的时间戳与状态
    private double _recordedMRTimeStamp;
    private double _torTimeStamp;
    private bool _mrTriggered = false;


    [Space]
    [Header("Event Objects")]
    [Tooltip("The gameObject which is the parent of the event object")]
    [SerializeField] private GameObject eventObjectParent;

    [SerializeField] private List<GameObject> eventObjects;

    [Tooltip("Should the event objects be active or not when experiment begins")]
    [SerializeField]
    private GameObject respawnPoint;

    [Space]
    [Header("Event Setting")]
    [Tooltip("Time the car needs from informing the driver to giving them the control. (0 - 15 seconds)")]
    [Range(0, 15)]
    [SerializeField] public float startEventDelay = 2.5f;

    [Tooltip("Time the car needs from informing the driver to taking back the control. (0 - 10 seconds)")]
    [Range(0, 10)]
    [SerializeField] private float endEventDelay = 1f;

    [Tooltip("End the event automatically after given (0 - 120) seconds in case the participant stays idle.")]
    [Range(0, 120)]
    [SerializeField] private float eventIdleDuration = 10f;
    [SerializeField] private bool eventObjectActive;


    private RestrictedZoneTrigger[] _restrictedZoneTriggers;
    private RestrictedZoneTrigger[] _restrictedZoneTriggersInEventObjects;

    private bool _activatedEvent;
    private bool _endIdleEventState;
    private MeshRenderer[] _meshRenderers;

    // Event Data variables
    private string _eventName;

    #endregion

    #region Private methods

    private void Start()
    {
        startTrigger.SetController(this);
        endTrigger.SetController(this);
        ExperimentManager.Instance.SetController(this);

        // --- 新增：初始化时随机抽取一个预警时间 ---
        float[] intervals = { 3f, 5f, 7f, 9f };
        mrToTorInterval = intervals[UnityEngine.Random.Range(0, intervals.Length)];
        // ----------------------------------------

        _restrictedZoneTriggers = consistentEventObjects.GetComponentsInChildren<RestrictedZoneTrigger>();
        _restrictedZoneTriggersInEventObjects = eventObjectParent.GetComponentsInChildren<RestrictedZoneTrigger>();

        DeactivateRestrictedZones();
        EventObjectsActivationSwitch(eventObjectParent);
        TurnOffMeshRenderers(consistentEventObjects);

        StartCoroutine(CheckMRDistanceRoutine());
    }

    // --- 【新增：基于车速和时间自动检测距离触发MR的逻辑】 ---
    private IEnumerator CheckMRDistanceRoutine()
    {
        // 1. 持续检测直到成功获取到玩家的车辆对象
        GameObject car = null;
        while (car == null)
        {
            if (PersistentTrafficEventManager.Instance != null)
            {
                car = PersistentTrafficEventManager.Instance.GetParticipantsCar();
            }
            yield return new WaitForSeconds(0.5f);
        }

        // 2. 计算触发MR的阈值距离：速度(m/s) * 时间(s)
        float speedMs = expectedCarSpeedKmh / 3.6f;
        float targetDistance = speedMs * mrToTorInterval;

        // 3. 每帧检测车辆与ToR触发器（startTrigger）的直线距离
        while (!_mrTriggered)
        {
            float currentDistance = Vector3.Distance(car.transform.position, startTrigger.transform.position);

            // 如果车辆驶入了这个距离圈内，则触发MR
            if (currentDistance <= targetDistance)
            {
                TriggerMR();
            }

            yield return null;
        }
    }

    private void TriggerMR()
    {
        if (_mrTriggered) return;
        _mrTriggered = true;

        // 记录MR爆发时间
        _recordedMRTimeStamp = TimeManager.Instance.GetCurrentUnixTimeStamp();

        // 播放音频
        if (mrAudioSource != null)
        {
            mrAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("【CriticalEvent】未绑定 MR AudioSource！");
        }

        Debug.Log($"【CriticalEvent】距离障碍物 {expectedCarSpeedKmh / 3.6f * mrToTorInterval} 米处自动触发 MR。");
    }


    private IEnumerator EndIdleEvent()
    {
        int seconds = 0;

        while (!_endIdleEventState)
        {
            yield return new WaitForSeconds(1);

            seconds++;

            if (seconds >= eventIdleDuration)
            {
                _activatedEvent = ExperimentManager.Instance.GetEventActivationState();
                if (_activatedEvent)
                    ExperimentManager.Instance.ParticipantFailed();

                _endIdleEventState = true;
            }
        }
    }

    private IEnumerator ActivateTheEvent()
    {
        _endIdleEventState = false;

        // ================= 正式 ToR 阶段 =================
        // 当车碰到原有的 startTrigger 时，记录此处为 ToR 爆发时间
        _torTimeStamp = TimeManager.Instance.GetCurrentUnixTimeStamp();

        // 触发原本的接管警报
        ConditionManager.Instance.DriverAlert();

        // --- 新增：独立播放 ToR 音频 ---
        if (torAudioSource != null)
        {
            torAudioSource.Play();
        }
        else
        {
            Debug.LogWarning($"【CriticalEvent】{gameObject.name} 未绑定 ToR AudioSource！");
        }

        eventObjectParent.SetActive(true);

        foreach (var trigger in _restrictedZoneTriggersInEventObjects)
        {
            trigger.SetController(this);
        }

        ConditionManager.Instance.StartEvent(eventObjects);

        yield return new WaitForSeconds(startEventDelay);
        // 此处交出控制权
        ActivateRestrictedZones();

        foreach (var trigger in _restrictedZoneTriggers)
        {
            trigger.SetController(this);
        }

        PersistentTrafficEventManager.Instance.InitiateEvent(eventObjects);
        ExperimentManager.Instance.SetRespawnPositionAndRotation(respawnPoint.transform.position,
            respawnPoint.transform.rotation);
        PersistentTrafficEventManager.Instance.GetParticipantsCar().GetComponent<AIController>().SetLocalTargetForEvents(respawnPoint.transform.position);

        StartCoroutine(EndIdleEvent());
    }

    private IEnumerator DeactivateTheEvent()
    {
        StopEndIdleEvent();

        EventObjectsActivationSwitch(eventObjectParent);

        ConditionManager.Instance.EndEvent(true);

        PersistentTrafficEventManager.Instance.GetParticipantsCar().GetComponent<AIController>().SetLocalTargetAndCurveDetection();

        yield return new WaitForSeconds(endEventDelay);
        DeactivateRestrictedZones();
        PersistentTrafficEventManager.Instance.FinalizeEvent();
        if (!eventObjectActive)
            eventObjectParent.SetActive(false);
    }

    private void ActivateRestrictedZones()
    {
        foreach (var restrictedZoneTrigger in _restrictedZoneTriggers)
        {
            restrictedZoneTrigger.gameObject.SetActive(true);
            restrictedZoneTrigger.gameObject.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    private void DeactivateRestrictedZones()
    {
        foreach (var restrictedZoneTrigger in _restrictedZoneTriggers)
        {
            restrictedZoneTrigger.gameObject.SetActive(false);
        }
    }

    private void EventObjectsActivationSwitch(GameObject parent)
    {
        if (eventObjectActive)
            parent.SetActive(true);
        else
            parent.SetActive(false);
    }

    #endregion

    #region Public methods

    // --- 新增：为下一轮循环重置整个事件状态 ---
    public void ResetEventForNextLoop()
    {
        // 1. 恢复未触发状态
        _mrTriggered = false;
        _activatedEvent = false;
        _endIdleEventState = false;

        // 2. 重新随机分配本轮的 MR-ToR 时间
        float[] intervals = { 3f, 5f, 7f, 9f };
        mrToTorInterval = intervals[UnityEngine.Random.Range(0, intervals.Length)];

        // 3. 重置物理触发器的缓存（调用第一步写的方法）
        if (startTrigger != null) startTrigger.ResetTrigger();
        if (endTrigger != null) endTrigger.ResetTrigger();

        // 4. 重启距离检测雷达（因为上一轮跑完后该协程已经结束了）
        StopCoroutine("CheckMRDistanceRoutine");
        StartCoroutine("CheckMRDistanceRoutine");

        Debug.Log($"【循环重置】事件重置完毕！本轮随机分配的MR提前量为: {mrToTorInterval}秒");
    }

    public void Triggered(bool state)
    {
        _activatedEvent = state;

        if (_activatedEvent)
        {
            StartCoroutine(ActivateTheEvent());
        }
        else
        {
            StartCoroutine(DeactivateTheEvent());
        }
    }

    public void ResetEventObjectsActivationStates()
    {
        EventObjectsActivationSwitch(eventObjectParent);
    }

    public void TurnOffMeshRenderers(GameObject trigger)
    {
        _meshRenderers = trigger.GetComponentsInChildren<MeshRenderer>();

        foreach (var meshRenderer in _meshRenderers)
        {
            meshRenderer.enabled = false;
        }
    }

    public void StopEndIdleEvent()
    {
        _endIdleEventState = true;
    }

    public void SetEventActivationState(bool state)
    {
        _activatedEvent = state;
    }

    public void SetEventStartData(double startTime, string eventName)
    {
        _eventName = eventName;
        // 原本记录StartTime的逻辑移交给了 _torTimeStamp，这里仅作为事件名称传递即可
    }

    public void SetEventEndData(double endTime, bool successState, string hitObjName = null)
    {
        // 调用上一轮我们在 SceneDataRecorder 里修改过的新方法，同时压入 MR 和 ToR 数据
        SceneDataRecorder.Instance.AssignEventData(
            _eventName,
            _recordedMRTimeStamp,
            mrToTorInterval,
            _torTimeStamp,
            endTime,
            successState,
            hitObjName
        );
    }

    #endregion
}