namespace Cap.Multiplayer
{
    // Vivox mute setters return before the server acknowledges the change.
    // Keep one outstanding request until acknowledgement, or report a bounded failure.
    public sealed class CapVoiceMuteGate
    {
        private bool pending, requested, failed;
        private float started;
        public bool TryRequest(bool desired,bool observed,float now,out bool timedOut)
        {
            timedOut=false;
            if(failed)return false;
            if(pending)
            {
                if(observed==requested)pending=false;
                else
                {
                    if(now-started>=5){failed=true;timedOut=true;}
                    return false;
                }
            }
            if(desired==observed)return false;
            requested=desired;started=now;pending=true;return true;
        }
        public void Reset(){pending=false;failed=false;}
    }
}
