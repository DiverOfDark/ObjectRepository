using System;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public interface ITrackable
    {
        event Action<ModelChangedEventArgs> ModelChanged;
    }
}