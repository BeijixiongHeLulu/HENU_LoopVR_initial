using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AIController),typeof(ManualController))]
public class ControlSwitch : MonoBehaviour
{
    private AIController _aiController;

    private ManualController _manualControl;

    [SerializeField] private bool manualDriving;
    
    // Start is called before the first frame update
    void Awake()
    {
        _aiController = GetComponent<AIController>();
        _manualControl = GetComponent<ManualController>();
    }

    void Start()
    {
        //_aiController = GetComponent<AIController>();
        //_manualControl = GetComponent<ManualController>();

        _aiController.SetManualOverride(manualDriving);
    }


    private void SetManualDrivingState(bool state)
    {
        _aiController.SetManualOverride(state);
        _manualControl.SetManualDriving(state);

        // --- [新增] 发出状态切换 Marker 10(自动) 或 11(手动) ---
        if (SyncManager.Instance != null)
        {
            if (state == true) // true 代表开启了手动接管
            {
                SyncManager.Instance.TriggerEvent(11);
            }
            else // false 代表切回了自动驾驶
            {
                SyncManager.Instance.TriggerEvent(10);
            }
        }
    }

    public bool GetManualDrivingState()
    {
        return manualDriving;
    }

    public void SwitchControl()
    {
        Debug.Log("Manual Driving is  switching");
        manualDriving = !manualDriving;
        
        SetManualDrivingState(manualDriving);
    }

    public void SwitchControl(bool state)
    {
        manualDriving = state;       
        SetManualDrivingState(manualDriving);
    }
}
