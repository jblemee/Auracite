using FFXIVClientStructs.FFXIV.Client.Game;

namespace Auracite;

public class HuntingLogStep : IStep
{
    public event IStep.CompletedDelegate? Completed;

    private const int CategoryCount = 12;
    private const int RankCount = 10;
    private const int TargetCount = 4;

    public void Run()
    {
        unsafe
        {
            var mgr = MonsterNoteManager.Instance();
            if (mgr == null)
            {
                Completed?.Invoke();
                return;
            }

            for (int catIdx = 0; catIdx < CategoryCount; catIdx++)
            {
                ref var rankInfo = ref mgr->RankData[catIdx];

                var category = new HuntingLogCategory
                {
                    index = catIdx,
                    flags = rankInfo.Flags,
                    current_rank = rankInfo.Rank,
                };

                for (int rankIdx = 0; rankIdx < RankCount; rankIdx++)
                {
                    ref var rankData = ref rankInfo.RankData[rankIdx];

                    var rank = new HuntingLogRank
                    {
                        rank = rankIdx,
                    };

                    for (int targetIdx = 0; targetIdx < TargetCount; targetIdx++)
                    {
                        rank.targets.Add(new HuntingLogTarget
                        {
                            count = rankData.Counts[targetIdx],
                        });
                    }

                    category.ranks.Add(rank);
                }

                Plugin.package!.hunting_log.Add(category);
            }
        }

        Completed?.Invoke();
    }

    public string StepName()
    {
        return "Hunting Log";
    }

    public string StepDescription()
    {
        return "No user action required.";
    }

    public void Dispose()
    {
    }
}
