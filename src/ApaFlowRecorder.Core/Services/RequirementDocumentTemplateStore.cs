namespace ApaFlowRecorder.Core.Services;

public sealed class RequirementDocumentTemplateStore
{
    public const string DefaultTemplate =
        """
        # [流程名称]

        ## 一、流程概述

        - **目标**：用一句话说清楚这个流程要干什么
        - **触发方式**：手动运行 / 定时调度 / 被其他流程调用
        - **最终产出**：明确列出流程结束时会生成什么（文件、数据、状态）

        ## 二、输入与输出

        ### 输入

        | 参数名 | 类型 | 必填 | 默认值 | 说明 |
        |--------|------|------|--------|------|
        | xxx | string/number/boolean | 是/否 | "" | 这是什么，从哪来 |

        ### 输出

        | 输出项 | 格式 | 说明 |
        |--------|------|------|
        | xxx | CSV/Excel/JSON/日志 | 字段说明 |

        ## 三、操作步骤（按执行顺序编号）

        ### 步骤1：[步骤标题]
        - **操作对象**：哪个系统/网站/应用？URL或窗口名称？
        - **具体动作**：打开页面 → 点击XX按钮 → 填写XX字段 → 提交
        - **关键元素**：需要操作的界面元素描述（如"登录按钮"、"数据表格"）
        - **预期结果**：这一步完成后应该看到什么/得到什么
        - **失败处理**：出错了怎么办（重试？跳过？中止？）

        ### 步骤2：[步骤标题]
        （同上格式）

        ...（每个步骤一节）

        ## 四、业务规则与约束

        - 规则1：如"汇率保留4位小数"、"文件名必须包含日期"
        - 规则2：如"超过3次重试则中止并告警"
        - 规则3：...

        ## 五、异常场景

        | 异常情况 | 处理方式 |
        |----------|----------|
        | 页面打不开 | 重试3次，每次间隔5秒 |
        | 数据为空 | 记录警告，继续执行还是中止？ |
        | 网络超时 | ... |

        ## 六、参考信息（可选但非常有帮助）

        - **截图**：关键页面的截图（标注了需要操作的元素）
        - **示例数据**：输入/输出的真实样例
        - **已有账号/凭据**：涉及的登录信息说明
        """;

    private readonly string _templatePath;

    public RequirementDocumentTemplateStore(string? templatePath = null)
    {
        _templatePath = string.IsNullOrWhiteSpace(templatePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ApaFlowRecorder",
                "requirement-document-template.md")
            : templatePath;
    }

    public string TemplatePath => _templatePath;

    public string LoadOrCreateDefault()
    {
        if (!File.Exists(_templatePath))
        {
            SaveDefault();
        }

        try
        {
            var template = File.ReadAllText(_templatePath);
            return string.IsNullOrWhiteSpace(template)
                ? DefaultTemplate
                : template;
        }
        catch
        {
            return DefaultTemplate;
        }
    }

    public void Save(string template)
    {
        var directory = Path.GetDirectoryName(_templatePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_templatePath, template);
    }

    public string ResetToDefault()
    {
        SaveDefault();
        return DefaultTemplate;
    }

    private void SaveDefault() => Save(DefaultTemplate);
}
