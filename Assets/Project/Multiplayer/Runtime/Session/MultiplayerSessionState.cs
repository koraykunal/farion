namespace Farion.Multiplayer.Session
{
    public enum MultiplayerSessionState
    {
        Idle = 0,
        Starting = 1,
        Connected = 2,
        Stopping = 3,
        Failed = 4
    }

    public enum MultiplayerFailureReason
    {
        None = 0,
        ConnectionFailed = 1,
        ConnectionLost = 2,
        ProtocolMismatch = 3,
        SessionSetup = 4,
        InvalidAddress = 5,
        ServerFull = 6,
        SaveLoadFailed = 7
    }
}
