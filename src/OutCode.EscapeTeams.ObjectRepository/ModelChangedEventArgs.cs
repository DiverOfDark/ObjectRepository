namespace OutCode.EscapeTeams.ObjectRepository
{
    public class ModelChangedEventArgs
    {
        public static ModelChangedEventArgs Added(ModelBase source)
        {
            return new ModelChangedEventArgs
            {
                Source = source,
                Entity = source.Entity,
                ChangeType = ChangeType.Add
            };
        }

        public static ModelChangedEventArgs Removed(ModelBase source)
        {
            return new ModelChangedEventArgs
            {
                Source = source,
                Entity = source.Entity,
                ChangeType = ChangeType.Remove
            };
        }

        public static ModelChangedEventArgs PropertyChange(ModelBase source, string propertyName, object from,
            object to)
        {
            return new ModelChangedEventArgs
            {
                Source = source,
                Entity = source.Entity,
                ChangeType = ChangeType.Update,
                PropertyName = propertyName,
                OldValue = from,
                NewValue = to
            };
        }
        
        public ModelBase Source { get; private set; }
        public BaseEntity Entity { get; private set; }
        public ChangeType ChangeType { get; private set; }
        public string PropertyName { get; private set; }
        public object OldValue { get; private set; }
        public object NewValue { get; private set; }
    }
}