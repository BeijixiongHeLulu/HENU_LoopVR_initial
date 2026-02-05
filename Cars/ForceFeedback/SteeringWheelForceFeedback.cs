using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForceFeedback; // 确保引用了你的插件命名空间

public class SteeringWheelForceFeedback : MonoBehaviour
{
    [SerializeField] private CarController _carController;
    [SerializeField] private ManualController _manualController;
    [SerializeField] private ControlSwitch _controlSwitch;

    public bool shouldInit = true;
    private float target;
    private float current;

    // 【新增】初始化状态标记，防止未初始化调用导致崩溃
    private bool _isInitialized = false;

    void Start()
    {
        _carController = GetComponent<CarController>();
        _manualController = GetComponent<ManualController>();
        _controlSwitch = GetComponent<ControlSwitch>();

        // 【关键修改】只在 非编辑器模式 (打包后) 初始化力反馈
        // 在编辑器里运行时跳过这一步，防止 DLL 重复加载导致崩溃
#if !UNITY_EDITOR
        if (shouldInit && FFB.needInit)
        {
            FFB.ForceFeedBackInit();
            FFB.AcquireDevice();
            _isInitialized = true;
        }
#else
        Debug.LogWarning("Steering Wheel Force Feedback is DISABLED in Editor to prevent crashes.");
#endif
    }

#if UNITY_EDITOR
    private void StopDirectInput(UnityEditor.PlayModeStateChange state)
    {
        // 原有代码注释说这里会崩，所以保持注释或不做操作
    }
#endif

    private void Update()
    {
        if (_carController == null || _manualController == null || _controlSwitch == null) return;

        target = _carController.GetSterring();
        current = _manualController.GetSteeringInput();

        if (!_controlSwitch.GetManualDrivingState())
        {
            int sign = 0;
            if ((current - target) > 0)
                sign = -1;
            else
            {
                sign = 1;
            }
            SetAutoPilotForceFeedbackEffect(8000 * sign * (Mathf.Abs(current) - Mathf.Abs(target)));
        }
    }

    public void SetAutoPilotForceFeedbackEffect(float force)
    {
        // 【安全检查】如果没有初始化（比如在编辑器里），直接返回，不要调用底层API
        if (!_isInitialized) return;

        int rounded = (int)-force;
        FFB.SetDeviceForceFeedback(rounded, 0);
    }

    public void SetManualForceFeedbackEffect(float force)
    {
        if (!_isInitialized) return;

        int rounded = (int)force;
        FFB.SetDeviceForceFeedback(rounded, 0);
    }

#if !UNITY_EDITOR
    private void OnDestroy()
    {
        if (_isInitialized)
        {
            if (!FFB.needInit)
                FFB.FreeDirectInput();
            else
            {
                FFB.needInit = false;
            }
        }
    }
#endif
}