using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private static readonly (double X, double Y, TravelMode Mode)[] RaceCheckpoints = [(6,6,TravelMode.Walk),(24,6,TravelMode.Skateboard),(40,22,TravelMode.Raft),(45,22,TravelMode.Swim)];
    private bool HasRaceLoan(string playerId, string item) => item is "skateboard" or "inflatableRaft" or "swimmies" &&
        _players.GetValueOrDefault(playerId)?.LocationId.StartsWith("race:") == true;
    private async Task<QuestActionResult> StartAdventureRaceAsync(PlayerState player, QuestState quest, CancellationToken token)
    {
        var id="race:"+player.Id+":"+Guid.NewGuid().ToString("N");var exit=player.Position with{X=6,Y=6,Z=0};
        _returnPositions[player.Id]=player.Position;
        var rival=new ActorState(id+":rival",EntityKind.Npc,"raceRival","Captain Wrong Turn",exit with{X=8},FriendRating:0,LocationId:id);
        _dungeons[id]=new(id,quest.Id,60,40,[new(0,0,60,40)],[],exit,[rival],[],[],WaterAreas:[new(30,0,3,40,false),new(33,0,27,40,true)]);
        quest=quest with{Progress=0,DeadlineUtc=_probulatorClock.GetUtcNow().AddMinutes(3),NextStagePosition=exit,NextStageName="Checkpoint 1: Walk",NextStageLocationId=id,Description="Three-minute race! Follow four checkpoints: Walk, Skateboard, Raft, Swim. Loan equipment only works inside this course."};
        _quests[(player.Id,quest.Id)]=quest;await _store.SaveQuestAsync(Configuration.Id,quest,token);
        player=player with{Position=exit,LocationId=id,TravelMode=TravelMode.Walk,Version=player.Version+1};await SavePlayerAsync(player,token);
        return new(GetPrivateState(player.Id),player,quest,quest.Description);
    }
    private async Task<QuestActionResult> CheckAdventureRaceAsync(PlayerState player, QuestState quest, CancellationToken token)
    {
        if(player.LocationId!=quest.NextStageLocationId||!player.LocationId.StartsWith("race:")||quest.Progress>=4)throw new InvalidOperationException("Start the race at its outdoor marker.");
        var checkpoint=RaceCheckpoints[quest.Progress];var point=player.Position with{X=checkpoint.X,Y=checkpoint.Y};
        if(player.Position.Distance2D(point)>3||player.TravelMode!=checkpoint.Mode)throw new InvalidOperationException($"Reach checkpoint {quest.Progress+1} using {checkpoint.Mode}.");
        var next=quest.Progress+1;
        if(next==4)
        {
            quest=quest with{Progress=4,Status="ready",Description="You beat Captain Wrong Turn. Return to the race organizer.",NextStageLocationId="outdoor",NextStagePosition=_returnPositions[player.Id]};
            _quests[(player.Id,quest.Id)]=quest;await _store.SaveQuestAsync(Configuration.Id,quest,token);
            player=await ExitDungeonAsync(player.Id,token);await UnlockAchievementAsync(player.Id,"Professionally Inconvenienced",token);
        }
        else
        {
            var target=RaceCheckpoints[next];quest=quest with{Progress=next,NextStagePosition=player.Position with{X=target.X,Y=target.Y},NextStageName=$"Checkpoint {next+1}: {target.Mode}"};
            _quests[(player.Id,quest.Id)]=quest;await _store.SaveQuestAsync(Configuration.Id,quest,token);
        }
        return new(GetPrivateState(player.Id),player,quest,quest.Description);
    }
}
