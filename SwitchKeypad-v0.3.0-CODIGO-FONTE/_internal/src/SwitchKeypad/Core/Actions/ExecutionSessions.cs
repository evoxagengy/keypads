using SwitchKeypad.Core.Models;

namespace SwitchKeypad.Core.Actions;

/// <summary>One execution per profile/key; all potentially long work runs off the UI thread.</summary>
public sealed class ExecutionSessions
{
    private sealed record Session(CancellationTokenSource Cancellation,bool Continuous);
    private readonly object _gate=new();
    private readonly Dictionary<string,Session> _running=[];

    public Task<ActionResult> TriggerAsync(string profileId,string key,ActionDefinition action,ActionExecutor executor)
    {
        string id=profileId+":"+key;
        lock(_gate)
        {
            if(_running.TryGetValue(id,out var current))
            {
                if(current.Continuous){current.Cancellation.Cancel();return Task.FromResult(new ActionResult(ActionStatus.Cancelled,"Interrompendo sequência"));}
                return Task.FromResult(ActionResult.Ok("Ação já está em execução"));
            }
            var session=new Session(new(),action.Type==ActionType.MultiAction&&action.Repeat.Mode==SequenceRepeatMode.UntilCancelled);
            _running.Add(id,session);
            return Task.Run(async ()=>
            {
                try{return await executor.ExecuteAsync(action,session.Cancellation.Token).ConfigureAwait(false);}
                finally
                {
                    lock(_gate){_running.Remove(id);session.Cancellation.Dispose();}
                }
            });
        }
    }
    public void CancelAll()
    {
        lock(_gate)foreach(var session in _running.Values)session.Cancellation.Cancel();
    }
}
