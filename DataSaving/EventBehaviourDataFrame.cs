using System; // 必须添加

[Serializable] // 【关键修复】：有了这个标签，JSON才能把这个类的数据写进去
public class EventBehaviourDataFrame
{
    public string EventName;

    // --- 新增的MR数据字段 ---
    public double MRTimeStamp;
    public float ConfiguredMRInterval; // 记录实验设定的秒数（3/5/7/9）
    // ----------------------

    public double StartofEventTimeStamp; // ToR触发时间
    public double EndOfEventTimeStamp;
    public double EventDuration;
    public bool SuccessfulCompletionState;
    public string HitObjectName;
}