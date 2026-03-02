using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SceneDataRecorder : MonoBehaviour
{
    public static SceneDataRecorder Instance { get; private set; }
    
    private List<EventBehaviourDataFrame> _eventBehaviourDataFrames;
    private SceneData _sceneData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        _sceneData = new SceneData();
        _eventBehaviourDataFrames = new List<EventBehaviourDataFrame>();
    }

    // 替换原有的 AssignEventData 方法
    public void AssignEventData(string eventName, double mrTime, float configuredInterval, double torTime, double endTime, bool successState, string hitObject = null)
    {
        EventBehaviourDataFrame eventBehaviour = new EventBehaviourDataFrame();

        if (_eventBehaviourDataFrames.Any())
        {
            if (_eventBehaviourDataFrames.Last().EventName != null && _eventBehaviourDataFrames.Last().EventName == eventName)
                _eventBehaviourDataFrames.RemoveAt(_eventBehaviourDataFrames.Count - 1);
        }

        eventBehaviour.EventName = eventName;
        eventBehaviour.MRTimeStamp = mrTime;                       // 写入MR时间
        eventBehaviour.ConfiguredMRInterval = configuredInterval;  // 写入设定的条件秒数
        eventBehaviour.StartofEventTimeStamp = torTime;            // 写入ToR时间
        eventBehaviour.EndOfEventTimeStamp = endTime;
        eventBehaviour.EventDuration = endTime - torTime;
        eventBehaviour.SuccessfulCompletionState = successState;
        eventBehaviour.HitObjectName = hitObject;

        _eventBehaviourDataFrames.Add(eventBehaviour);
    }

    public SceneData GetDataFrame()
    {
        _sceneData.AverageSceneFPS = EyetrackingManager.Instance.GetAverageSceneFPS();
        _sceneData.EventBehavior = _eventBehaviourDataFrames;
        return _sceneData;
    }
}
