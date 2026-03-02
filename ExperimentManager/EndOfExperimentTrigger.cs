using System.Collections;
using UnityEngine;

public class EndOfExperimentTrigger : MonoBehaviour
{
    [Header("Loop Settings")]
    public int totalLoops = 4; // 根据你的要求，这里默认改成了 4 轮
    public int currentLoop = 0;

    [Header("Speed Reset")]
    [Tooltip("传送回起点时的初始车速限速 (km/h)")]
    public float defaultSpeedKmh = 100f; // 你可以在面板里修改为你想要的初始速度

    [Header("Debug Info (自动获取)")]
    [SerializeField] private Vector3 startPos;
    private Quaternion startRot;
    private bool isInitialized = false;

    [Header("Original References")]
    [SerializeField] private TestEventManager testEventManager;
    [SerializeField] private float secondsTillManualControl;
    [SerializeField] private GameObject zoneLimiter;

    private void Start()
    {
        StartCoroutine(InitializeStartPoint());
    }

    private IEnumerator InitializeStartPoint()
    {
        yield return new WaitForEndOfFrame();

        GameObject car = null;
        car = GameObject.FindGameObjectWithTag("Player");

        if (car == null && AutobahnManager.Instance != null)
        {
            car = AutobahnManager.Instance.GetParticipantsCar();
        }

        if (car != null)
        {
            startPos = car.transform.position;
            startRot = car.transform.rotation;
            isInitialized = true;
            Debug.Log($"<color=green>[Loop] 起点已锁定: {startPos}</color>");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CarController>() != null || other.CompareTag("Player"))
        {
            currentLoop++;
            Debug.Log($"[Loop] 完成第 {currentLoop} / {totalLoops} 圈。");

            if (currentLoop < totalLoops)
            {
                if (isInitialized)
                {
                    StartCoroutine(ResetCar(other.gameObject));
                }
            }
            else
            {
                StartCoroutine(OriginalTriggered(other));
            }
        }
    }

    private IEnumerator ResetCar(GameObject car)
    {
        Rigidbody rb = car.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 1. 传送
        car.transform.position = startPos;
        car.transform.rotation = startRot;

        // 2. 重置 AI 路径
        var ai = car.GetComponent<AIController>();
        if (ai) ai.SetLocalTargetAndCurveDetection();

        // ================= 【核心新增逻辑】 =================

        // 3. 重置车辆初始限速
        AimedSpeed aimedSpeed = car.GetComponent<AimedSpeed>();
        if (aimedSpeed != null)
        {
            // SetRuleSpeed 的参数是 m/s，所以要除以 3.6
            aimedSpeed.SetRuleSpeed(defaultSpeedKmh / 3.6f);
            Debug.Log($"[Loop] 车辆限速已重置为 {defaultSpeedKmh} km/h");
        }

        // 4. 全局唤醒所有事件并重新随机时间
        CriticalEventController[] allEvents = FindObjectsOfType<CriticalEventController>();
        foreach (var evt in allEvents)
        {
            evt.ResetEventForNextLoop();
        }

        // ==================================================

        yield return null;

        if (rb) rb.isKinematic = false;
        Debug.Log($"[Loop] 车辆已传送回起点: {startPos}，所有事件及限速已重置！");
    }

    private IEnumerator OriginalTriggered(Collider other)
    {
        if (testEventManager != null)
        {
            StartCoroutine(testEventManager.ActivateHUD());
            yield return new WaitForSecondsRealtime(secondsTillManualControl);

            GetComponent<BoxCollider>().enabled = false;
            if (zoneLimiter) zoneLimiter.SetActive(true);

            testEventManager.EndTrigger(other);
        }
    }
}