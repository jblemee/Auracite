using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace Auracite;

public class ActiveQuestsStep : IStep
{
    public event IStep.CompletedDelegate? Completed;

    // QuestEventHandler::IsTodoChecked is the only known way to read
    // per-objective completion. The bits are not stored in QuestWork.Variables
    // (that span holds quest-script-specific scratch state), they live on the
    // QuestEventHandler instance and require a BattleChara* (the local player)
    // plus a step index 0..N. Practical upper bound observed in the wild
    // (ProfileBuilder, SpeakWithWukLamat) is 6 objectives; the FFXIV ToDoList
    // addon never shows more. We probe 0..7 anyway to keep the JSON schema
    // (`todo_checked` = 8 bools) stable; out-of-range probes return false.
    // Sequence 255 means "turn-in step": no objectives, all bits false.
    private const int TodoSlots = 8;

    public void Run()
    {
        var list = new List<ActiveQuest>();

        unsafe
        {
            var questManager = QuestManager.Instance();
            var eventFramework = EventFramework.Instance();
            var localPlayer = Plugin.ObjectTable.LocalPlayer;
            BattleChara* battleChara = null;
            if (localPlayer != null)
            {
                battleChara = (BattleChara*)localPlayer.Address;
            }

            if (questManager != null)
            {
                var normal = questManager->NormalQuests;
                for (int i = 0; i < normal.Length; i++)
                {
                    var qw = normal[i];
                    if (qw.QuestId == 0) continue;

                    var aq = new ActiveQuest
                    {
                        // QuestWork.QuestId is the lower 16 bits; the canonical
                        // Excel `Quest` sheet row id is offset by 0x10000.
                        // Storing the full 32-bit id keeps the JSON directly
                        // resolvable via xivapi without arithmetic on the
                        // consumer side.
                        id = (uint)qw.QuestId + 0x10000U,
                        sequence = qw.Sequence,
                        flags = qw.Flags,
                        accept_class_job = qw.AcceptClassJob,
                        is_hidden = qw.IsHidden,
                        is_priority = qw.IsPriority,
                        todo_checked = ReadTodoChecked(eventFramework, battleChara, qw.QuestId, qw.Sequence),
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

    private static unsafe List<bool> ReadTodoChecked(EventFramework* eventFramework, BattleChara* battleChara, ushort questId, byte sequence)
    {
        var result = new List<bool>(TodoSlots);
        for (int i = 0; i < TodoSlots; i++) result.Add(false);

        // Turn-in step has no objectives.
        if (sequence == 255) return result;
        if (eventFramework == null || battleChara == null) return result;

        var handler = eventFramework->GetEventHandlerById(questId);
        if (handler == null) return result;

        var questHandler = (QuestEventHandler*)handler;
        for (int i = 0; i < TodoSlots; i++)
        {
            try
            {
                result[i] = questHandler->IsTodoChecked(battleChara, (byte)i);
            }
            catch
            {
                // Defensive: native call may read OOB at high indices for
                // quests with few objectives. Leave the slot at false.
                break;
            }
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
