using UnityEngine;

public enum MemorySceneType
{
    Classroom,
    CarBackseat,
    ConvenienceStore,
    Bedroom,
    Fallback
}

public class SimpleSceneClassifier : MonoBehaviour
{
    public MemorySceneType Classify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return MemorySceneType.Fallback;

        if (ContainsAny(input, "教室", "学校", "同学", "老师", "课桌", "黑板", "粉笔", "风扇", "晚自习", "上课"))
            return MemorySceneType.Classroom;

        if (ContainsAny(input, "车", "出租车", "公交", "后座", "车窗", "路灯", "回家", "下班", "堵车", "司机"))
            return MemorySceneType.CarBackseat;

        if (ContainsAny(input, "便利店", "躲雨", "自动门", "烤肠", "货架", "收银台", "暖光", "湿鞋"))
            return MemorySceneType.ConvenienceStore;

        if (ContainsAny(input, "卧室", "房间", "床", "外婆家", "老家", "窗边", "电视", "厨房", "防盗窗", "小时候"))
            return MemorySceneType.Bedroom;

        return MemorySceneType.Fallback;
    }

    private bool ContainsAny(string input, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (input.Contains(keyword))
                return true;
        }
        return false;
    }
}