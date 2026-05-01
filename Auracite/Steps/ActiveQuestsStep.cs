using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Auracite;

public class ActiveQuestsStep : IStep
{
    public event IStep.CompletedDelegate? Completed;

    public void Run()
    {
        var list = new List<ActiveQuest>();

        unsafe
        {
            var questManager = QuestManager.Instance();
            if (questManager != null)
            {
                var normal = questManager->NormalQuests;
                for (int i = 0; i < normal.Length; i++)
                {
                    var qw = normal[i];
                    if (qw.QuestId == 0) continue;

                    var aq = new ActiveQuest
                    {
                        id = qw.QuestId,
                        sequence = qw.Sequence,
                        flags = qw.Flags,
                        accept_class_job = qw.AcceptClassJob,
                        is_hidden = qw.IsHidden,
                        is_priority = qw.IsPriority,
                        todo_checked = ReadTodoChecked(qw.Variables),
                        kind = "normal",
                    };
                    list.Add(aq);
                }
            }
            // DailyQuests intentionally excluded from v1: DailyQuestWork on the
            // currently referenced FFXIVClientStructs build does not expose
            // Sequence / AcceptClassJob / IsPriority / IsHidden, so it is not
            // schema-compatible with QuestWork. Levequests are out of scope.
        }

        Plugin.package!.active_quests = list;

        Completed?.Invoke();
    }

    // QuestWork on the FFXIVClientStructs build referenced at compile time does
    // not expose IsTodoChecked (that method lives on QuestEventHandler and
    // requires a BattleChara* — not usable here). The plan's documented
    // fallback was QuestWork.Variables[6], but on this build Variables is a
    // 6-byte span (indices 0..5), so we read the last byte (Variables[5]) as
    // the todo bitmap. If the actual byte differs in practice, adjust here.
    private static List<bool> ReadTodoChecked(System.Span<byte> variables)
    {
        var result = new List<bool>(8);
        byte bits = variables.Length >= 6 ? variables[5] : (byte)0;
        for (int i = 0; i < 8; i++)
        {
            result.Add((bits & (1 << i)) != 0);
        }
        return result;
    }

    public string StepName()
    {
        return "Active Quests";
    }

    public string StepDescription()
    {
        return "No user action required.";
    }

    public void Dispose()
    {
    }
}
