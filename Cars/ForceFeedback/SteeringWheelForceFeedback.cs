using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteeringWheelForceFeedback : MonoBehaviour
{
    [SerializeField] private CarController _carController;
    [SerializeField] private ManualController _manualController;
    [SerializeField] private ControlSwitch _controlSwitch;

    public bool shouldInit = true;
    private float target;
    private float _smoothedTarget = 0f;
    private bool _isInitialized = false;

    void Start()
    {
        _carController = GetComponent<CarController>();
        _manualController = GetComponent<ManualController>();
        _controlSwitch = GetComponent<ControlSwitch>();

        if (shouldInit)
        {
            _isInitialized = LogitechGSDK.LogiSteeringInitialize(false);

            // 增加更严格的底层连接与力反馈马达检测
            bool isConnected = LogitechGSDK.LogiIsConnected(0);
            bool hasFFB = LogitechGSDK.LogiHasForceFeedback(0);

            Debug.Log($"【官方FFB】初始化:{_isInitialized} | 设备已连接:{isConnected} | 支持力反馈:{hasFFB}");
        }
    }

    private void Update()
    {
        if (!_isInitialized) return;

        LogitechGSDK.LogiUpdate();

        if (_carController == null || _manualController == null || _controlSwitch == null) return;

        // 自动驾驶状态下，获取游戏车辆的目标转向角 (范围 -1 到 1)
        target = _carController.GetSterring();

        // 处于自动驾驶状态时
        // 处于自动驾驶状态时
        if (!_controlSwitch.GetManualDrivingState())
        {
            // 1. 软件层平滑（低通滤波）：不要让方向盘瞬间跳到 target，而是以一定的惯性“滑”过去
            // 5f 是平滑系数。数值越小方向盘动作越迟缓平滑；数值越大越暴躁。推荐范围 2f ~ 8f
            _smoothedTarget = Mathf.Lerp(_smoothedTarget, target, Time.deltaTime * 5f);

            int offsetPercentage = (int)(-_smoothedTarget * 100f);

            // 2. 硬件层柔化：降低最大拉力（Saturation）和刚度（Coefficient）
            // 参数 40 (拉力) 和 30 (刚度) 模拟了类似真车车道保持辅助(LKA)那种“有韧性但不死板”的皮筋手感
            // 如果觉得还是抖，把 30 继续往下降；如果觉得太松垮，可以加到 50
            LogitechGSDK.LogiPlaySpringForce(0, offsetPercentage, 40, 30);
        }
        else
        {
            // ==========================================
            // 手动接管阶段：还原真实的车辆物理手感
            // ==========================================

            // 1. 模拟轮胎摩擦的“阻尼感”（数值 0-100）
            // 数值越大方向盘越重、越黏手。推荐范围：30 到 60
            LogitechGSDK.LogiPlayDamperForce(0, 40);

            // 2. 模拟真车松手后的“自然回正力”（数值 0-100）
            // 参数说明：设备0，目标中心点0，最大拉力20，弹性刚度20
            // 这会让方向盘有一个极其轻柔的力把你往中心点拉，而不是轻飘飘的乱转
            LogitechGSDK.LogiPlaySpringForce(0, 0, 20, 20);

            // 3. （可选）如果你想在土路或者草地上有颠簸感，可以取消下面这行的注释
            // LogitechGSDK.LogiPlaySurfaceEffect(0, 0, 50, 1000); 
        }
    }
    // 把这段代码粘贴到 Update 方法的下方
    public void SetManualForceFeedbackEffect(float force)
    {
        if (!_isInitialized) return;

        // 既然换了官方 SDK，手动驾驶时的基础阻尼/回正力通常由方向盘硬件或 G HUB 自动接管。
        // 所以这里我们完全留空，仅仅是为了满足 ManualController 的调用，消除报错。

        // 如果你觉得手动驾驶时方向盘太轻（轻飘飘的没有摩擦力），
        // 你可以取消下面这行的注释，给方向盘加上 10% 的持久黏滞阻尼感：
        // LogitechGSDK.LogiPlayDamperForce(0, 10); 
    }

    private void OnApplicationQuit()
    {
        ReleaseFFB();
    }

    private void OnDestroy()
    {
        ReleaseFFB();
    }

    private void ReleaseFFB()
    {
        if (_isInitialized)
        {
            LogitechGSDK.LogiSteeringShutdown();
            _isInitialized = false;
        }
    }
}