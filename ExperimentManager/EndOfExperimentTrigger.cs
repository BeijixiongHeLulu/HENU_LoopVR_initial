using System.Collections;
using UnityEngine;

public class EndOfExperimentTrigger : MonoBehaviour
{
    [Header("Loop Settings")]
    public int totalLoops = 3; // 总圈数
    public int currentLoop = 0; // 当前圈数

    [Header("Debug Info (自动获取)")]
    // 改为 SerializeField，这样你可以在 Inspector 里看到它是否为 (0,0,0)
    [SerializeField] private Vector3 startPos;
    private Quaternion startRot;
    private bool isInitialized = false;

    [Header("Original References")]
    [SerializeField] private TestEventManager testEventManager;
    [SerializeField] private float secondsTillManualControl;
    [SerializeField] private GameObject zoneLimiter;

    private void Start()
    {
        // 改为协程，等待一帧，防止车还没初始化完我们就去读坐标
        StartCoroutine(InitializeStartPoint());
    }

    private IEnumerator InitializeStartPoint()
    {
        yield return new WaitForEndOfFrame(); // 等待场景加载完毕

        GameObject car = null;

        // 1. 尝试通过 Tag 查找
        car = GameObject.FindGameObjectWithTag("Player");

        // 2. 如果没找到，尝试通过 AutobahnManager 查找 (这是最稳的)
        if (car == null && AutobahnManager.Instance != null)
        {
            car = AutobahnManager.Instance.GetParticipantsCar();
        }

        // 3. 记录坐标
        if (car != null)
        {
            startPos = car.transform.position;
            startRot = car.transform.rotation;
            isInitialized = true;
            Debug.Log($"<color=green>[Loop] 起点已锁定: {startPos}</color>");
        }
        else
        {
            Debug.LogError("<color=red>[Loop] 严重错误：找不到玩家车辆！StartPos 仍为 (0,0,0)。请检查车的 Tag 是否为 'Player'，或者 AutobahnManager 是否挂载了车辆。</color>");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 只有车撞上来才算
        if (other.GetComponent<CarController>() != null || other.CompareTag("Player"))
        {
            currentLoop++;
            Debug.Log($"[Loop] 完成第 {currentLoop} / {totalLoops} 圈。");

            if (currentLoop < totalLoops)
            {
                // --- 还没跑完，执行传送 ---
                if (isInitialized)
                {
                    StartCoroutine(ResetCar(other.gameObject));
                }
                else
                {
                    Debug.LogError("[Loop] 无法传送：起点未初始化！车可能没有被正确识别。");
                }
            }
            else
            {
                // --- 跑完了，执行原有的结束逻辑 ---
                StartCoroutine(OriginalTriggered(other));
            }
        }
    }

    private IEnumerator ResetCar(GameObject car)
    {
        // 1. 暂时冻结物理，防止传送后因惯性乱飞
        Rigidbody rb = car.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 2. 传送回记录的起点
        car.transform.position = startPos;
        car.transform.rotation = startRot;

        // 3. 重置 AI 路径 (如果是自动驾驶，这一步至关重要，否则它会试图开回终点)
        var ai = car.GetComponent<AIController>();
        if (ai) ai.SetLocalTargetAndCurveDetection();

        yield return null; // 等一帧，让物理引擎更新位置

        // 4. 恢复物理
        if (rb) rb.isKinematic = false;

        Debug.Log($"[Loop] 车辆已传送回起点: {startPos}");
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