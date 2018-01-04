using System;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public class LoadingInProgressException : Exception
    {
        public double Progress { get; set; }

        public LoadingInProgressException(double progress)
        {
            Progress = progress;
        }

        public override string Message => Progress + "% loading done...";
    }
}